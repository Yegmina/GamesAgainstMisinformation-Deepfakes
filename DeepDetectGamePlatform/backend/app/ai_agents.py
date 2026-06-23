from __future__ import annotations

import json
import os
import base64
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Generic, TypeVar

import requests
from dotenv import load_dotenv
from openai import APIStatusError, OpenAI, RateLimitError

try:
    from google import genai
    from google.genai import types
except ImportError:  # pragma: no cover - exercised only before dependencies are installed
    genai = None
    types = None

APP_ROOT = Path(__file__).resolve().parents[2]
REPO_ROOT = APP_ROOT.parent

load_dotenv(REPO_ROOT / ".env")
load_dotenv(APP_ROOT / ".env", override=True)

OPENAI_MODEL = os.getenv("OPENAI_MODEL_AGENT", "gpt-5.3-chat-latest")
OPENAI_IMAGE_MODEL = os.getenv("OPENAI_IMAGE_MODEL", "gpt-image-2")
GEMINI_MODEL = os.getenv("GEMINI_MODEL_AGENT", "gemini-3.1-flash-lite")
MODEL = OPENAI_MODEL
DEFAULT_PROVIDER = "gemini"

T = TypeVar("T")


@dataclass(frozen=True)
class AgentResult(Generic[T]):
    data: T
    mode: str
    model: str


def configured_provider() -> str:
    provider = os.getenv("AI_PROVIDER", DEFAULT_PROVIDER).strip().lower()
    if provider not in {"auto", "openai", "gemini"}:
        raise RuntimeError("AI_PROVIDER must be one of: auto, openai, gemini.")
    return provider


def enabled() -> bool:
    provider = configured_provider()
    if provider == "openai":
        return bool(os.getenv("OPENAI_API_KEY"))
    if provider == "gemini":
        return bool(os.getenv("GEMINI_API_KEY"))
    return bool(os.getenv("OPENAI_API_KEY") or os.getenv("GEMINI_API_KEY"))


def _provider_order() -> list[str]:
    provider = configured_provider()
    if provider in {"openai", "gemini"}:
        return [provider]

    providers: list[str] = []
    if os.getenv("GEMINI_API_KEY"):
        providers.append("gemini")
    if os.getenv("OPENAI_API_KEY"):
        providers.append("openai")
    return providers


def _provider_model(provider: str) -> str:
    return GEMINI_MODEL if provider == "gemini" else OPENAI_MODEL


def _openai_quota_exhausted(exc: Exception) -> bool:
    if isinstance(exc, RateLimitError):
        return True
    if isinstance(exc, APIStatusError) and exc.status_code == 429:
        return True
    message = str(exc).lower()
    return "insufficient_quota" in message or "quota" in message


def _safe_provider_error(exc: Exception) -> str:
    message = str(exc).replace(os.getenv("OPENAI_API_KEY") or "", "").replace(os.getenv("GEMINI_API_KEY") or "", "")
    return " ".join(message.split())[:500]


def _gemini_grounding_fallback_allowed(exc: Exception) -> bool:
    message = str(exc).lower()
    return (
        "resource_exhausted" in message
        or "quota" in message
        or "rate limit" in message
        or "google_search" in message
        or "google search" in message
    )


def _openai_text_response(prompt: str, *, max_output_tokens: int) -> str:
    if not os.getenv("OPENAI_API_KEY"):
        raise RuntimeError("OPENAI_API_KEY is not configured.")

    client = OpenAI()
    response = client.responses.create(
        model=OPENAI_MODEL,
        input=prompt,
        max_output_tokens=max_output_tokens,
        metadata={"app": "deepdetect-game-platform"},
    )
    return response.output_text.strip()


def _gemini_text_response(prompt: str, *, max_output_tokens: int) -> str:
    if not os.getenv("GEMINI_API_KEY"):
        raise RuntimeError("GEMINI_API_KEY is not configured.")
    if genai is None or types is None:
        raise RuntimeError("google-genai is not installed. Run pip install -r requirements.txt.")

    client = genai.Client(api_key=os.environ["GEMINI_API_KEY"])

    def generate(*, grounded: bool) -> str:
        tools = [types.Tool(google_search=types.GoogleSearch())] if grounded else None
        config = types.GenerateContentConfig(
            thinking_config=types.ThinkingConfig(thinking_level="MINIMAL"),
            tools=tools,
            max_output_tokens=max_output_tokens,
        )
        response = client.models.generate_content(
            model=GEMINI_MODEL,
            contents=prompt,
            config=config,
        )
        return (response.text or "").strip()

    try:
        return generate(grounded=True)
    except Exception as exc:
        if not _gemini_grounding_fallback_allowed(exc):
            raise
        return generate(grounded=False)


