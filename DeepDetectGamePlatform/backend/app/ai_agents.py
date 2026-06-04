from __future__ import annotations

import json
import os
from typing import Any

from openai import OpenAI

MODEL = os.getenv("OPENAI_MODEL_AGENT", "gpt-4o-mini")


def enabled() -> bool:
    return bool(os.getenv("OPENAI_API_KEY"))


def _text_response(prompt: str, *, max_output_tokens: int = 450) -> str:
    client = OpenAI()
    response = client.responses.create(
        model=MODEL,
        input=prompt,
        max_output_tokens=max_output_tokens,
        metadata={"app": "deepdetect-game-platform"},
    )
    return response.output_text.strip()


def _json_response(prompt: str, *, max_output_tokens: int = 2200) -> dict[str, Any]:
    text = _text_response(prompt, max_output_tokens=max_output_tokens)
    start = text.find("{")
    end = text.rfind("}")
    if start == -1 or end == -1:
        raise ValueError(f"Agent did not return JSON: {text[:120]}")
    return json.loads(text[start : end + 1])


def generate_shift_bundle(articles: list[dict[str, Any]]) -> dict[str, Any]:
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


def judge_and_reply(surface: str, participant: str, prompt_text: str, player_answer: str) -> dict[str, Any]:
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
    data = _json_response(prompt, max_output_tokens=500)
    return {
        "correct": bool(data.get("correct")),
        "response": str(data.get("response") or "I need a clearer verification step before moving this forward."),
        "reason": str(data.get("reason") or ""),
    }


def continue_thread(surface: str, participant: str, messages: list[dict[str, Any]], player_answer: str, turn_number: int, min_turns: int) -> dict[str, Any]:
    compact_messages = [
        {
            "sender": item.get("sender", ""),
            "role": item.get("role", ""),
            "text": item.get("text", ""),
        }
        for item in messages[-12:]
        if isinstance(item, dict)
    ]
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
- Do not resolve before turn {min_turns}; if turn_number is lower, ask a useful follow-up question.
- Resolve only when the player has meaningfully handled verification, evidence, source tracing, and whether to publish/share.
- correct=true only when the resolved outcome slows or prevents misinformation.
- If unresolved, response should ask for the next concrete clarification/action.
- Keep the character believable for {surface}; do not lecture like a narrator.

Surface: {surface}
Character/contact: {participant}
Turn number after this player reply: {turn_number}
Minimum turns before resolution: {min_turns}
Recent thread:
{json.dumps(compact_messages, ensure_ascii=False)}
Latest player reply:
{player_answer}
"""
    data = _json_response(prompt, max_output_tokens=700)
    options = data.get("options") if isinstance(data.get("options"), list) else []
    return {
        "resolved": bool(data.get("resolved")),
        "correct": bool(data.get("correct")),
        "response": str(data.get("response") or "What evidence should we check before deciding?"),
        "reason": str(data.get("reason") or ""),
        "options": options[:3],
    }


def world_event(state: dict[str, Any], articles: list[dict[str, Any]]) -> dict[str, Any]:
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
