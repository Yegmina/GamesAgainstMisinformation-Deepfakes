from __future__ import annotations

import random
import re
import uuid
from datetime import datetime, timezone
from typing import Any

import feedparser
import requests

from . import ai_agents

RSS_FEEDS = [
    ("YLE News", "https://feeds.yle.fi/uutiset/v1/recent.rss?publisherIds=YLE_NEWS"),
    ("BBC World", "https://feeds.bbci.co.uk/news/world/rss.xml"),
    ("NPR", "https://feeds.npr.org/1001/rss.xml"),
]

FALLBACK_NEWS = [
    {
        "title": "City council reviews new rules for AI-generated political adverts",
        "summary": "Officials are weighing disclosure labels and penalties before the next election cycle.",
        "source": "Civic Wire",
        "url": "https://example.test/ai-adverts",
    },
    {
        "title": "University researchers publish guide for spotting synthetic video edits",
        "summary": "The guide highlights lighting, lip-sync, source history, and reverse-image checks.",
        "source": "Science Desk",
        "url": "https://example.test/synthetic-video-guide",
    },
    {
        "title": "Public transport agency warns about fake refund messages",
        "summary": "The agency says it never asks passengers to enter bank credentials through text links.",
        "source": "Metro Daily",
        "url": "https://example.test/refund-warning",
    },
    {
        "title": "Health ministry expands media-literacy lessons for secondary schools",
        "summary": "The new material focuses on emotional manipulation and verification habits.",
        "source": "Education Today",
        "url": "https://example.test/media-literacy-lessons",
    },
]


def _clean(text: str) -> str:
    text = re.sub(r"<[^>]+>", "", text or "")
    return re.sub(r"\s+", " ", text).strip()


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


def message(sender: str, text: str, role: str = "agent") -> dict[str, str]:
    return {"sender": sender, "text": text, "role": role, "at": now_iso()}


def option_label(item: dict[str, Any], choice: str) -> str:
    match = next((option for option in item.get("options", []) if option["id"] == choice), None)
    return match["label"] if match else choice


def custom_reply_is_responsible(text: str) -> bool:
    lowered = text.lower()
    good = [
        "verify",
        "verified",
        "source",
        "evidence",
        "wait",
        "check",
        "confirm",
        "corroborate",
        "credible",
        "archive",
        "not publish",
        "hold",
        "do not share",
        "don't share",
        "not forward",
    ]
    risky = ["publish now", "share now", "viral", "ignore", "sounds true", "send it", "no need"]
    return any(word in lowered for word in good) and not any(word in lowered for word in risky)


def agent_followup(surface: str, item: dict[str, Any], correct: bool) -> str:
    if surface == "email":
        if correct:
            return "Good call. I am logging the source trail and holding the story until the evidence is clean."
        return "I am worried about that response. The claim is still not verified, so I am escalating it to the fact desk."
    if surface == "telegram":
        contact = item.get("contact", "They")
        if correct:
            return f"{contact}: Okay, I will not forward it yet. Send me the checked version when you have it."
        return f"{contact}: That still sounds like I should act fast. I might send it unless you give me a clearer reason."
    if correct:
        return "Assignment Editor: Decision logged. The queue has been updated and the desk agrees with your reasoning."
    return "Assignment Editor: Decision logged, but the desk flags this as a risky editorial call."


def ensure_thread_state(item: dict[str, Any]) -> None:
    item.setdefault("chat_turns", 0)
    item.setdefault("min_turns", 3)
    item.setdefault("resolved", bool(item.get("selected")))
    item.setdefault("agent_response", "")
    item.setdefault("agent_reason", "")


def new_thread_state() -> dict[str, Any]:
    return {
        "agent_response": "",
        "agent_reason": "",
        "chat_turns": 0,
        "min_turns": 3,
        "resolved": False,
        "correct": None,
    }


