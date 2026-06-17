from __future__ import annotations

import json
import os
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Generic, TypeVar

from dotenv import load_dotenv
from openai import APIStatusError, OpenAI, RateLimitError

try:
    from google import genai
    from google.genai import types
except ImportError:  # pragma: no cover - exercised only before dependencies are installed
    genai = None
    types = None

load_dotenv(Path(__file__).resolve().parents[2] / ".env")

OPENAI_MODEL = os.getenv("OPENAI_MODEL_AGENT", "gpt-5.3-chat-latest")
GEMINI_MODEL = os.getenv("GEMINI_MODEL_AGENT", "gemini-3.1-flash-lite")
MODEL = OPENAI_MODEL
DEFAULT_PROVIDER = "auto"

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
    if os.getenv("OPENAI_API_KEY"):
        providers.append("openai")
    if os.getenv("GEMINI_API_KEY"):
        providers.append("gemini")
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
    contents = [
        types.Content(
            role="user",
            parts=[types.Part.from_text(text=prompt)],
        )
    ]
    config = types.GenerateContentConfig(
        thinking_config=types.ThinkingConfig(thinking_level="MINIMAL"),
        tools=[types.Tool(google_search=types.GoogleSearch())],
        max_output_tokens=max_output_tokens,
    )

    chunks: list[str] = []
    for chunk in client.models.generate_content_stream(
        model=GEMINI_MODEL,
        contents=contents,
        config=config,
    ):
        if text := chunk.text:
            chunks.append(text)
    return "".join(chunks).strip()


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
