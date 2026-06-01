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

const EXPECTED_INSIGHT_CHAT = "2026-06-08-payment-behavior";

async function loadApiVersion() {
  const el = document.getElementById("api-version");
  if (!el) return;
  try {
    const h = await json(`${api}/health`);
    const v = h.insightChatVersion || "(no version — restart API)";
    el.textContent = `Chat ${v}`;
    el.classList.toggle("version-stale", v !== EXPECTED_INSIGHT_CHAT);
    el.title = v === EXPECTED_INSIGHT_CHAT
      ? "API matches this Insight build."
      : "Stale API — WizPilot → Restart local API, or run scripts/restart-local-api.ps1";
  } catch {
    el.textContent = "API offline";
    el.classList.add("version-stale");
  }
}

async function loadSites() {
  const sel = document.getElementById("site-select");
  const previous = sel.value;
  const sites = await json(`${api}/api/sites`);
  const online = sites.filter((s) => s.isOnline);
  sel.innerHTML = online.length
    ? online.map((s) => `<option value="${s.siteId}">${escapeHtml(s.siteName)}</option>`).join("")
    : '<option value="">No online sites — start connector in WizPilot</option>';
  if (previous && online.some((s) => s.siteId === previous))
    sel.value = previous;
  else if (online.length === 1)
    sel.value = online[0].siteId;
}

function siteId() {
  const sel = document.getElementById("site-select");
  const v = sel.value;
  if (!v) {
    const hint = sel.options[sel.selectedIndex]?.text || "";
    if (hint.includes("No online sites"))
      throw new Error("No online site — in WizPilot click Start service + tray, wait 10 seconds, then refresh this page (F5).");
    throw new Error("Select a site in the dropdown at the top of Insight first.");
  }
  return v;
}

function formatJson(text) {
  try { return JSON.stringify(JSON.parse(text), null, 2); } catch { return text; }
}

function showJobResult(outEl, job) {
  outEl.classList.remove("muted");
  if (job.status === 3 || job.status === "Failed") {
    outEl.textContent = `Sage read failed:\n${job.error || "Unknown error"}`;
    return;
  }
  if (!job.resultJson) {
    outEl.textContent = "No data returned from Sage.";
    return;
  }
  outEl.textContent = formatJson(job.resultJson);
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
    showJobResult(out, job);
  } catch (e) { out.textContent = e.message; }
});

async function runWorkspace(op, accountInputId, outId, extraParams = {}) {
  const out = document.getElementById(outId);
  out.textContent = "Loading…";
  const params = { top: "500", ...extraParams };
  const acct = document.getElementById(accountInputId)?.value?.trim();
  if (acct) params.account = acct;
  try {
    const job = await runWait(op, params);
    lastJobId = job.jobId;
    showJobResult(out, job);
  } catch (e) { out.textContent = e.message; }
}

document.querySelectorAll("#panel-ar [data-op]").forEach((btn) => {
  btn.addEventListener("click", () => runWorkspace(btn.dataset.op, "ar-account", "ar-out"));
});
document.querySelectorAll("#panel-ap [data-op]").forEach((btn) => {
  btn.addEventListener("click", () => {
    const extra = {};
    const minBal = document.getElementById("ap-min-balance")?.value?.trim();
    if (minBal) extra.minBalance = minBal;
    runWorkspace(btn.dataset.op, "ap-account", "ap-out", extra);
  });
});
document.querySelectorAll("#panel-inventory [data-op]").forEach((btn) => {
  btn.addEventListener("click", () => {
    const extra = {};
    const minVal = document.getElementById("inv-min-valuation").value.trim();
    if (minVal) extra.minValuation = minVal;
    runWorkspace(btn.dataset.op, "", "inventory-out", extra);
  });
});

document.getElementById("export-ar").addEventListener("click", () => exportLast());
document.getElementById("export-ap").addEventListener("click", () => exportLast());
document.getElementById("export-inventory").addEventListener("click", () => exportLast());

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
    showJobResult(out, job);
  } catch (err) { out.textContent = err.message; }
});