def initial_values() -> dict[str, dict[str, Any]]:
    return {
        "public_trust": {
            "label": "Public trust",
            "value": 50,
            "description": "Audience confidence in the desk.",
        },
        "editorial_integrity": {
            "label": "Editorial integrity",
            "value": 50,
            "description": "How well you resist pressure and protect evidence.",
        },
        "community_care": {
            "label": "Community care",
            "value": 50,
            "description": "Whether private conversations reduce harm.",
        },
        "newsroom_momentum": {
            "label": "Newsroom momentum",
            "value": 50,
            "description": "The desk's ability to move without panic.",
        },
    }


def initial_quests() -> list[dict[str, Any]]:
    return [
        {
            "id": "homepage-guardian",
            "type": "main",
            "title": "Homepage Guardian",
            "description": "Make four correct publish/reject calls before the shift ends.",
            "target": 4,
            "current": 0,
            "reward": "+120 trust bonus",
            "complete": False,
            "claimed": False,
        },
        {
            "id": "source-chain",
            "type": "side",
            "title": "Source Chain",
            "description": "Resolve two inbox threads with evidence-first answers.",
            "target": 2,
            "current": 0,
            "reward": "+60 integrity bonus",
            "complete": False,
            "claimed": False,
        },
        {
            "id": "social-firebreak",
            "type": "side",
            "title": "Social Firebreak",
            "description": "Slow down two private-message rumors without escalating.",
            "target": 2,
            "current": 0,
            "reward": "+60 community bonus",
            "complete": False,
            "claimed": False,
        },
        {
            "id": "balanced-desk",
            "type": "value",
            "title": "Balanced Desk",
            "description": "Raise all newsroom values to 55 or higher.",
            "target": 4,
            "current": 0,
            "reward": "+40 stability bonus",
            "complete": False,
            "claimed": False,
        },
    ]


def ensure_game_systems(state: dict[str, Any]) -> None:
    state.setdefault("values", initial_values())
    for key, default in initial_values().items():
        state["values"].setdefault(key, default)
        state["values"][key].setdefault("label", default["label"])
        state["values"][key].setdefault("description", default["description"])
        state["values"][key]["value"] = max(0, min(100, int(state["values"][key].get("value", default["value"]))))
    state.setdefault("quests", initial_quests())
    state.setdefault("quest_log", [])


def add_value(state: dict[str, Any], key: str, amount: int) -> None:
    ensure_game_systems(state)
    if key not in state["values"]:
        return
    current = int(state["values"][key].get("value", 50))
    state["values"][key]["value"] = max(0, min(100, current + amount))


def update_quests(state: dict[str, Any]) -> None:
    ensure_game_systems(state)
    news_correct = sum(1 for item in state["news_items"] if item.get("correct") is True)
    email_correct = sum(1 for item in state["emails"] if item.get("selected") and item.get("correct") is True)
    tg_correct = sum(1 for item in state["telegram_threads"] if item.get("selected") and item.get("correct") is True)
    balanced_values = sum(1 for value in state["values"].values() if int(value.get("value", 0)) >= 55)
    progress = {
        "homepage-guardian": news_correct,
        "source-chain": email_correct,
        "social-firebreak": tg_correct,
        "balanced-desk": balanced_values,
    }
    rewards = {
        "homepage-guardian": 120,
        "source-chain": 60,
        "social-firebreak": 60,
        "balanced-desk": 40,
    }
    for quest in state["quests"]:
        quest["current"] = min(progress.get(quest["id"], 0), int(quest["target"]))
        was_complete = bool(quest.get("complete"))
        quest["complete"] = quest["current"] >= int(quest["target"])
        if quest["complete"] and not was_complete and not quest.get("claimed"):
            bonus = rewards.get(quest["id"], 0)
            state["score"] += bonus
            quest["claimed"] = True
            state["quest_log"].insert(0, f"Quest complete: {quest['title']} ({quest['reward']})")
            state["action_log"].append(f"Quest complete: {quest['title']} (+{bonus})")