def _text_response(prompt: str, *, max_output_tokens: int = 450) -> AgentResult[str]:
    providers = _provider_order()
    if not providers:
        raise RuntimeError("No agent runtime configured. Set OPENAI_API_KEY or GEMINI_API_KEY.")

    fallback_errors: list[str] = []
    for index, provider in enumerate(providers):
        try:
            if provider == "openai":
                text = _openai_text_response(prompt, max_output_tokens=max_output_tokens)
            else:
                text = _gemini_text_response(prompt, max_output_tokens=max_output_tokens)
            return AgentResult(text, provider, _provider_model(provider))
        except Exception as exc:
            can_try_next = index < len(providers) - 1
            if provider == "openai" and configured_provider() == "auto" and can_try_next and _openai_quota_exhausted(exc):
                fallback_errors.append("OpenAI quota/rate limit reached; falling back to Gemini.")
                continue
            if provider == "gemini" and configured_provider() == "auto" and can_try_next:
                fallback_errors.append(f"Gemini request failed; falling back to OpenAI: {_safe_provider_error(exc)}")
                continue
            if fallback_errors:
                raise RuntimeError("; ".join(fallback_errors) + f" Final provider failed: {_safe_provider_error(exc)}") from exc
            raise RuntimeError(f"{provider.title()} agent request failed: {_safe_provider_error(exc)}") from exc

    raise RuntimeError("; ".join(fallback_errors) or "No agent provider returned a response.")


def _json_response(prompt: str, *, max_output_tokens: int = 2200) -> AgentResult[dict[str, Any]]:
    result = _text_response(prompt, max_output_tokens=max_output_tokens)
    text = result.data
    start = text.find("{")
    end = text.rfind("}")
    if start == -1 or end == -1:
        raise ValueError(f"{result.mode} agent did not return JSON: {text[:120]}")
    return AgentResult(json.loads(text[start : end + 1]), result.mode, result.model)


def generate_shift_bundle(articles: list[dict[str, Any]]) -> AgentResult[dict[str, Any]]:
    compact_articles = [
        {
            "title": item.get("title", ""),
            "summary": item.get("summary", ""),
            "source": item.get("source", ""),
            "url": item.get("url", ""),
            "published_at": item.get("published_at", ""),
        }
        for item in articles[:8]
    ]
    prompt = f"""
You are the DeepDetect multi-agent game director. Build a playable media-literacy game shift from recent RSS news.

Return ONLY valid JSON with this exact shape:
{{
  "title": "short shift title",
  "news_items": [
    {{"title":"...", "summary":"...", "source":"...", "url":"...", "published_at":"...", "truth_label":"real|manipulated", "editor_note":"...", "public_pressure":"..."}}
  ],
  "emails": [
    {{"from_name":"...", "from_email":"...", "subject":"...", "body":"...", "linked_news_index":0, "options":[{{"id":"...", "label":"..."}}], "correct_option":"..."}}
  ],
  "telegram_threads": [
    {{"contact":"...", "relationship":"family|friend|source", "messages":["..."], "options":[{{"id":"...", "label":"..."}}], "correct_option":"..."}}
  ],
  "generation_log": ["agent log line"]
}}

Rules:
- Create exactly 6 news_items. At least 3 must be truth_label "real" and at least 2 "manipulated".
- Manipulated items must be plausible misinformation based on the provided article, not random fantasy.
- Create exactly 3 emails and exactly 3 Telegram threads.
- Each email/TG must have 3 options. Correct options reward verification, evidence requests, delay, or refusal to amplify.
- Keep text concise and safe for a classroom game.

Recent articles:
{json.dumps(compact_articles, ensure_ascii=False)}
"""
    return _json_response(prompt)


