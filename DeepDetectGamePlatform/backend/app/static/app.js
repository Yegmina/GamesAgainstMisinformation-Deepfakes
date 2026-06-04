const state = {
  mode: "login",
  token: localStorage.getItem("dd_token") || "",
  user: JSON.parse(localStorage.getItem("dd_user") || "null"),
  game: null,
  activeEmailId: "",
};

const $ = (selector) => document.querySelector(selector);
const $$ = (selector) => [...document.querySelectorAll(selector)];

function setAuthMode(mode) {
  state.mode = mode;
  $("#show-login").classList.toggle("active", mode === "login");
  $("#show-register").classList.toggle("active", mode === "register");
  $("#name-field").classList.toggle("hidden", mode === "login");
  $("#auth-submit").textContent = mode === "login" ? "Login" : "Register";
  $("#auth-error").textContent = "";
}

async function api(path, options = {}) {
  const headers = { "Content-Type": "application/json", ...(options.headers || {}) };
  if (state.token) headers.Authorization = `Bearer ${state.token}`;
  const response = await fetch(path, { ...options, headers });
  const data = await response.json().catch(() => ({}));
  if (!response.ok) {
    throw new Error(data.detail || "Request failed");
  }
  return data;
}

function showApp() {
  const signedIn = Boolean(state.token && state.user);
  $("#auth-screen").classList.toggle("hidden", signedIn);
  $("#game-shell").classList.toggle("hidden", !signedIn);
  $("#advance-world").classList.toggle("hidden", !signedIn || !state.game);
  if (signedIn) {
    $("#game-title").textContent = state.game?.title || `DeepDetect / ${state.user.name}`;
  }
  $("#agent-status").classList.toggle("hidden", !state.game);
}

async function handleAuth(event) {
  event.preventDefault();
  $("#auth-error").textContent = "";
  const payload = {
    email: $("#email").value.trim(),
    password: $("#password").value,
  };
  if (state.mode === "register") payload.name = $("#name").value.trim();
  try {
    const data = await api(`/api/auth/${state.mode}`, {
      method: "POST",
      body: JSON.stringify(payload),
    });
    state.token = data.token;
    state.user = data.user;
    localStorage.setItem("dd_token", state.token);
    localStorage.setItem("dd_user", JSON.stringify(state.user));
    showApp();
  } catch (error) {
    $("#auth-error").textContent = error.message;
  }
}

function renderGoals() {
  const values = Object.values(state.game.values || {});
  $("#goals").innerHTML = `
    <section class="hud-panel">
      <div class="hud-heading">
        <strong>Mission board</strong>
        <span>${state.game.complete ? "Shift complete" : "Shift active"}</span>
      </div>
      <div class="goal-list">
        ${state.game.goals.map((goal) => `
          <article class="goal ${goal.complete ? "complete" : ""}">
            <strong>${goal.title}</strong>
            <span>${goal.current}/${goal.target} complete</span>
          </article>
        `).join("")}
      </div>
    </section>
    <section class="hud-panel values-panel">
      <div class="hud-heading">
        <strong>Values</strong>
        <span>Choice consequences</span>
      </div>
      ${values.map((value) => `
        <article class="value-meter">
          <div>
            <strong>${value.label}</strong>
            <span>${value.description}</span>
          </div>
          <b>${value.value}</b>
          <div class="meter-track"><i style="width:${value.value}%"></i></div>
        </article>
      `).join("")}
    </section>
  `;
}

function renderAgentLog() {
  const quests = state.game.quests || [];
  const world = (state.game.world_feed || []).slice(0, 4);
  const questLines = (state.game.quest_log || []).slice(0, 3);
  const generation = (state.game.generation_log || []).slice(-4);
  const lines = [...new Set([...world, ...generation])];
  $("#agent-log").innerHTML = `
    <section class="hud-panel quest-panel">
      <div class="hud-heading">
        <strong>Quest log</strong>
        <span>${quests.filter((quest) => quest.complete).length}/${quests.length} complete</span>
      </div>
      ${quests.map((quest) => `
        <article class="quest ${quest.complete ? "complete" : ""}">
          <div>
            <span class="quest-type">${quest.type}</span>
            <strong>${quest.title}</strong>
            <p>${quest.description}</p>
          </div>
          <span>${quest.current}/${quest.target}</span>
        </article>
      `).join("")}
    </section>
    <section class="hud-panel">
      <div class="hud-heading">
        <strong>Live feed</strong>
        <span>Agents and rewards</span>
      </div>
      ${questLines.map((line) => `<div class="log-line reward-line">${line}</div>`).join("")}
      ${lines.map((line) => `<div class="log-line">${line}</div>`).join("")}
    </section>
  `;
}