def local_continue_thread(surface: str, item: dict[str, Any], answer_text: str) -> dict[str, Any]:
    turns = int(item.get("chat_turns", 0))
    correct = custom_reply_is_responsible(answer_text)
    if turns < int(item.get("min_turns", 3)):
        if surface == "email":
            response = "Before I move this, tell me which source or evidence trail you want attached to the decision."
        else:
            response = f"{item.get('contact', 'They')}: What should I check first so I do not accidentally spread something false?"
        return {
            "resolved": False,
            "correct": False,
            "response": response,
            "reason": "local follow-up before minimum conversation depth",
            "options": item.get("options", []),
            "mode": "local",
        }
    return {
        "resolved": True,
        "correct": correct,
        "response": agent_followup(surface, item, correct),
        "reason": "local final evaluation after multi-turn thread",
        "options": item.get("options", []),
        "mode": "local",
    }


def continue_conversation(surface: str, item: dict[str, Any], answer_text: str) -> dict[str, Any]:
    ensure_thread_state(item)
    item["chat_turns"] = int(item.get("chat_turns", 0)) + 1
    turn_number = int(item["chat_turns"])
    min_turns = int(item.get("min_turns", 3))
    if ai_agents.enabled():
        try:
            result = ai_agents.continue_thread(
                surface,
                item.get("from_name") or item.get("contact") or "Agent",
                item.get("messages", []),
                answer_text,
                turn_number,
                min_turns,
            )
            if turn_number < min_turns:
                result["resolved"] = False
                result["correct"] = False
            elif not result.get("resolved") and custom_reply_is_responsible(answer_text):
                result["resolved"] = True
                result["correct"] = True
                result["reason"] = f"{result.get('reason', '')} GameDirector resolved after required multi-turn verification depth.".strip()
            elif turn_number >= min_turns + 2:
                result["resolved"] = True
                result["correct"] = custom_reply_is_responsible(answer_text)
                result["reason"] = f"{result.get('reason', '')} GameDirector forced closure after extended thread depth.".strip()
            result["mode"] = "openai"
            return result
        except Exception as exc:
            result = local_continue_thread(surface, item, answer_text)
            result["reason"] = f"{result['reason']} after {type(exc).__name__}"
            return result
    return local_continue_thread(surface, item, answer_text)


def fetch_recent_news(limit: int = 8) -> list[dict[str, Any]]:
    articles: list[dict[str, Any]] = []
    for source, url in RSS_FEEDS:
        try:
            response = requests.get(url, timeout=8, headers={"User-Agent": "DeepDetectGamePlatform/1.0"})
            response.raise_for_status()
            feed = feedparser.parse(response.content)
        except Exception:
            continue
        for entry in feed.entries[:5]:
            title = _clean(getattr(entry, "title", ""))
            if not title:
                continue
            summary = _clean(getattr(entry, "summary", "")) or "A breaking story is being reviewed by the newsroom."
            articles.append(
                {
                    "title": title,
                    "summary": summary[:420],
                    "source": source,
                    "url": getattr(entry, "link", url),
                    "published_at": getattr(entry, "published", ""),
                }
            )
            if len(articles) >= limit:
                return articles
    return FALLBACK_NEWS[:limit]


def distort_article(article: dict[str, Any], rng: random.Random) -> dict[str, Any]:
    hooks = [
        "Leaked documents prove",
        "Experts are hiding that",
        "Viral footage confirms",
        "Anonymous insiders claim",
    ]
    pressure = [
        "competitors are already posting it",
        "the morning editor wants a fast decision",
        "a sponsor is asking why it is not live yet",
        "the story is trending in private chats",
    ]
    title = article["title"]
    distorted_title = f"{rng.choice(hooks)}: {title}"
    return {
        **article,
        "title": distorted_title,
        "summary": (
            f"{article['summary']} The version in the queue adds unsupported certainty, emotional wording, "
            "and a missing-source claim that has not been verified."
        ),
        "truth_label": "manipulated",
        "editor_note": "Manipulated framing: verify the original source before publishing.",
        "public_pressure": rng.choice(pressure),
    }