function renderChatGrid(grid) {
  const table = document.getElementById("chat-grid");
  const head = document.getElementById("chat-grid-head");
  const body = document.getElementById("chat-grid-body");
  const empty = document.getElementById("chat-grid-empty");
  const meta = document.getElementById("chat-grid-meta");

  head.innerHTML = "";
  body.innerHTML = "";

  if (!grid || !grid.rows || grid.rows.length === 0) {
    table.classList.add("hidden");
    empty.classList.remove("hidden");
    empty.textContent = "No tabular rows for this answer — see Explanation.";
    meta.textContent = "";
    return;
  }

  const columns = grid.columns && grid.columns.length
    ? grid.columns
    : Object.keys(grid.rows[0]);

  const trHead = document.createElement("tr");
  columns.forEach((col) => {
    const th = document.createElement("th");
    th.textContent = col;
    trHead.appendChild(th);
  });
  head.appendChild(trHead);

  grid.rows.forEach((row) => {
    const tr = document.createElement("tr");
    columns.forEach((col) => {
      const td = document.createElement("td");
      const val = row[col] ?? row[col.toLowerCase()] ?? "";
      td.textContent = val == null ? "" : String(val);
      tr.appendChild(td);
    });
    body.appendChild(tr);
  });

  table.classList.remove("hidden");
  empty.classList.add("hidden");
  meta.textContent = `${grid.rows.length} row(s)`;
}

function setChatExplanation(text) {
  const el = document.getElementById("chat-explanation");
  el.value = text || "";
}

function setupSpeechInput() {
  const micBtn = document.getElementById("chat-mic");
  const status = document.getElementById("chat-mic-status");
  const query = document.getElementById("chat-query");
  const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;

  if (!SpeechRecognition) {
    micBtn.disabled = true;
    status.textContent = "Voice input needs Chrome or Edge (Speech Recognition not available in this browser).";
    return;
  }

  const recognition = new SpeechRecognition();
  recognition.continuous = false;
  recognition.interimResults = true;
  recognition.lang = "en-ZA";

  let listening = false;

  micBtn.addEventListener("click", () => {
    if (listening) {
      recognition.stop();
      return;
    }
    try {
      recognition.start();
    } catch {
      status.textContent = "Could not start microphone — check browser permissions.";
    }
  });

  recognition.onstart = () => {
    listening = true;
    micBtn.classList.add("listening");
    micBtn.textContent = "⏹ Stop";
    status.textContent = "Listening… speak your question.";
  };

  recognition.onend = () => {
    listening = false;
    micBtn.classList.remove("listening");
    micBtn.textContent = "🎤 Mic";
    status.textContent = "Done. Edit the text if needed, then Run query.";
  };

  recognition.onerror = (e) => {
    status.textContent = e.error === "not-allowed"
      ? "Microphone permission denied."
      : `Voice error: ${e.error}`;
  };

  recognition.onresult = (e) => {
    let transcript = "";
    for (let i = e.resultIndex; i < e.results.length; i++) {
      transcript += e.results[i][0].transcript;
    }
    query.value = (query.value ? query.value + " " : "") + transcript.trim();
  };
}

document.getElementById("chat-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const queryEl = document.getElementById("chat-query");
  const message = queryEl.value.trim();
  if (!message) return;

  const runBtn = document.getElementById("chat-run");
  runBtn.disabled = true;
  setChatExplanation("Running query against Sage…");
  renderChatGrid(null);
  document.getElementById("chat-grid-empty").classList.remove("hidden");
  document.getElementById("chat-grid-empty").textContent = "Loading…";

  try {
    await loadSites();
    const res = await json(`${api}/api/insight/chat`, {
      method: "POST",
      body: JSON.stringify({ siteId: siteId(), conversationId, message }),
    });
    conversationId = res.conversationId;

    let explanation = res.explanation || res.reply || "";
    if (res.insightChatVersion && res.insightChatVersion !== EXPECTED_INSIGHT_CHAT) {
      explanation += `\n\n[Stale API: chat ${res.insightChatVersion}, need ${EXPECTED_INSIGHT_CHAT}. WizPilot → Restart local API.]`;
    } else if (explanation.includes("open items for CASH")) {
      explanation += "\n\n[Stale API — restart local API from WizPilot or scripts/restart-local-api.ps1]";
    }

    setChatExplanation(explanation);
    renderChatGrid(res.grid);
  } catch (err) {
    setChatExplanation(err.message);
    renderChatGrid(null);
    document.getElementById("chat-grid-empty").textContent = "Query failed.";
  } finally {
    runBtn.disabled = false;
  }
});

setupSpeechInput();

function escapeHtml(s) {
  return String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
}

loadSites();
loadApiVersion();
setInterval(loadSites, 20000);
setInterval(loadApiVersion, 20000);
