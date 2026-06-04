from __future__ import annotations

import json
import os
from typing import Any

from openai import OpenAI

MODEL = os.getenv("OPENAI_MODEL_AGENT", "gpt-5.3-chat-latest")


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


def news_decision_reply(item: dict[str, Any], choice: str, correct: bool) -> dict[str, str]:
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
    data = _json_response(prompt, max_output_tokens=500)
    response = str(data.get("response") or "").strip()
    if not response:
        raise ValueError("News decision agent did not return a response")
    return {
        "response": response,
        "reason": str(data.get("reason") or ""),
    }


def continue_thread(
    surface: str,
    participant: str,
    item_context: dict[str, Any],
    messages: list[dict[str, Any]],
    player_answer: str,
    turn_number: int,
    min_turns: int,
    max_turns: int,
) -> dict[str, Any]:
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
    data = _json_response(prompt, max_output_tokens=700)
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
        data = _json_response(repair_prompt, max_output_tokens=700)
    options = data.get("options") if isinstance(data.get("options"), list) else []
    response = str(data.get("response") or "").strip()
    if not response:
        raise ValueError("Conversation agent did not return a response")
    if turn_number >= max_turns and not bool(data.get("resolved")):
        raise ValueError("Conversation agent did not resolve at the configured max turn")
    if len(options) < 3:
        raise ValueError("Conversation agent did not return three suggested options")
    return {
        "resolved": bool(data.get("resolved")),
        "correct": bool(data.get("correct")),
        "response": response,
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