def build_newsdesk(articles: list[dict[str, Any]], rng: random.Random) -> list[dict[str, Any]]:
    items: list[dict[str, Any]] = []
    for index, article in enumerate(articles[:6]):
        is_manipulated = index % 2 == 1
        item = distort_article(article, rng) if is_manipulated else {
            **article,
            "truth_label": "real",
            "editor_note": "Real source found. Still check relevance and wording before publishing.",
            "public_pressure": rng.choice([
                "low pressure; enough time to verify",
                "homepage slot opens in ten minutes",
                "audience team says the topic is rising",
            ]),
        }
        item.update(
            {
                "id": f"news-{index + 1}",
                "decision": None,
                "correct": None,
                "points": 0,
            }
        )
        items.append(item)
    return items


def hydrate_news_items(items: list[dict[str, Any]], rng: random.Random) -> list[dict[str, Any]]:
    hydrated: list[dict[str, Any]] = []
    for index, raw in enumerate(items[:6]):
        truth_label = raw.get("truth_label") if raw.get("truth_label") in {"real", "manipulated"} else ("real" if index % 2 == 0 else "manipulated")
        item = {
            "title": raw.get("title") or "Untitled wire item",
            "summary": raw.get("summary") or "A developing story needs editorial review.",
            "source": raw.get("source") or "Live Desk",
            "url": raw.get("url") or "",
            "published_at": raw.get("published_at") or "",
            "truth_label": truth_label,
            "editor_note": raw.get("editor_note") or ("Verify source and framing before publication." if truth_label == "real" else "Manipulated framing: verify before publishing."),
            "public_pressure": raw.get("public_pressure") or rng.choice(["homepage slot opens soon", "audience team says the topic is rising", "source wants a fast decision"]),
            "id": f"news-{index + 1}",
            "decision": None,
            "correct": None,
            "points": 0,
            "agent_generated": bool(raw.get("agent_generated", True)),
        }
        hydrated.append(item)
    return hydrated


def hydrate_emails(items: list[dict[str, Any]], news_items: list[dict[str, Any]]) -> list[dict[str, Any]]:
    emails: list[dict[str, Any]] = []
    for index, raw in enumerate(items[:3]):
        from_name = raw.get("from_name") or "Live Desk"
        body = raw.get("body") or "Can you verify this before it moves?"
        options = raw.get("options") if isinstance(raw.get("options"), list) else []
        if len(options) < 3:
            options = [
                {"id": "verify-first", "label": "Verify first and hold publication"},
                {"id": "publish-fast", "label": "Publish fast while traffic is high"},
                {"id": "ask-source", "label": "Ask for source documents"},
            ]
        emails.append(
            {
                "id": f"email-agent-{index + 1}",
                "from_name": from_name,
                "from_email": raw.get("from_email") or "agent@newmedia.local",
                "subject": raw.get("subject") or "Verification needed",
                "body": body,
                "messages": [message(from_name, body)],
                "linked_news_id": news_items[min(int(raw.get("linked_news_index") or 0), len(news_items) - 1)]["id"] if news_items else None,
                "options": options[:3],
                "correct_option": raw.get("correct_option") or options[0]["id"],
                "selected": None,
                "custom_answer": "",
                **new_thread_state(),
                "agent_generated": True,
            }
        )
    return emails


def hydrate_telegram(items: list[dict[str, Any]]) -> list[dict[str, Any]]:
    threads: list[dict[str, Any]] = []
    for index, raw in enumerate(items[:3]):
        contact = raw.get("contact") or f"Contact {index + 1}"
        raw_messages = raw.get("messages") if isinstance(raw.get("messages"), list) else ["Is this real?"]
        options = raw.get("options") if isinstance(raw.get("options"), list) else []
        if len(options) < 3:
            options = [
                {"id": "ask-wait", "label": "Ask them to wait while you verify"},
                {"id": "share-fast", "label": "Tell them to forward it"},
                {"id": "request-evidence", "label": "Request source and screenshots"},
            ]
        threads.append(
            {
                "id": f"tg-agent-{index + 1}",
                "contact": contact,
                "relationship": raw.get("relationship") or "friend",
                "messages": [message(contact, str(text)) for text in raw_messages[:4]],
                "options": options[:3],
                "correct_option": raw.get("correct_option") or options[0]["id"],
                "selected": None,
                "custom_answer": "",
                **new_thread_state(),
                "agent_generated": True,
            }
        )
    return threads