def generate_real_article(item: dict[str, Any], scraped: dict[str, Any]) -> AgentResult[dict[str, Any]]:
    prompt = f"""
You are the DeepDetect article desk. Create an original in-game article based on real scraped source material.

Return ONLY valid JSON:
{{
  "byline": "short newsroom byline",
  "paragraphs": ["paragraph 1", "paragraph 2"],
  "image_caption": "short caption for the source image"
}}

Rules:
- Write 500-800 words total across 6-10 paragraphs.
- Do not copy the source article verbatim. Rewrite in original newsroom language.
- Stay grounded in the source title, RSS summary, scraped text, source name, and URL.
- Do not invent named victims, exact numbers, quotes, or outcomes that are not supported by the provided material.
- Keep the tone like a real wire article for a media-literacy game.
- The player should have enough detail to decide whether this article should be published.

News item:
{json.dumps({
    "title": item.get("title", ""),
    "summary": item.get("summary", ""),
    "source": item.get("source", ""),
    "url": item.get("url", ""),
    "published_at": item.get("published_at", ""),
    "editor_note": item.get("editor_note", ""),
    "public_pressure": item.get("public_pressure", ""),
}, ensure_ascii=False)}

Scraped source material:
{json.dumps(scraped, ensure_ascii=False)[:16000]}
"""
    result = _json_response(prompt, max_output_tokens=2600)
    data = result.data
    paragraphs = data.get("paragraphs") if isinstance(data.get("paragraphs"), list) else []
    return AgentResult(
        {
            "byline": str(data.get("byline") or "DeepDetect Wire"),
            "paragraphs": [str(p).strip() for p in paragraphs if str(p).strip()],
            "image_caption": str(data.get("image_caption") or ""),
        },
        result.mode,
        result.model,
    )


def generate_synthetic_article(item: dict[str, Any]) -> AgentResult[dict[str, Any]]:
    prompt = f"""
You are the DeepDetect misinformation simulation desk. Create a fictional in-game article for a manipulated news item.

Return ONLY valid JSON:
{{
  "byline": "short newsroom byline",
  "paragraphs": ["paragraph 1", "paragraph 2"],
  "image_prompt": "photorealistic editorial image prompt",
  "image_caption": "short caption"
}}

Rules:
- Write 500-800 words total across 6-10 paragraphs.
- The article must feel like a plausible real article, but it is fictional game content.
- Preserve the item's misinformation premise so rejecting it remains the correct newsroom action.
- Avoid gore, private personal data, harassment, and claims that target protected classes.
- Include subtle verification weaknesses a careful player can notice.
- The image prompt must ask for a safe, non-branded, editorial-style image without visible text, logos, watermarks, or real identifiable people.

News item:
{json.dumps({
    "title": item.get("title", ""),
    "summary": item.get("summary", ""),
    "source": item.get("source", ""),
    "url": item.get("url", ""),
    "published_at": item.get("published_at", ""),
    "editor_note": item.get("editor_note", ""),
    "public_pressure": item.get("public_pressure", ""),
}, ensure_ascii=False)}
"""
    result = _json_response(prompt, max_output_tokens=2800)
    data = result.data
    paragraphs = data.get("paragraphs") if isinstance(data.get("paragraphs"), list) else []
    return AgentResult(
        {
            "byline": str(data.get("byline") or "DeepDetect Wire"),
            "paragraphs": [str(p).strip() for p in paragraphs if str(p).strip()],
            "image_prompt": str(data.get("image_prompt") or ""),
            "image_caption": str(data.get("image_caption") or ""),
        },
        result.mode,
        result.model,
    )


def generate_article_image(prompt: str) -> AgentResult[bytes]:
    if not os.getenv("OPENAI_API_KEY"):
        raise RuntimeError("OPENAI_API_KEY is not configured.")

    client = OpenAI()
    request_args: dict[str, Any] = {
        "model": OPENAI_IMAGE_MODEL,
        "prompt": prompt,
    }
    if OPENAI_IMAGE_MODEL.startswith("dall-e-"):
        request_args["size"] = "1792x1024" if OPENAI_IMAGE_MODEL == "dall-e-3" else "1024x1024"
        if OPENAI_IMAGE_MODEL == "dall-e-3":
            request_args["quality"] = "standard"
    else:
        request_args["size"] = "1536x1024"
        request_args["output_format"] = "png"
        request_args["quality"] = "medium"

    response = client.images.generate(**request_args)
    first_image = response.data[0] if response.data else None
    b64_json = getattr(first_image, "b64_json", None)
    if b64_json:
        return AgentResult(base64.b64decode(b64_json), "openai", OPENAI_IMAGE_MODEL)

    image_url = getattr(first_image, "url", None)
    if image_url:
        download = requests.get(
            image_url,
            timeout=30,
            headers={"User-Agent": "DeepDetectGamePlatform/1.0"},
        )
        download.raise_for_status()
        return AgentResult(download.content, "openai", OPENAI_IMAGE_MODEL)

    if not first_image:
        raise RuntimeError("OpenAI image response did not include image data.")
    raise RuntimeError("OpenAI image response did not include downloadable image data.")