function resultMarkup(item) {
  if (item.correct === null || item.correct === undefined) return "";
  const label = item.correct ? "Correct call" : "Risky call";
  return `<div class="result ${item.correct ? "good" : "bad"}">${label}</div>`;
}

function msgText(message) {
  return typeof message === "string" ? message : message.text;
}

function renderThread(messages, className = "thread") {
  return `<div class="${className}">
    ${(messages || []).map((entry) => {
      const role = typeof entry === "string" ? "agent" : entry.role;
      const sender = typeof entry === "string" ? "" : entry.sender;
      return `<div class="bubble ${role === "player" ? "player" : "agent"}">
        ${sender ? `<strong>${sender}</strong>` : ""}
        <span>${msgText(entry)}</span>
      </div>`;
    }).join("")}
  </div>`;
}

function customReplyForm(surface, itemId, placeholder) {
  return `
    <form class="custom-reply" data-custom-surface="${surface}" data-custom-id="${itemId}">
      <textarea name="customText" rows="3" placeholder="${placeholder}"></textarea>
      <button type="submit">Send custom reply</button>
    </form>
  `;
}

function emailResultClass(item) {
  if (item.correct === true) return "resolved-good";
  if (item.correct === false) return "resolved-bad";
  return "";
}

function sourceHost(url, fallback) {
  try {
    return new URL(url).hostname.replace(/^www\./, "");
  } catch {
    return fallback || "source pending";
  }
}

function newsStatus(item) {
  if (item.decision) return item.correct ? "Cleared" : "Flagged";
  return item.truth_label === "manipulated" ? "Needs checks" : "Ready check";
}

function threadProgress(item) {
  if (item.selected || item.resolved) return "Resolved";
  const turns = Number(item.chat_turns || 0);
  const minTurns = Number(item.min_turns || 3);
  return `Thread ${turns}/${minTurns}`;
}

function renderNews() {
  const lead = state.game.news_items[0];
  const queue = state.game.news_items.slice(1);
  $("#tab-news").innerHTML = `
    <div class="newsroom-shell">
      <aside class="news-sidebar">
        <div class="news-logo">DD</div>
        <button class="news-tool active">Wire</button>
        <button class="news-tool">CMS</button>
        <button class="news-tool">Verify</button>
        <button class="news-tool">Legal</button>
      </aside>
      <section class="news-main">
        <div class="news-toolbar">
          <div>
            <strong>Live editorial desk</strong>
            <span>${state.game.news_items.length} incoming wires / ${state.game.news_items.filter((item) => !item.decision).length} open decisions</span>
          </div>
          <div class="news-filters">
            <span>Wire</span>
            <span>Homepage</span>
            <span>Fact desk</span>
          </div>
        </div>
        <article class="lead-story">
          <div class="lead-copy">
            <div class="story-kicker">
              <span class="desk-label">Lead slot</span>
              <span>${sourceHost(lead.url, lead.source)}</span>
              <span>${newsStatus(lead)}</span>
            </div>
            <h2>${lead.title}</h2>
            <p>${lead.summary}</p>
            <div class="news-evidence">
              <span><strong>Source</strong>${lead.source}</span>
              <span><strong>Pressure</strong>${lead.public_pressure}</span>
              <span><strong>Desk note</strong>${lead.editor_note}</span>
            </div>
            ${resultMarkup(lead)}
          </div>
          <div class="decision-panel">
            <span>Homepage decision</span>
            <strong>${lead.decision ? lead.decision.toUpperCase() : "Awaiting editor"}</strong>
            <div class="choices">
              <button class="publish" data-action="news" data-id="${lead.id}" data-choice="publish" ${lead.decision ? "disabled" : ""}>Publish</button>
              <button class="reject" data-action="news" data-id="${lead.id}" data-choice="reject" ${lead.decision ? "disabled" : ""}>Reject</button>
            </div>
          </div>
        </article>
        <div class="wire-list">
          ${queue.map((item) => `
            <article class="wire-row ${item.decision ? "decided" : ""}">
              <div class="wire-status">
                <strong>${newsStatus(item)}</strong>
                <span>${sourceHost(item.url, item.source)}</span>
              </div>
              <div class="wire-copy">
                <div class="story-kicker">
                  <span>${item.source}</span>
                  <span>${item.public_pressure}</span>
                </div>
                <h3>${item.title}</h3>
                <p>${item.summary}</p>
                <p class="desk-note">${item.editor_note}</p>
                ${resultMarkup(item)}
              </div>
              <div class="choices compact">
                <button class="publish" data-action="news" data-id="${item.id}" data-choice="publish" ${item.decision ? "disabled" : ""}>Publish</button>
                <button class="reject" data-action="news" data-id="${item.id}" data-choice="reject" ${item.decision ? "disabled" : ""}>Reject</button>
              </div>
            </article>
          `).join("")}
        </div>
      </section>
    </div>`;
}