def build_emails(news_items: list[dict[str, Any]], rng: random.Random) -> list[dict[str, Any]]:
    real_item = next((item for item in news_items if item["truth_label"] == "real"), news_items[0])
    fake_item = next((item for item in news_items if item["truth_label"] == "manipulated"), news_items[-1])
    emails = [
        {
            "id": "email-source-check",
            "from_name": "Mira / Senior Editor",
            "from_email": "mira@newmedia.local",
            "subject": "Need source check before homepage",
            "body": f"Can you verify whether this story is safe to publish? It references: {real_item['title']}",
            "messages": [
                message("Mira / Senior Editor", f"Can you verify whether this story is safe to publish? It references: {real_item['title']}")
            ],
            "linked_news_id": real_item["id"],
            "options": [
                {"id": "send-source-pack", "label": "Send source pack and mark verified"},
                {"id": "publish-without-check", "label": "Tell her to publish immediately"},
                {"id": "ignore", "label": "Ignore until after lunch"},
            ],
            "correct_option": "send-source-pack",
            "selected": None,
            "custom_answer": "",
            **new_thread_state(),
        },
        {
            "id": "email-sponsor-pressure",
            "from_name": "Brand Partner",
            "from_email": "promo@brand-partner.example",
            "subject": "Small wording request for today",
            "body": f"Please remove uncertainty from this headline and make it sound confirmed: {fake_item['title']}",
            "messages": [
                message("Brand Partner", f"Please remove uncertainty from this headline and make it sound confirmed: {fake_item['title']}")
            ],
            "linked_news_id": fake_item["id"],
            "options": [
                {"id": "push-back", "label": "Push back and request evidence"},
                {"id": "accept-pressure", "label": "Accept the wording request"},
                {"id": "forward-family", "label": "Forward it to a friend for vibes"},
            ],
            "correct_option": "push-back",
            "selected": None,
            "custom_answer": "",
            **new_thread_state(),
        },
    ]
    rng.shuffle(emails)
    return emails


def build_telegram(news_items: list[dict[str, Any]], rng: random.Random) -> list[dict[str, Any]]:
    suspicious = next((item for item in news_items if item["truth_label"] == "manipulated"), news_items[-1])
    real_item = next((item for item in news_items if item["truth_label"] == "real"), news_items[0])
    return [
        {
            "id": "tg-mom-rumor",
            "contact": "Mom",
            "relationship": "family",
            "messages": [
                message("Mom", "Hey, this scary headline is everywhere in my group chat."),
                message("Mom", suspicious["title"]),
                message("Mom", "Should I share it so people are warned?"),
            ],
            "options": [
                {"id": "ask-wait", "label": "Ask her to wait while you verify"},
                {"id": "share-fast", "label": "Tell her to share quickly"},
                {"id": "mock", "label": "Mock the group chat"},
            ],
            "correct_option": "ask-wait",
            "selected": None,
            "custom_answer": "",
            **new_thread_state(),
        },
        {
            "id": "tg-friend-tip",
            "contact": "Leo",
            "relationship": "friend",
            "messages": [
                message("Leo", "I found the original source for a story you are editing."),
                message("Leo", real_item["url"]),
                message("Leo", "Want me to send screenshots too?"),
            ],
            "options": [
                {"id": "request-evidence", "label": "Request screenshots and archive the link"},
                {"id": "dismiss", "label": "Dismiss it as boring"},
                {"id": "publish-credit", "label": "Publish and credit Leo as official source"},
            ],
            "correct_option": "request-evidence",
            "selected": None,
            "custom_answer": "",
            **new_thread_state(),
        },
    ]


