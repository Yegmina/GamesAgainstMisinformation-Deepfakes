# DeepDetect Game Platform

A standalone browser game platform for media-literacy training. Players register, log in, generate a game day, and work as a new-media editor deciding which stories should be published while handling sidequests from email and Telegram-style conversations.

## Run Locally

```powershell
cd C:\Erasmus\DeepDetectGamePlatform
py -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
uvicorn backend.app.main:app --reload --host 127.0.0.1 --port 8765
```

Open:

```text
http://127.0.0.1:8765
```

## What Generate Game Does

`Generate Game` runs an agent pipeline. If `OPENAI_API_KEY` is present in the environment, the platform uses OpenAI (`OPENAI_MODEL_AGENT`, default `gpt-4o-mini`) for generation, custom response judging, and world simulation. If an API call fails, it falls back to the local deterministic agents so the game remains playable.

- `NewsScoutAgent` fetches recent RSS headlines from public news feeds.
- `DistortionAgent` creates manipulated variants from some real stories.
- `InboxAgent` generates work emails connected to the newsdesk mission.
- `SideQuestAgent` creates Telegram-style family/friend sidequests.
- `MissionDirector` scores goals and packages a playable shift.
- `WorldDirector` continues the simulation after generation. Use **Advance World** to inject new live news, emails, or Telegram sidequests while the player is working.

The game works offline too: if RSS feeds are unavailable, it falls back to seeded news scenarios.

Players can use fast reply buttons or write custom answers in email and Telegram threads. Those side conversations now run across several turns before resolution, so the player can ask for sources, evidence, and verification instead of being scored on a single reply.

The shift also has a game layer beyond raw score:

- **Values** track Public trust, Editorial integrity, Community care, and Newsroom momentum.
- **Quests** reward larger arcs such as protecting the homepage, building a source chain, slowing social rumors, and keeping the desk balanced.
- **World ticks** add pressure over time, so waiting, verification, and public-facing choices have visible consequences.

## Gameplay

The player is a new-media editor. The main mission is to publish real, relevant stories and reject manipulated ones. Sidequests add pressure through emails and personal messages, where the player must choose how to respond without spreading misinformation.

See [docs/GAMEPLAY.md](docs/GAMEPLAY.md) for a short walkthrough with screenshots.
