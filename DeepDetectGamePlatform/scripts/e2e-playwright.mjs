import { chromium } from "playwright";
import fs from "node:fs/promises";
import path from "node:path";

const baseUrl = process.env.DEEPDETECT_URL || "http://127.0.0.1:8765";
const screenshotDir = path.resolve("docs", "screenshots");
const reportPath = path.resolve("docs", "browser-test-report.json");

await fs.mkdir(screenshotDir, { recursive: true });

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });

const steps = [];
const record = async (name, screenshotName) => {
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.waitForTimeout(150);
  const file = path.join(screenshotDir, screenshotName);
  await page.screenshot({ path: file, fullPage: true });
  steps.push({ name, screenshot: file, url: page.url() });
};

const fetchDebugState = async () => page.evaluate(async () => {
  const token = localStorage.getItem("dd_token");
  const gamesResponse = await fetch("/api/games", { headers: { Authorization: `Bearer ${token}` } });
  const games = await gamesResponse.json();
  const gameId = games.games[0].id;
  const debugResponse = await fetch(`/api/game/${gameId}/debug`, { headers: { Authorization: `Bearer ${token}` } });
  return debugResponse.json();
});

const waitForThreadTurn = async (collectionName, id, turn) => page.waitForFunction(
  async ({ collectionName, id, turn }) => {
    const token = localStorage.getItem("dd_token");
    const gamesResponse = await fetch("/api/games", { headers: { Authorization: `Bearer ${token}` } });
    const games = await gamesResponse.json();
    const gameId = games.games[0].id;
    const debugResponse = await fetch(`/api/game/${gameId}/debug`, { headers: { Authorization: `Bearer ${token}` } });
    const debug = await debugResponse.json();
    const item = debug[collectionName].find((entry) => entry.id === id);
    return item && item.chat_turns >= turn;
  },
  { collectionName, id, turn },
  { timeout: 90000 },
);