def judge_and_reply(surface: str, participant: str, prompt_text: str, player_answer: str) -> AgentResult[dict[str, Any]]:
    prompt = f"""
You are an in-world DeepDetect simulation agent. Evaluate the player's custom answer and reply as the character.

Return ONLY valid JSON:
{{"correct": true, "response": "one short in-character reply", "reason": "short scoring reason"}}

Surface: {surface}
Character/contact: {participant}
Original prompt: {prompt_text}
Player answer: {player_answer}

Score correct=true when the player slows misinformation, asks for evidence/source verification, archives links, refuses pressure, or explains why not to share/publish yet.
Score correct=false when the player amplifies, publishes, forwards, mocks without helping, ignores risk, or accepts unsupported certainty.
"""
    result = _json_response(prompt, max_output_tokens=500)
    data = result.data
    return AgentResult(
        {
            "correct": bool(data.get("correct")),
            "response": str(data.get("response") or "I need a clearer verification step before moving this forward."),
            "reason": str(data.get("reason") or ""),
        },
        result.mode,
        result.model,
    )


def news_decision_reply(item: dict[str, Any], choice: str, correct: bool) -> AgentResult[dict[str, str]]:
    prompt = f"""
You are the DeepDetect assignment editor. React in-world to a player's newsdesk decision.

Return ONLY valid JSON:
{{"response": "one concise newsroom reply", "reason": "short editorial reason"}}

The player chose: {choice}
The choice was scored as: {"correct" if correct else "risky"}
News item:
{json.dumps({
    "title": item.get("title", ""),
    "summary": item.get("summary", ""),
    "source": item.get("source", ""),
    "url": item.get("url", ""),
    "truth_label": item.get("truth_label", ""),
    "editor_note": item.get("editor_note", ""),
    "public_pressure": item.get("public_pressure", ""),
}, ensure_ascii=False)}

Rules:
- Do not use a canned generic line.
- Mention the actual story or editorial issue.
- If risky, explain the concrete newsroom risk.
- If correct, explain what protection or verification value the decision created.
"""
    result = _json_response(prompt, max_output_tokens=500)
    data = result.data
    response = str(data.get("response") or "").strip()
    if not response:
        raise ValueError("News decision agent did not return a response")
    return AgentResult(
        {
            "response": response,
            "reason": str(data.get("reason") or ""),
        },
        result.mode,
        result.model,
    )