function renderEmails() {
  const activeEmail = state.game.emails.find((item) => item.id === state.activeEmailId) || state.game.emails.find((item) => !item.selected) || state.game.emails[0];
  state.activeEmailId = activeEmail?.id || "";
  $("#tab-email").innerHTML = `
    <div class="gmail-shell">
      <header class="gmail-header">
        <div class="gmail-brand">
          <span class="hamburger">&#9776;</span>
          <img src="/static/assets/gmail-logo.svg" alt="" />
          <strong>Gmail</strong>
        </div>
        <div class="gmail-search">Search mail</div>
        <div class="gmail-icons">
          <span>?</span>
          <span>&#9881;</span>
          <span>&#8942;</span>
          <img src="/static/assets/profile-pic.jpg" alt="" />
        </div>
      </header>
      <div class="gmail-body">
        <aside class="gmail-leftnav">
          <button class="compose-dot">&#9998;</button>
          <span class="active-mailbox">Inbox</span>
          <span>Starred</span>
          <span>Snoozed</span>
          <span>Sent</span>
          <span>Labels</span>
        </aside>
        <section class="gmail-main">
          <div class="gmail-toolbar">
            <span>&#9744;</span>
            <span>&#8635;</span>
            <span>&#8942;</span>
            <strong>1-${state.game.emails.length} of ${state.game.emails.length}</strong>
          </div>
          <div class="gmail-categories">
            <button class="active">Primary</button>
            <button>Social</button>
            <button>Updates</button>
            <button>Forums</button>
          </div>
          <div class="gmail-layout">
            <div class="gmail-list">
              ${state.game.emails.map((item) => `
                <article class="gmail-row ${activeEmail.id === item.id ? "selected" : ""} ${emailResultClass(item)}" data-email-id="${item.id}">
                  <span class="star">&#9734;</span>
                  <strong>${item.from_name}</strong>
                  <span class="gmail-subject">${item.subject}</span>
                  <span class="gmail-snippet">${msgText((item.messages || [item.body]).at(-1))}</span>
                  <time>${threadProgress(item)}</time>
                </article>
              `).join("")}
            </div>
            <article class="gmail-reader">
              <div class="reader-meta">
                <h2>${activeEmail.subject}</h2>
                <div class="gmail-message-head">
                  <span class="gmail-avatar">${activeEmail.from_name.slice(0, 1).toUpperCase()}</span>
                  <span><strong>${activeEmail.from_name}</strong> &lt;${activeEmail.from_email}&gt;</span>
                  <time>${threadProgress(activeEmail)}</time>
                </div>
              </div>
              ${renderThread(activeEmail.messages || [activeEmail.body], "email-thread")}
              ${resultMarkup(activeEmail)}
              <div class="choices email-actions">
                ${activeEmail.options.map((option) => `
                  <button data-action="email" data-id="${activeEmail.id}" data-choice="${option.id}" ${activeEmail.selected ? "disabled" : ""}>${option.label}</button>
                `).join("")}
              </div>
              ${activeEmail.selected ? "" : `<div class="gmail-reply-card">${customReplyForm("email", activeEmail.id, "Write your own newsroom reply...")}</div>`}
            </article>
          </div>
        </section>
        <aside class="gmail-rightnav">
          <span>Cal</span>
          <span>Keep</span>
          <span>Task</span>
          <span>+</span>
        </aside>
      </div>
    </div>`;
}
function renderTelegram() {
  $("#tab-telegram").innerHTML = `<div class="grid">${state.game.telegram_threads.map((thread) => `
    <article class="card">
      <div class="meta">
        <span class="pill">${thread.contact}</span>
        <span class="pill">${thread.relationship}</span>
      </div>
      ${renderThread(thread.messages, "chat-thread")}
      ${resultMarkup(thread)}
      <div class="choices">
        ${thread.options.map((option) => `
          <button data-action="telegram" data-id="${thread.id}" data-choice="${option.id}" ${thread.selected ? "disabled" : ""}>${option.label}</button>
        `).join("")}
      </div>
      ${thread.selected ? "" : customReplyForm("telegram", thread.id, "Write your own message...")}
    </article>
  `).join("")}</div>`;
}