def update_goals(state: dict[str, Any]) -> None:
    ensure_game_systems(state)
    news_done = sum(1 for item in state["news_items"] if item["decision"])
    news_correct = sum(1 for item in state["news_items"] if item["correct"] is True)
    email_done = sum(1 for item in state["emails"] if item["selected"])
    tg_done = sum(1 for item in state["telegram_threads"] if item["selected"])
    for goal in state["goals"]:
        if goal["id"] == "newsdesk":
            goal["current"] = min(news_correct, goal["target"])
            goal["complete"] = news_correct >= goal["target"]
        elif goal["id"] == "inbox":
            goal["current"] = email_done
            goal["complete"] = email_done >= goal["target"]
        elif goal["id"] == "sidequests":
            goal["current"] = tg_done
            goal["complete"] = tg_done >= goal["target"]
    state["complete"] = news_done == len(state["news_items"]) and email_done == len(state["emails"]) and tg_done == len(state["telegram_threads"])
    update_quests(state)


def generate_game(user: dict[str, Any]) -> dict[str, Any]:
    rng = random.Random(f"{user['id']}-{datetime.now(timezone.utc).isoformat()}")
    generation_log = [
        "NewsScoutAgent: collecting recent public RSS headlines",
    ]
    articles = fetch_recent_news()
    generation_log.append(f"NewsScoutAgent: prepared {len(articles)} source stories")
    agent_mode = "local"
    agent_error = ""
    title = "Morning Shift: False Signal"
    try:
        if ai_agents.enabled():
            bundle = ai_agents.generate_shift_bundle(articles)
            title = str(bundle.get("title") or title)
            news_items = hydrate_news_items(bundle.get("news_items") or [], rng)
            emails = hydrate_emails(bundle.get("emails") or [], news_items)
            telegram = hydrate_telegram(bundle.get("telegram_threads") or [])
            generation_log.extend(str(line) for line in (bundle.get("generation_log") or []))
            generation_log.append("OpenAIAgentRuntime: live OpenAI generation completed")
            agent_mode = "openai"
        else:
            raise RuntimeError("OPENAI_API_KEY not available")
    except Exception as exc:
        agent_error = f"{type(exc).__name__}: {str(exc)[:160]}"
        news_items = build_newsdesk(articles, rng)
        generation_log.append("DistortionAgent: created manipulated variants and verification notes")
        emails = build_emails(news_items, rng)
        generation_log.append("InboxAgent: generated newsroom email pressure and source-check tasks")
        telegram = build_telegram(news_items, rng)
        generation_log.append("SideQuestAgent: generated family/friend misinformation sidequests")
        generation_log.append(f"OpenAIAgentRuntime: fallback used ({agent_error})")
    generation_log.append("MissionDirector: packaged playable shift goals and scoring")
    state = {
        "id": str(uuid.uuid4()),
        "title": title,
        "player": {"name": user["name"], "role": "New Media Editor"},
        "created_at": datetime.now(timezone.utc).isoformat(),
        "agent_mode": agent_mode,
        "agent_model": ai_agents.MODEL if agent_mode == "openai" else "local-fallback",
        "agent_error": agent_error,
        "score": 0,
        "complete": False,
        "values": initial_values(),
        "quests": initial_quests(),
        "quest_log": [],
        "generation_log": generation_log,
        "world_tick": 0,
        "world_feed": ["WorldDirector: shift started; newsroom, inbox, and private chats are live."],
        "goals": [
            {"id": "newsdesk", "title": "Make at least 4 correct publish/reject calls", "target": 4, "current": 0, "complete": False},
            {"id": "inbox", "title": "Resolve 2 newsroom emails", "target": 2, "current": 0, "complete": False},
            {"id": "sidequests", "title": "Answer 2 personal sidequests responsibly", "target": 2, "current": 0, "complete": False},
        ],
        "news_items": news_items,
        "emails": emails,
        "telegram_threads": telegram,
        "action_log": [],
    }
    return state


