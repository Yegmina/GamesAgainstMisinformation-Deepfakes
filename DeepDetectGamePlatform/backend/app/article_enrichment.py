from __future__ import annotations

import json
import os
import re
import threading
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
from pathlib import Path
from typing import Any
from urllib.parse import urljoin

import requests
from bs4 import BeautifulSoup

from . import ai_agents
from .db import connect, load_game

PROJECT_DIR = Path(__file__).resolve().parents[2]
MEDIA_DIR = PROJECT_DIR / "data" / "media"
NEWS_MEDIA_DIR = MEDIA_DIR / "news"

ARTICLE_FIELDS = {
    "article_status",
    "article_mode",
    "article_byline",
    "article_paragraphs",
    "article_image_url",
    "article_image_caption",
    "article_image_credit",
    "article_source_url",
    "article_updated_at",
    "article_error",
    "article_agent_mode",
    "article_agent_model",
    "article_image_model",
}

_executor: ThreadPoolExecutor | None = None
_jobs_lock = threading.Lock()
_active_jobs: set[tuple[str, int, str]] = set()


def ensure_media_dirs() -> None:
    NEWS_MEDIA_DIR.mkdir(parents=True, exist_ok=True)


def schedule_article_enrichment(game_id: str, user_id: int) -> None:
    state = load_game(game_id, user_id)
    if not state:
        return
    for item in state.get("news_items", []) or []:
        if not isinstance(item, dict):
            continue
        item_id = str(item.get("id") or "")
        if not item_id or item.get("article_status") != "pending":
            continue
        key = (game_id, int(user_id), item_id)
        with _jobs_lock:
            if key in _active_jobs:
                continue
            _active_jobs.add(key)
        _pool().submit(_run_article_job, game_id, int(user_id), item_id)


def merge_article_fields(target_state: dict[str, Any], source_state: dict[str, Any] | None) -> dict[str, Any]:
    if not target_state or not source_state:
        return target_state
    source_items = {
        item.get("id"): item
        for item in source_state.get("news_items", []) or []
        if isinstance(item, dict) and item.get("id")
    }
    for item in target_state.get("news_items", []) or []:
        if not isinstance(item, dict):
            continue
        source = source_items.get(item.get("id"))
        if not source:
            continue
        source_status = str(source.get("article_status") or "")
        if source_status not in {"generating", "ready", "failed"}:
            continue
        for field in ARTICLE_FIELDS:
            if field in source:
                item[field] = source[field]
    return target_state


def _pool() -> ThreadPoolExecutor:
    global _executor
    if _executor is None:
        workers = max(1, min(12, int(os.getenv("ARTICLE_ENRICHMENT_CONCURRENCY", "6"))))
        _executor = ThreadPoolExecutor(max_workers=workers, thread_name_prefix="article-enrich")
    return _executor


def _run_article_job(game_id: str, user_id: int, item_id: str) -> None:
    key = (game_id, user_id, item_id)
    try:
        _update_article_fields(game_id, user_id, item_id, {"article_status": "generating", "article_error": "", "article_updated_at": _now_iso()})
        state = load_game(game_id, user_id)
        item = _find_item(state, item_id) if state else None
        if not item:
            return
        if item.get("truth_label") == "real":
            fields = _build_real_article(item)
        else:
            fields = _build_synthetic_article(game_id, item)
        fields["article_status"] = "ready"
        fields["article_updated_at"] = _now_iso()
        fields["article_error"] = fields.get("article_error") or ""
        _update_article_fields(game_id, user_id, item_id, fields)
    except Exception as exc:
        fallback_state = load_game(game_id, user_id)
        fallback_item = _find_item(fallback_state, item_id) if fallback_state else None
        _update_article_fields(game_id, user_id, item_id, _fallback_fields(fallback_item, exc))
    finally:
        with _jobs_lock:
            _active_jobs.discard(key)


def _build_real_article(item: dict[str, Any]) -> dict[str, Any]:
    scraped = _scrape_source(str(item.get("url") or ""))
    result = ai_agents.generate_real_article(item, scraped)
    data = result.data
    return {
        "article_mode": "real_source",
        "article_byline": data.get("byline") or scraped.get("byline") or "DeepDetect Wire",
        "article_paragraphs": _safe_paragraphs(data.get("paragraphs"), item),
        "article_image_url": scraped.get("image_url") or "",
        "article_image_caption": data.get("image_caption") or scraped.get("image_caption") or "",
        "article_image_credit": scraped.get("image_credit") or item.get("source") or "",
        "article_source_url": item.get("url") or "",
        "article_agent_mode": result.mode,
        "article_agent_model": result.model,
    }


def _build_synthetic_article(game_id: str, item: dict[str, Any]) -> dict[str, Any]:
    result = ai_agents.generate_synthetic_article(item)
    data = result.data
    fields = {
        "article_mode": "synthetic",
        "article_byline": data.get("byline") or "DeepDetect Wire",
        "article_paragraphs": _safe_paragraphs(data.get("paragraphs"), item),
        "article_image_url": "",
        "article_image_caption": data.get("image_caption") or "",
        "article_image_credit": "AI-generated in-game image",
        "article_source_url": item.get("url") or "",
        "article_agent_mode": result.mode,
        "article_agent_model": result.model,
    }
    prompt = str(data.get("image_prompt") or "").strip()
    if prompt:
        try:
            image_result = ai_agents.generate_article_image(prompt)
            fields["article_image_url"] = _write_article_image(game_id, str(item.get("id") or "news"), image_result.data)
            fields["article_image_model"] = image_result.model
        except Exception as exc:
            fields["article_error"] = f"Image generation unavailable. {_safe_error(exc)}"
    return fields