function renderBriefing() {
  const completed = state.game.complete ? "Shift complete. Review your calls and replay with a new generated day." : "Shift active. Finish every workspace to complete the day.";
  const values = Object.values(state.game.values || {});
  const quests = state.game.quests || [];
  $("#tab-briefing").innerHTML = `
    <section class="briefing">
      <h2>${completed}</h2>
      <div class="briefing-systems">
        <div>
          <h3>Values</h3>
          ${values.map((value) => `<p><strong>${value.label}:</strong> ${value.value}/100 - ${value.description}</p>`).join("")}
        </div>
        <div>
          <h3>Quests</h3>
          ${quests.map((quest) => `<p><strong>${quest.title}:</strong> ${quest.current}/${quest.target} ${quest.complete ? "complete" : "active"} - ${quest.reward}</p>`).join("")}
        </div>
      </div>
      <ol>
        <li>You are responsible for what appears on the new-media front page.</li>
        <li>Real stories should be published only when the source and framing are credible.</li>
        <li>Manipulated stories often contain pressure, unsupported certainty, or emotional wording.</li>
        <li>Email and Telegram sidequests affect your trust score just like newsdesk calls.</li>
      </ol>
      <h3>Action log</h3>
      <div class="agent-log">${state.game.action_log.map((line) => `<div class="log-line">${line}</div>`).join("") || "<div class='log-line'>No actions yet.</div>"}</div>
    </section>
  `;
}

function renderGame() {
  $("#empty-state").classList.add("hidden");
  $("#active-game").classList.remove("hidden");
  $("#advance-world").classList.remove("hidden");
  $("#game-title").textContent = state.game.title;
  $("#agent-status").classList.remove("hidden");
  $("#agent-status").textContent = `Agent runtime: ${state.game.agent_mode || "local"} / ${state.game.agent_model || "unknown"}${state.game.last_world_agent_mode ? ` / last world: ${state.game.last_world_agent_mode}` : ""}`;
  $("#score").textContent = state.game.score;
  renderGoals();
  renderAgentLog();
  renderNews();
  renderEmails();
  renderTelegram();
  renderBriefing();
}

async function generateGame() {
  const buttons = [$("#generate-game"), $("#generate-game-empty")];
  buttons.forEach((button) => {
    button.disabled = true;
    button.textContent = "Generating...";
  });
  try {
    const data = await api("/api/game/generate", { method: "POST", body: "{}" });
    state.game = data.game;
    renderGame();
  } catch (error) {
    alert(error.message);
  } finally {
    buttons.forEach((button) => {
      button.disabled = false;
      button.textContent = "Generate Game";
    });
  }
}

async function sendAction(button) {
  const data = await api(`/api/game/${state.game.id}/action`, {
    method: "POST",
    body: JSON.stringify({
      surface: button.dataset.action,
      item_id: button.dataset.id,
      choice: button.dataset.choice,
    }),
  });
  state.game = data.game;
  renderGame();
}

async function sendCustomReply(form) {
  const customText = String(new FormData(form).get("customText") || "").trim();
  if (!customText) return;
  if (form.dataset.customSurface === "email") state.activeEmailId = form.dataset.customId;
  const data = await api(`/api/game/${state.game.id}/action`, {
    method: "POST",
    body: JSON.stringify({
      surface: form.dataset.customSurface,
      item_id: form.dataset.customId,
      choice: "__custom__",
      custom_text: customText,
    }),
  });
  state.game = data.game;
  renderGame();
}

async function advanceWorld() {
  if (!state.game) return;
  const button = $("#advance-world");
  button.disabled = true;
  button.textContent = "Simulating...";
  try {
    const data = await api(`/api/game/${state.game.id}/tick`, { method: "POST", body: "{}" });
    state.game = data.game;
    renderGame();
  } finally {
    button.disabled = false;
    button.textContent = "Advance World";
  }
}

function switchTab(tabName) {
  $$(".tab").forEach((button) => button.classList.toggle("active", button.dataset.tab === tabName));
  $$(".tab-panel").forEach((panel) => panel.classList.add("hidden"));
  $(`#tab-${tabName}`).classList.remove("hidden");
}

function logout() {
  localStorage.removeItem("dd_token");
  localStorage.removeItem("dd_user");
  state.token = "";
  state.user = null;
  state.game = null;
  showApp();
}

document.addEventListener("click", (event) => {
  const emailRow = event.target.closest("[data-email-id]");
  if (emailRow) {
    state.activeEmailId = emailRow.dataset.emailId;
    renderEmails();
    return;
  }
  const button = event.target.closest("button");
  if (!button) return;
  if (button.id === "show-login") setAuthMode("login");
  if (button.id === "show-register") setAuthMode("register");
  if (button.id === "generate-game" || button.id === "generate-game-empty") generateGame();
  if (button.id === "advance-world") advanceWorld();
  if (button.id === "logout") logout();
  if (button.classList.contains("tab")) switchTab(button.dataset.tab);
  if (button.dataset.action) sendAction(button);
});

$("#auth-form").addEventListener("submit", handleAuth);
document.addEventListener("submit", (event) => {
  const form = event.target.closest(".custom-reply");
  if (!form) return;
  event.preventDefault();
  sendCustomReply(form);
});
setAuthMode("login");
showApp();