def apply_action(state: dict[str, Any], surface: str, item_id: str, choice: str, custom_text: str | None = None) -> dict[str, Any]:
    ensure_game_systems(state)
    if surface == "news":
        item = next((entry for entry in state["news_items"] if entry["id"] == item_id), None)
        if not item:
            raise ValueError("News item not found")
        if item["decision"]:
            return state
        correct_choice = "publish" if item["truth_label"] == "real" else "reject"
        item["decision"] = choice
        item["correct"] = choice == correct_choice
        item["points"] = 100 if item["correct"] else -50
        item["agent_response"] = agent_followup("news", item, item["correct"])
        state["score"] += item["points"]
        if item["correct"]:
            add_value(state, "public_trust", 6)
            add_value(state, "editorial_integrity", 4)
            add_value(state, "newsroom_momentum", 3)
            if choice == "reject":
                add_value(state, "community_care", 2)
        else:
            add_value(state, "public_trust", -10)
            add_value(state, "editorial_integrity", -8)
            add_value(state, "newsroom_momentum", -6)
            if choice == "publish":
                add_value(state, "community_care", -5)
        state["action_log"].append(f"Newsdesk: {choice} {item['title']} ({'correct' if item['correct'] else 'wrong'})")
    elif surface == "email":
        item = next((entry for entry in state["emails"] if entry["id"] == item_id), None)
        if not item:
            raise ValueError("Email not found")
        if item["selected"]:
            return state
        ensure_thread_state(item)
        answer_text = custom_text.strip() if custom_text and choice == "__custom__" else option_label(item, choice)
        item.setdefault("messages", [message(item["from_name"], item["body"])])
        item["messages"].append(message("You", answer_text, "player"))
        result = continue_conversation("email", item, answer_text)
        item["agent_response"] = result["response"]
        item["agent_reason"] = result["reason"]
        item["reply_agent_mode"] = result["mode"]
        if result.get("options"):
            item["options"] = result["options"]
        item["messages"].append(message(item["from_name"], item["agent_response"]))
        if result["resolved"]:
            item["resolved"] = True
            item["selected"] = choice
            item["custom_answer"] = answer_text if choice == "__custom__" else ""
            item["correct"] = result["correct"]
            state["score"] += 75 if item["correct"] else -35
            if item["correct"]:
                add_value(state, "editorial_integrity", 6)
                add_value(state, "public_trust", 3)
                add_value(state, "newsroom_momentum", 2)
            else:
                add_value(state, "editorial_integrity", -7)
                add_value(state, "public_trust", -4)
                add_value(state, "newsroom_momentum", -3)
            state["action_log"].append(f"Inbox resolved after {item['chat_turns']} turns: {item['subject']} ({'correct' if item['correct'] else 'wrong'})")
    elif surface == "telegram":
        item = next((entry for entry in state["telegram_threads"] if entry["id"] == item_id), None)
        if not item:
            raise ValueError("Thread not found")
        if item["selected"]:
            return state
        ensure_thread_state(item)
        answer_text = custom_text.strip() if custom_text and choice == "__custom__" else option_label(item, choice)
        item["messages"].append(message("You", answer_text, "player"))
        result = continue_conversation("telegram", item, answer_text)
        item["agent_response"] = result["response"]
        item["agent_reason"] = result["reason"]
        item["reply_agent_mode"] = result["mode"]
        if result.get("options"):
            item["options"] = result["options"]
        item["messages"].append(message(item["contact"], item["agent_response"]))
        if result["resolved"]:
            item["resolved"] = True
            item["selected"] = choice
            item["custom_answer"] = answer_text if choice == "__custom__" else ""
            item["correct"] = result["correct"]
            state["score"] += 75 if item["correct"] else -35
            if item["correct"]:
                add_value(state, "community_care", 7)
                add_value(state, "public_trust", 2)
                add_value(state, "editorial_integrity", 2)
            else:
                add_value(state, "community_care", -8)
                add_value(state, "public_trust", -4)
                add_value(state, "editorial_integrity", -3)
            state["action_log"].append(f"Telegram resolved after {item['chat_turns']} turns with {item['contact']} ({'correct' if item['correct'] else 'wrong'})")
    else:
        raise ValueError("Unknown action surface")
    update_goals(state)
    return state


