const api = window.location.origin;
let lastJobId = null;
let conversationId = null;

async function json(url, options) {
  const res = await fetch(url, {
    headers: { Accept: "application/json", "Content-Type": "application/json", "X-Tenant-Id": "pilot-tenant" },
    ...options,
  });
  const text = await res.text();
  let body = text;
  try { body = text ? JSON.parse(text) : null; } catch { /* */ }
  if (!res.ok) {
    const msg = body?.error || body?.message || body || res.statusText;
    throw new Error(typeof msg === "string" ? msg : JSON.stringify(msg));
  }
  return body;
}

function siteId() {
  const v = document.getElementById("site-select").value;
  if (!v) throw new Error("Select an online site first.");
  return v;
}

function formatJson(text) {
  try { return JSON.stringify(JSON.parse(text), null, 2); } catch { return text; }
}

async function runWait(operation, parameters = {}) {
  return json(`${api}/api/jobs/run-wait`, {
    method: "POST",
    body: JSON.stringify({
      siteId: siteId(),
      operation,
      parameters,
      requestedBy: "insight-ui",
      timeoutSeconds: 90,
    }),
  });
}

async function loadSites() {
  const sel = document.getElementById("site-select");
  const sites = await json(`${api}/api/sites`);
  const online = sites.filter((s) => s.isOnline);
  sel.innerHTML = online.length
    ? online.map((s) => `<option value="${s.siteId}">${escapeHtml(s.siteName)}</option>`).join("")
    : '<option value="">No online sites</option>';
}

document.querySelectorAll(".tab").forEach((tab) => {
  tab.addEventListener("click", () => {
    document.querySelectorAll(".tab").forEach((t) => t.classList.remove("active"));
    document.querySelectorAll(".panel").forEach((p) => p.classList.remove("active"));
    tab.classList.add("active");
    document.getElementById(`panel-${tab.dataset.tab}`).classList.add("active");
  });
});

document.getElementById("btn-dashboard").addEventListener("click", async () => {
  const out = document.getElementById("dashboard-out");
  out.textContent = "Loading…";
  try {
    const job = await json(`${api}/api/insight/dashboard/${siteId()}`);
    lastJobId = job.jobId;
    out.textContent = formatJson(job.resultJson || "{}");
  } catch (e) { out.textContent = e.message; }
});

async function runWorkspace(op, accountInputId, outId) {
  const out = document.getElementById(outId);
  out.textContent = "Loading…";
  const params = { top: "100" };
  const acct = document.getElementById(accountInputId).value.trim();
  if (acct) params.account = acct;
  try {
    const job = await runWait(op, params);
    lastJobId = job.jobId;
    out.textContent = formatJson(job.resultJson || "{}");
  } catch (e) { out.textContent = e.message; }
}

document.querySelectorAll("#panel-ar [data-op]").forEach((btn) => {
  btn.addEventListener("click", () => runWorkspace(btn.dataset.op, "ar-account", "ar-out"));
});
document.querySelectorAll("#panel-ap [data-op]").forEach((btn) => {
  btn.addEventListener("click", () => runWorkspace(btn.dataset.op, "ap-account", "ap-out"));
});

document.getElementById("export-ar").addEventListener("click", () => exportLast());
document.getElementById("export-ap").addEventListener("click", () => exportLast());

function exportLast() {
  if (!lastJobId) { alert("Run a list first."); return; }
  window.open(`${api}/api/insight/export/${lastJobId}`, "_blank");
}

document.getElementById("search-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const q = new FormData(e.target).get("q");
  const out = document.getElementById("search-out");
  out.textContent = "Searching…";
  try {
    const job = await json(`${api}/api/insight/search`, {
      method: "POST",
      body: JSON.stringify({ siteId: siteId(), query: q }),
    });
    lastJobId = job.jobId;
    out.textContent = formatJson(job.resultJson || "{}");
  } catch (err) { out.textContent = err.message; }
});

function appendChat(role, text) {
  const log = document.getElementById("chat-log");
  const div = document.createElement("div");
  div.className = `chat-msg ${role}`;
  div.innerHTML = `<strong>${role === "user" ? "You" : "Assistant"}:</strong> ${escapeHtml(text).replace(/\n/g, "<br>")}`;
  log.appendChild(div);
  log.scrollTop = log.scrollHeight;
}

document.getElementById("chat-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const message = new FormData(e.target).get("message");
  e.target.reset();
  appendChat("user", message);
  try {
    const res = await json(`${api}/api/insight/chat`, {
      method: "POST",
      body: JSON.stringify({ siteId: siteId(), conversationId, message }),
    });
    conversationId = res.conversationId;
    appendChat("assistant", res.reply);
  } catch (err) {
    appendChat("assistant", err.message);
  }
});

function escapeHtml(s) {
  return String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
}

loadSites();
setInterval(loadSites, 20000);