def continue_thread(
    surface: str,
    participant: str,
    item_context: dict[str, Any],
    messages: list[dict[str, Any]],
    player_answer: str,
    turn_number: int,
    min_turns: int,
    max_turns: int,
) -> AgentResult[dict[str, Any]]:
    compact_messages = [
        {
            "sender": item.get("sender", ""),
            "role": item.get("role", ""),
            "text": item.get("text", ""),
        }
        for item in messages[-12:]
        if isinstance(item, dict)
    ]
    safe_context = {
        key: value
        for key, value in item_context.items()
        if key
        in {
            "id",
            "from_name",
            "from_email",
            "subject",
            "body",
            "contact",
            "relationship",
            "linked_news_id",
            "correct_option",
            "options",
        }
    }
    prompt = f"""
You are a DeepDetect in-world conversation agent. Continue a realistic multi-turn chat with the player.

Return ONLY valid JSON:
{{
  "resolved": false,
  "correct": false,
  "response": "one in-character reply",
  "reason": "short evaluation reason",
  "options": [
    {{"id": "short-id", "label": "short suggested reply"}},
    {{"id": "short-id-2", "label": "short suggested reply"}},
    {{"id": "short-id-3", "label": "short suggested reply"}}
  ]
}}

Rules:
- You are the ONLY evaluator for this conversation turn. Judge the actual player text, not a prewritten answer key.
- React directly to the latest player reply. If the player says nonsense, slang, "amogus", "wtf", insults, or anything unclear, the character should be confused, annoyed, or ask for a real actionable answer. Do not pretend the player asked for verification.
- Do not resolve before turn {min_turns}; if turn_number is lower, ask a useful follow-up question.
- Resolve as soon as the player has meaningfully handled verification, evidence, source tracing, and whether to publish/share.
- The conversation must end by turn {max_turns}. If turn_number is {max_turns} or higher, set resolved=true and score correct=true only if the player gave a usable verification/safety action; otherwise resolved=true and correct=false.
- Evaluate the player's newsroom or private-chat action, not whether they actually browsed the web inside the game.
- For email/newsroom threads, correct=true when the player says to hold, delay, reject unsupported wording, attach/archive the source trail, request primary or official confirmation, send to the fact desk, or publish only a verified/corroborated summary.
- For Telegram/private threads, correct=true when the player asks the contact to stop sharing, wait, provide the original source, preserve screenshots for checking, or rely on verified/official reporting.
- correct=true only when the resolved outcome slows or prevents misinformation.
- correct=false when the player is unclear, hostile, jokes, amplifies the claim, or gives no usable verification action.
- If unresolved, response should ask for the next concrete clarification/action.
- Do not ask broad looping questions like "what specifically are you looking for?" after the player already proposed a reasonable verification action. Either ask for one precise missing item, or resolve.
- Keep the character believable for {surface}; do not lecture like a narrator.
- Suggested options must be newly generated for this situation and should fit the character's last reply.

Surface: {surface}
Character/contact: {participant}
Turn number after this player reply: {turn_number}
Minimum turns before resolution: {min_turns}
Maximum turns before forced resolution: {max_turns}
Item context:
{json.dumps(safe_context, ensure_ascii=False)}
Recent thread:
{json.dumps(compact_messages, ensure_ascii=False)}
Latest player reply:
{player_answer}
"""
    result = _json_response(prompt, max_output_tokens=700)
    data = result.data
    if turn_number >= max_turns and not bool(data.get("resolved")):
        repair_prompt = f"""
Your previous DeepDetect conversation JSON violated the max-turn rule by leaving resolved=false at turn {turn_number}/{max_turns}.

Return ONLY corrected valid JSON with the same shape:
{{
  "resolved": true,
  "correct": false,
  "response": "one in-character final reply",
  "reason": "short final scoring reason",
  "options": [
    {{"id": "short-id", "label": "short suggested reply"}},
    {{"id": "short-id-2", "label": "short suggested reply"}},
    {{"id": "short-id-3", "label": "short suggested reply"}}
  ]
}}

Evaluate the player's action, not whether they actually browsed the web inside the game. Decide correct=true if the player gave a usable verification/safety action such as holding publication, seeking official/primary confirmation, attaching the source trail, escalating to the fact desk, stopping forwarding, or waiting for verified reporting. Decide correct=false if the player was vague, hostile, joking, amplifying, or did not provide a concrete safe action.

Surface: {surface}
Character/contact: {participant}
Item context: {json.dumps(safe_context, ensure_ascii=False)}
Recent thread: {json.dumps(compact_messages, ensure_ascii=False)}
Latest player reply: {player_answer}
Previous invalid JSON: {json.dumps(data, ensure_ascii=False)}
"""
        result = _json_response(repair_prompt, max_output_tokens=700)
        data = result.data
    options = data.get("options") if isinstance(data.get("options"), list) else []
    response = str(data.get("response") or "").strip()
    if not response:
        raise ValueError("Conversation agent did not return a response")
    if turn_number >= max_turns and not bool(data.get("resolved")):
        raise ValueError("Conversation agent did not resolve at the configured max turn")
    if len(options) < 3:
        raise ValueError("Conversation agent did not return three suggested options")
    return AgentResult(
        {
            "resolved": bool(data.get("resolved")),
            "correct": bool(data.get("correct")),
            "response": response,
            "reason": str(data.get("reason") or ""),
            "options": options[:3],
        },
        result.mode,
        result.model,
    )


def world_event(state: dict[str, Any], articles: list[dict[str, Any]]) -> AgentResult[dict[str, Any]]:
    prompt = f"""
You are WorldDirector for DeepDetect. Continue the live simulation by creating one new event.

Return ONLY valid JSON with one of these shapes:
{{"kind":"news", "log":"...", "item":{{"title":"...", "summary":"...", "source":"...", "url":"...", "published_at":"...", "truth_label":"real|manipulated", "editor_note":"...", "public_pressure":"..."}}}}
OR
{{"kind":"email", "log":"...", "item":{{"from_name":"...", "from_email":"...", "subject":"...", "body":"...", "options":[{{"id":"...", "label":"..."}}], "correct_option":"..."}}}}
OR
{{"kind":"telegram", "log":"...", "item":{{"contact":"...", "relationship":"family|friend|source", "messages":["..."], "options":[{{"id":"...", "label":"..."}}], "correct_option":"..."}}}}

Make the event feel new, connected to an active newsroom shift, and useful for media-literacy play. Keep text concise.
Current world tick: {state.get("world_tick", 0)}
Existing score: {state.get("score", 0)}
Recent articles: {json.dumps(articles[:6], ensure_ascii=False)}
"""
    return _json_response(prompt, max_output_tokens=1200)
