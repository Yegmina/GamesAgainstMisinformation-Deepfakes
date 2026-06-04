# DeepDetect Gameplay

DeepDetect is a browser game where the player works as a new-media editor during a high-pressure morning shift. The goal is to publish credible stories, reject manipulated ones, and respond responsibly to newsroom and personal messages.

## 1. Register or Login

The platform starts with account access. New players can register; returning players can log in and keep their generated game history in the local SQLite database.

![Dashboard after registration](file:///C:/Erasmus/DeepDetectGamePlatform/docs/screenshots/01-dashboard.png)

## 2. Generate Game

Click **Generate Game** to run the preparation pipeline and open the full shift HUD:

- `NewsScoutAgent` scrapes recent public RSS news.
- `DistortionAgent` creates suspicious manipulated versions of some stories.
- `InboxAgent` prepares work email tasks.
- `SideQuestAgent` creates family/friend Telegram-style sidequests.
- `MissionDirector` packages goals, scoring, quests, values, and briefing.
- `WorldDirector` keeps the shift alive after generation so new events can arrive while the player is working.

![Generated newsdesk](file:///C:/Erasmus/DeepDetectGamePlatform/docs/screenshots/02-generated-newsdesk.png)

The top HUD is part of the game, not just decoration. Mission goals track required work, quests reward larger arcs, and values show the consequences of editorial choices:

- **Public trust** rises when the desk makes credible calls and falls when misinformation slips through.
- **Editorial integrity** rises when the player resists pressure and protects evidence.
- **Community care** rises when private conversations slow down rumors without humiliating people.
- **Newsroom momentum** changes as the desk moves or stalls under pressure.

When `OPENAI_API_KEY` is available, the visible badge shows the live runtime, for example `Agent runtime: openai / gpt-4o-mini`. The shift is no longer static. **Advance World** simulates time passing and lets agents inject fresh news, letters, or Telegram sidequests into the live game state.

![Live world tick](file:///C:/Erasmus/DeepDetectGamePlatform/docs/screenshots/06-live-world-tick-5.png)

## 3. Play The Newsdesk Mission

The player reviews the editorial queue and chooses **Publish** or **Reject** for each story. Real stories should be published when the source and framing are credible. Manipulated stories should be rejected when they use unsupported certainty, emotional pressure, or missing-source claims.

## 4. Handle Inbox Missions

Inbox tasks simulate workplace pressure: editors, partners, or sources ask the player to verify, publish, change wording, or respond. Correct choices reward verification and resisting pressure.

![Inbox mission](file:///C:/Erasmus/DeepDetectGamePlatform/docs/screenshots/03-inbox.png)

Players can use fast answer buttons or type a custom reply. Email threads now stay open across multiple turns, so the player has to ask for sources, evidence, and verification before the agent resolves the exchange.

![Email agent response](file:///C:/Erasmus/DeepDetectGamePlatform/docs/screenshots/07-email-agent-response.png)

## 5. Complete Sidequests

Telegram sidequests add social pressure from family and friends. These missions test whether the player can slow down misinformation without escalating or humiliating the sender.

![Telegram sidequest](file:///C:/Erasmus/DeepDetectGamePlatform/docs/screenshots/04-telegram.png)

Custom Telegram replies also receive in-world responses from the simulated contact. Like email, the thread can continue for several turns before it is scored.

![Telegram agent response](file:///C:/Erasmus/DeepDetectGamePlatform/docs/screenshots/08-telegram-agent-response.png)

## 6. Review Briefing

The briefing tab explains the rules, summarizes current values and quests, and shows the action log. A shift is complete after all news, email, and Telegram tasks are resolved.

![Briefing and action log](file:///C:/Erasmus/DeepDetectGamePlatform/docs/screenshots/05-briefing.png)

## Browser Verification

The current build was tested with a real browser automation flow:

```powershell
cd C:\Erasmus\DeepDetectGamePlatform
npm run e2e
```

Result: registration, game generation, quest/value HUD validation, five world-advancement ticks, adaptive Newsdesk decisions against generated truth labels, a three-turn Inbox conversation, a three-turn Telegram conversation, final Briefing review, score/action-log assertions, quest progress assertions, value-change assertions, and visible agent-runtime assertion all passed. The report is saved at `docs/browser-test-report.json`.
