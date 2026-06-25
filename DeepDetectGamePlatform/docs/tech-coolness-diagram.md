# DeepDetect Tech Coolness Diagram

Open or embed:

```text
DeepDetectGamePlatform/docs/tech-coolness-diagram.svg
```

## Style

The SVG is styled after the Unity game's horror presentation rather than the browser dashboard: dark monitor background, scanline/static texture, cold blue text glow, red warning accents, and night-shift terminal panels.

## What It Shows

The diagram frames the project as three connected systems:

- **Immersive Unity Client**: Apartment scene, phone simulator, world-space PC canvas, global points/paranoia/timer HUD, and ending flow.
- **Agentic Mission Engine**: NewsScoutAgent, DistortionAgent, InboxAgent, SideQuestAgent, MissionDirector, and WorldDirector all feeding the live game-state core.
- **FastAPI Backend Platform**: Auth, sessions, generate/action/tick endpoints, OpenAI/Gemini provider runtime, RSS inputs, SQLite persistence, backups, and media enrichment.

The bottom rail highlights the engineering loop: browser frontend, Unity integration, Playwright e2e, Docker Compose, smoke scripts, screenshots, and JSON reports.