try {
  await page.goto(baseUrl, { waitUntil: "domcontentloaded" });
  await page.getByRole("button", { name: "Register" }).click();
  const stamp = Date.now();
  await page.getByPlaceholder("Editor name").fill("Browser Agent");
  await page.getByPlaceholder("you@example.com").fill(`browser.agent.${stamp}@example.com`);
  await page.getByPlaceholder("At least 6 characters").fill("secret123");
  await page.getByRole("button", { name: "Register" }).last().click();
  await page.getByRole("button", { name: "Generate Game" }).first().waitFor({ state: "visible" });
  await record("registered dashboard", "01-dashboard.png");

  await page.getByRole("button", { name: "Generate Game" }).first().click();
  await page.getByText("Agent runtime:", { exact: false }).waitFor({ state: "visible", timeout: 90000 });
  await record("generated game newsdesk", "02-generated-newsdesk.png");

  for (let depth = 1; depth <= 5; depth += 1) {
    await page.getByRole("button", { name: "Advance World" }).click();
    await page.getByText("last world:", { exact: false }).waitFor({ state: "visible", timeout: 90000 });
    await page.locator(".log-line").first().waitFor({ state: "visible", timeout: 90000 });
    await record(`live world tick ${depth}`, `06-live-world-tick-${depth}.png`);
  }

  const debugAfterTicks = await fetchDebugState();
  if (Object.keys(debugAfterTicks.values || {}).length !== 4) throw new Error("Expected four value meters");
  if ((debugAfterTicks.quests || []).length !== 4) throw new Error("Expected four quests");
  const newsChoices = debugAfterTicks.news_truth.slice(0, 6).map((item) => [
    item.id,
    item.truth_label === "real" ? "publish" : "reject",
  ]);
  for (const [itemId, choice] of newsChoices) {
    const locator = page.locator(`button[data-action="news"][data-id="${itemId}"][data-choice="${choice}"]`);
    await locator.waitFor({ state: "visible" });
    await locator.click();
  }
  await page.getByText("4/4 complete", { exact: true }).waitFor({ state: "visible" });

  await page.getByRole("button", { name: "Inbox" }).click();
  await page.locator('button[data-action="email"]').first().waitFor({ state: "visible" });
  await record("inbox mission", "03-inbox.png");
  const debugBeforeEmail = await fetchDebugState();
  const targetEmail = debugBeforeEmail.email_modes.find((item) => !item.selected);
  if (!targetEmail) throw new Error("Expected an unresolved email thread");
  await page.locator(`[data-email-id="${targetEmail.id}"]`).click();
  const emailReplies = [
    "Please wait while I verify the original source and keep the item out of the feed.",
    "I found the source trail, but I still need corroborating evidence before we move.",
    "I will archive the source trail and recommend holding until the claim is verified.",
  ];
  for (const [index, reply] of emailReplies.entries()) {
    await page.locator(`form[data-custom-surface="email"][data-custom-id="${targetEmail.id}"] textarea`).fill(reply);
    await page.locator(`form[data-custom-surface="email"][data-custom-id="${targetEmail.id}"] button`).click();
    await waitForThreadTurn("email_modes", targetEmail.id, index + 1);
    if (index === 0) await record("email custom reply turn one", "07-email-agent-response.png");
  }
  await page.waitForFunction(
    (id) => !document.querySelector(`form[data-custom-surface="email"][data-custom-id="${id}"]`),
    targetEmail.id,
    { timeout: 90000 },
  );
  await record("email thread resolved after three turns", "09-email-multiturn-resolved.png");
  const debugAfterEmailCustom = await fetchDebugState();
  const resolvedEmail = debugAfterEmailCustom.email_modes.find((item) => item.id === targetEmail.id);
  if (!resolvedEmail?.selected || resolvedEmail.chat_turns < 3) throw new Error("Expected email to resolve after at least three turns");

  await page.getByRole("button", { name: "Telegram" }).click();
  await page.locator('button[data-action="telegram"]').first().waitFor({ state: "visible" });
  await record("telegram sidequest", "04-telegram.png");
  const debugBeforeTelegram = await fetchDebugState();
  const targetTelegram = debugBeforeTelegram.telegram_modes.find((item) => !item.selected);
  if (!targetTelegram) throw new Error("Expected an unresolved Telegram thread");
  const telegramReplies = [
    "Do not forward it yet; I will inspect the source first.",
    "Please send me where it came from and any screenshots so I can check the evidence.",
    "I checked it; wait for a verified summary before sharing anything.",
  ];
  for (const [index, reply] of telegramReplies.entries()) {
    await page.locator(`form[data-custom-surface="telegram"][data-custom-id="${targetTelegram.id}"] textarea`).fill(reply);
    await page.locator(`form[data-custom-surface="telegram"][data-custom-id="${targetTelegram.id}"] button`).click();
    await waitForThreadTurn("telegram_modes", targetTelegram.id, index + 1);
    if (index === 0) await record("telegram custom reply turn one", "08-telegram-agent-response.png");
  }
  await page.waitForFunction(
    (id) => !document.querySelector(`form[data-custom-surface="telegram"][data-custom-id="${id}"]`),
    targetTelegram.id,
    { timeout: 90000 },
  );
  await record("telegram thread resolved after three turns", "10-telegram-multiturn-resolved.png");
  const debugAfterTelegramCustom = await fetchDebugState();
  const resolvedTelegram = debugAfterTelegramCustom.telegram_modes.find((item) => item.id === targetTelegram.id);
  if (!resolvedTelegram?.selected || resolvedTelegram.chat_turns < 3) throw new Error("Expected Telegram to resolve after at least three turns");

  await page.getByRole("button", { name: "Briefing" }).click();
  await page.getByRole("heading", { name: /Shift active/ }).waitFor({ state: "visible" });
  await page.getByText("Action log", { exact: true }).waitFor({ state: "visible" });
  await record("briefing and action log", "05-briefing.png");

  const score = await page.locator("#score").innerText();
  const actionLogCount = await page.locator("#tab-briefing .log-line").count();
  if (Number(score) < 600) throw new Error(`Expected score of at least 600, got ${score}`);
  if (actionLogCount < 8) throw new Error(`Expected at least 8 action log entries, got ${actionLogCount}`);
  const agentStatus = await page.locator("#agent-status").innerText();
  if (!agentStatus.includes("Agent runtime:")) throw new Error(`Expected agent status, got ${agentStatus}`);
  const finalDebug = await fetchDebugState();
  const finalValues = Object.fromEntries(Object.entries(finalDebug.values || {}).map(([key, value]) => [key, value.value]));
  const finalQuests = Object.fromEntries((finalDebug.quests || []).map((quest) => [quest.id, { current: quest.current, complete: quest.complete }]));
  if (!Object.values(finalValues).some((value) => value !== 50)) throw new Error("Expected values to change after player actions");
  if (!finalQuests["homepage-guardian"]?.complete) throw new Error("Expected Homepage Guardian quest to complete");
  if ((finalQuests["source-chain"]?.current || 0) < 1) throw new Error("Expected Source Chain quest to progress");
  if ((finalQuests["social-firebreak"]?.current || 0) < 1) throw new Error("Expected Social Firebreak quest to progress");
  steps.push({ name: "assertions", score, actionLogCount, customReplies: true, emailTurns: resolvedEmail.chat_turns, telegramTurns: resolvedTelegram.chat_turns, values: finalValues, quests: finalQuests, worldAdvancedDepth: 5, agentStatus });
  await fs.writeFile(reportPath, JSON.stringify({ ok: true, baseUrl, steps }, null, 2));
  console.log(JSON.stringify({ ok: true, score, actionLogCount, reportPath }, null, 2));
} catch (error) {
  await fs.writeFile(reportPath, JSON.stringify({ ok: false, baseUrl, error: String(error), steps }, null, 2));
  console.error(error);
  process.exitCode = 1;
} finally {
  await browser.close();
}