def advance_world(state: dict[str, Any]) -> dict[str, Any]:
    ensure_game_systems(state)
    tick = int(state.get("world_tick", 0)) + 1
    state["world_tick"] = tick
    add_value(state, "newsroom_momentum", -1)
    rng = random.Random(f"{state['id']}-{tick}")
    state.setdefault("world_feed", [])
    state.setdefault("generation_log", [])
    articles = fetch_recent_news(limit=8)
    article = articles[tick % len(articles)] if articles else FALLBACK_NEWS[tick % len(FALLBACK_NEWS)]

    try:
        if ai_agents.enabled():
            event = ai_agents.world_event(state, articles)
            kind = event.get("kind")
            raw_item = event.get("item") or {}
            if kind == "news":
                item = hydrate_news_items([{**raw_item, "agent_generated": True}], rng)[0]
                item["id"] = f"news-live-{tick}"
                state["news_items"].insert(0, item)
            elif kind == "email":
                item = hydrate_emails([raw_item], state.get("news_items") or [])[0]
                item["id"] = f"email-live-{tick}"
                state["emails"].insert(0, item)
            elif kind == "telegram":
                item = hydrate_telegram([raw_item])[0]
                item["id"] = f"tg-live-{tick}"
                state["telegram_threads"].insert(0, item)
            else:
                raise ValueError(f"Unknown world event kind: {kind}")
            line = str(event.get("log") or f"OpenAIWorldDirector: added {kind} event")
            state["last_world_agent_mode"] = "openai"
        else:
            raise RuntimeError("OPENAI_API_KEY not available")
    except Exception as exc:
        line = ""
        state["last_world_agent_mode"] = "local"
        state["last_world_agent_error"] = f"{type(exc).__name__}: {str(exc)[:160]}"
    if not line:
        if tick % 3 == 1:
            item = distort_article(article, rng) if tick % 2 else {
                **article,
                "truth_label": "real",
                "editor_note": "Fresh wire story. Verify source, relevance, and wording.",
                "public_pressure": "new alert from the live desk",
            }
            item.update({"id": f"news-live-{tick}", "decision": None, "correct": None, "points": 0})
            state["news_items"].insert(0, item)
            line = f"NewsScoutAgent: new live story entered the queue: {item['title']}"
        elif tick % 3 == 2:
            item = {
                "id": f"email-live-{tick}",
                "from_name": rng.choice(["Copy Chief", "Legal Desk", "Audience Editor", "Freelance Source"]),
                "from_email": "live-desk@newmedia.local",
                "subject": rng.choice(["Can you clear this update?", "Urgent wording check", "Source wants a reply", "Homepage slot opened"]),
                "body": f"The live desk needs your judgement on this developing item: {article['title']}. What should we do before it moves?",
                "messages": [
                    message("Live Desk", f"The live desk needs your judgement on this developing item: {article['title']}. What should we do before it moves?")
                ],
                "linked_news_id": None,
                "options": [
                    {"id": "verify-first", "label": "Verify first and hold publication"},
                    {"id": "publish-fast", "label": "Publish fast while traffic is high"},
                    {"id": "ask-source", "label": "Ask for source documents"},
                ],
                "correct_option": rng.choice(["verify-first", "ask-source"]),
                "selected": None,
                "custom_answer": "",
                **new_thread_state(),
            }
            state["emails"].insert(0, item)
            line = f"InboxAgent: new email arrived from {item['from_name']}: {item['subject']}"
        else:
            contact = rng.choice(["Dad", "Sara", "Niko", "Aunt Lina", "Classmate"])
            item = {
                "id": f"tg-live-{tick}",
                "contact": contact,
                "relationship": rng.choice(["family", "friend", "source"]),
                "messages": [
                    message(contact, "This just popped up in my feed. Is it real?"),
                    message(contact, article["title"]),
                    message(contact, "I need a quick answer before I forward it."),
                ],
                "options": [
                    {"id": "ask-wait", "label": "Ask them to wait while you verify"},
                    {"id": "share-fast", "label": "Tell them to forward it"},
                    {"id": "request-evidence", "label": "Request source and screenshots"},
                ],
            "correct_option": rng.choice(["ask-wait", "request-evidence"]),
            "selected": None,
            "custom_answer": "",
            **new_thread_state(),
            }
            state["telegram_threads"].insert(0, item)
            line = f"SideQuestAgent: {contact} sent a new private-message sidequest"

    state["world_feed"].insert(0, line)
    state["generation_log"].append(line)
    update_goals(state)
    return state