def _scrape_source(url: str) -> dict[str, Any]:
    if not url.lower().startswith(("http://", "https://")):
        return {}
    response = requests.get(
        url,
        timeout=10,
        headers={"User-Agent": "DeepDetectGamePlatform/1.0 (+https://deepdetect.game)"},
    )
    response.raise_for_status()
    soup = BeautifulSoup(response.text, "html.parser")
    base_url = response.url or url
    title = _meta(soup, "og:title") or _text(soup.title)
    description = _meta(soup, "og:description") or _meta(soup, "description")
    image_url = _meta(soup, "og:image") or _meta(soup, "twitter:image")
    byline = _meta(soup, "author") or _first_text(soup, ['[rel="author"]', ".byline", "[class*=byline]", "[class*=author]"])
    published_at = _meta(soup, "article:published_time") or _meta(soup, "date")
    paragraphs = _extract_paragraphs(soup)
    return {
        "title": title,
        "description": description,
        "byline": byline,
        "published_at": published_at,
        "image_url": urljoin(base_url, image_url) if image_url else "",
        "image_caption": _first_text(soup, ["figcaption", "[class*=caption]"]),
        "image_credit": _first_text(soup, ["[class*=credit]", "[class*=copyright]"]),
        "paragraphs": paragraphs,
        "url": base_url,
    }


def _extract_paragraphs(soup: BeautifulSoup) -> list[str]:
    selectors = ["article p", "main p", "[role=main] p", "p"]
    seen: set[str] = set()
    paragraphs: list[str] = []
    for selector in selectors:
        for node in soup.select(selector):
            text = _clean(node.get_text(" ", strip=True))
            if len(text) < 60 or text in seen:
                continue
            seen.add(text)
            paragraphs.append(text)
            if sum(len(p) for p in paragraphs) > 9000:
                return paragraphs
        if paragraphs:
            break
    return paragraphs


def _update_article_fields(game_id: str, user_id: int, item_id: str, fields: dict[str, Any]) -> None:
    allowed = {key: value for key, value in fields.items() if key in ARTICLE_FIELDS}
    if not allowed:
        return
    with connect() as con:
        con.execute("BEGIN IMMEDIATE")
        row = con.execute(
            "SELECT state_json FROM games WHERE id = ? AND user_id = ?",
            (game_id, user_id),
        ).fetchone()
        if not row:
            return
        state = json.loads(row["state_json"])
        item = _find_item(state, item_id)
        if not item:
            return
        item.update(allowed)
        con.execute(
            "UPDATE games SET state_json = ?, updated_at = CURRENT_TIMESTAMP WHERE id = ? AND user_id = ?",
            (json.dumps(state), game_id, user_id),
        )


def _write_article_image(game_id: str, item_id: str, image_bytes: bytes) -> str:
    ensure_media_dirs()
    filename = f"{_slug(game_id)}-{_slug(item_id)}.png"
    path = NEWS_MEDIA_DIR / filename
    path.write_bytes(image_bytes)
    return f"/media/news/{filename}"


def _fallback_fields(item: dict[str, Any] | None, exc: Exception) -> dict[str, Any]:
    return {
        "article_status": "failed",
        "article_mode": "fallback",
        "article_byline": "DeepDetect Wire",
        "article_paragraphs": _safe_paragraphs([], item or {}),
        "article_image_url": "",
        "article_image_caption": "",
        "article_image_credit": "",
        "article_source_url": (item or {}).get("url") or "",
        "article_updated_at": _now_iso(),
        "article_error": _safe_error(exc),
    }


def _find_item(state: dict[str, Any] | None, item_id: str) -> dict[str, Any] | None:
    if not state:
        return None
    return next((item for item in state.get("news_items", []) or [] if isinstance(item, dict) and item.get("id") == item_id), None)


def _safe_paragraphs(value: Any, item: dict[str, Any]) -> list[str]:
    paragraphs = [str(p).strip() for p in (value or []) if str(p).strip()]
    if paragraphs:
        return paragraphs[:10]
    summary = str(item.get("summary") or "").strip()
    if summary:
        return [summary]
    return ["This developing story needs more verification before it moves to the front page."]


def _meta(soup: BeautifulSoup, key: str) -> str:
    node = soup.find("meta", attrs={"property": key}) or soup.find("meta", attrs={"name": key})
    return str(node.get("content") or "").strip() if node else ""


def _first_text(soup: BeautifulSoup, selectors: list[str]) -> str:
    for selector in selectors:
        node = soup.select_one(selector)
        text = _text(node)
        if text:
            return text
    return ""


def _text(node: Any) -> str:
    return _clean(node.get_text(" ", strip=True)) if node else ""


def _clean(text: str) -> str:
    return re.sub(r"\s+", " ", text or "").strip()


def _slug(value: str) -> str:
    slug = re.sub(r"[^a-zA-Z0-9_.-]+", "-", value).strip("-")
    return slug[:96] or "article"


def _safe_error(exc: Exception) -> str:
    message = str(exc).replace(os.getenv("OPENAI_API_KEY") or "", "")
    normalized = " ".join(message.split())
    lower = normalized.lower()
    if "unknown parameter" in lower:
        return "Image request option was not accepted by the image service."
    if "rate limit" in lower or "rate_limit" in lower or "429" in lower:
        return "Image service is temporarily rate-limited."
    if "quota" in lower or "billing" in lower or "insufficient" in lower:
        return "Image service quota is unavailable."
    if "401" in lower or "unauthorized" in lower or "api key" in lower:
        return "Image service is not configured."
    return normalized[:180]


def _now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()
