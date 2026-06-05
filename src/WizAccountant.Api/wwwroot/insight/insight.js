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

const EXPECTED_INSIGHT_CHAT = "2026-06-12-feedback-validation";

let lastQueryLogId = null;
let feedbackLocked = false;

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

function renderGrid(grid, ids = {}) {
  const table = document.getElementById(ids.table || "chat-grid");
  const head = document.getElementById(ids.head || "chat-grid-head");
  const body = document.getElementById(ids.body || "chat-grid-body");
  const empty = document.getElementById(ids.empty || "chat-grid-empty");
  const meta = document.getElementById(ids.meta || "chat-grid-meta");
  const emptyText = ids.emptyText || "No tabular rows for this answer — see Explanation.";
  const sortable = ids.sortable === true;

  if (!table || !head || !body || !empty) return;

  head.innerHTML = "";
  body.innerHTML = "";

  if (!grid || !grid.rows || grid.rows.length === 0) {
    table.classList.add("hidden");
    empty.classList.remove("hidden");
    empty.textContent = emptyText;
    if (meta) meta.textContent = "";
    return;
  }

  let rows = grid.rows.slice();
  const columns = grid.columns && grid.columns.length
    ? grid.columns
    : Object.keys(grid.rows[0]);

  const sortState = sortable ? (window.__sqlGridSort || { col: null, dir: "asc" }) : null;
  if (sortState?.col && columns.includes(sortState.col)) {
    const col = sortState.col;
    const dir = sortState.dir === "desc" ? -1 : 1;
    rows.sort((a, b) => {
      const av = a[col] ?? a[col.toLowerCase()] ?? "";
      const bv = b[col] ?? b[col.toLowerCase()] ?? "";
      const an = parseFloat(av);
      const bn = parseFloat(bv);
      if (!Number.isNaN(an) && !Number.isNaN(bn) && String(av).trim() !== "" && String(bv).trim() !== "")
        return (an - bn) * dir;
      return String(av).localeCompare(String(bv), undefined, { numeric: true, sensitivity: "base" }) * dir;
    });
  }

  const trHead = document.createElement("tr");
  columns.forEach((col) => {
    const th = document.createElement("th");
    th.textContent = col;
    if (sortable) {
      th.classList.add("sortable");
      if (sortState?.col === col)
        th.classList.add(sortState.dir === "desc" ? "sort-desc" : "sort-asc");
      th.addEventListener("click", () => {
        const next = window.__sqlGridSort?.col === col && window.__sqlGridSort.dir === "asc" ? "desc" : "asc";
        window.__sqlGridSort = { col, dir: next };
        renderGrid(grid, ids);
      });
    }
    trHead.appendChild(th);
  });
  head.appendChild(trHead);

  rows.forEach((row) => {
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
  if (meta) meta.textContent = `${rows.length} row(s)`;
}

function renderChatGrid(grid) {
  renderGrid(grid);
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
  resetFeedbackBar();
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
    if (res.queryLogId) showFeedbackBar(res.queryLogId);
  } catch (err) {
    const msg = SafeExecutionBoundaryLooksLikeStack(err.message)
      ? "The request could not be completed. Check that the API is running and try again."
      : err.message;
    setChatExplanation(msg);
    renderChatGrid(null);
    document.getElementById("chat-grid-empty").textContent = "Query failed.";
  } finally {
    runBtn.disabled = false;
  }
});

function resetFeedbackBar() {
  feedbackLocked = false;
  lastQueryLogId = null;
  const bar = document.getElementById("chat-feedback");
  const wrongPanel = document.getElementById("chat-feedback-wrong-panel");
  const status = document.getElementById("chat-feedback-status");
  bar?.classList.add("hidden");
  wrongPanel?.classList.add("hidden");
  if (status) status.textContent = "";
  document.querySelectorAll(".feedback-btn").forEach((b) => {
    b.disabled = false;
    b.classList.remove("submitted");
  });
  const note = document.getElementById("chat-feedback-note");
  const reason = document.getElementById("chat-feedback-reason");
  if (note) note.value = "";
  if (reason) reason.value = "";
}

function showFeedbackBar(queryLogId) {
  if (!queryLogId) return;
  lastQueryLogId = queryLogId;
  const bar = document.getElementById("chat-feedback");
  bar?.classList.remove("hidden");
}

async function submitFeedback(rating, reason, note) {
  if (!lastQueryLogId || feedbackLocked) return;
  const status = document.getElementById("chat-feedback-status");
  try {
    const body = await json(`${api}/api/insight/feedback`, {
      method: "POST",
      body: JSON.stringify({
        queryLogId: lastQueryLogId,
        rating,
        reason: reason || null,
        note: note || null,
      }),
    });
    feedbackLocked = true;
    document.querySelectorAll(".feedback-btn").forEach((b) => {
      b.disabled = true;
      if (b.dataset.rating === rating) b.classList.add("submitted");
    });
    document.getElementById("chat-feedback-wrong-panel")?.classList.add("hidden");
    if (status) {
      status.textContent = body.duplicate
        ? "Feedback already recorded — thank you."
        : "Thank you — feedback saved.";
    }
  } catch (e) {
    if (status) status.textContent = `Could not save feedback: ${e.message}`;
  }
}

function setupFeedbackUi() {
  document.querySelectorAll(".feedback-btn").forEach((btn) => {
    btn.addEventListener("click", () => {
      if (feedbackLocked || !lastQueryLogId) return;
      const rating = btn.dataset.rating;
      if (rating === "wrong") {
        document.getElementById("chat-feedback-wrong-panel")?.classList.remove("hidden");
        return;
      }
      submitFeedback(rating === "helpful" ? "helpful" : "needs_improvement", null, null);
    });
  });
  document.getElementById("chat-feedback-submit-wrong")?.addEventListener("click", () => {
    const reason = document.getElementById("chat-feedback-reason")?.value || "";
    const note = document.getElementById("chat-feedback-note")?.value?.trim() || "";
    submitFeedback("wrong", reason || null, note || null);
  });
}

setupSpeechInput();
setupFeedbackUi();
setupSqlTab();

const SQL_SAVED_KEY = "wiz-insight-saved-sql-v1";
const SQL_SAVED_IMPORTED_KEY = "wiz-insight-saved-sql-imported-v1";
const sqlGridIds = {
  table: "sql-grid",
  head: "sql-grid-head",
  body: "sql-grid-body",
  empty: "sql-grid-empty",
  meta: "sql-grid-meta",
  sortable: true,
};

let cachedSavedSqlQueries = [];

function loadLocalSavedSqlQueries() {
  try {
    const raw = localStorage.getItem(SQL_SAVED_KEY);
    return raw ? JSON.parse(raw) : [];
  } catch {
    return [];
  }
}

function refreshSavedSqlSelect(selectedId) {
  const sel = document.getElementById("sql-saved-select");
  if (!sel) return;
  const items = cachedSavedSqlQueries.slice().sort((a, b) => (b.updatedAtUtc || "").localeCompare(a.updatedAtUtc || ""));
  sel.innerHTML = '<option value="">— Select a saved query —</option>'
    + items.map((q) => `<option value="${escapeHtml(q.queryId)}">${escapeHtml(q.title || "Untitled")}</option>`).join("");
  if (selectedId) sel.value = selectedId;
}

async function maybeImportLocalSavedQueries(siteIdValue) {
  if (localStorage.getItem(SQL_SAVED_IMPORTED_KEY) === "1") return false;
  const local = loadLocalSavedSqlQueries();
  if (!local.length || cachedSavedSqlQueries.length > 0) {
    localStorage.setItem(SQL_SAVED_IMPORTED_KEY, "1");
    return false;
  }
  for (const item of local) {
    if (!item.title || !item.sql) continue;
    await json(`${api}/api/insight/sql/saved`, {
      method: "POST",
      body: JSON.stringify({
        siteId: siteIdValue,
        title: item.title,
        aiPrompt: item.aiPrompt || "",
        sql: item.sql,
      }),
    });
  }
  localStorage.removeItem(SQL_SAVED_KEY);
  localStorage.setItem(SQL_SAVED_IMPORTED_KEY, "1");
  return true;
}

async function refreshSavedSqlQueriesFromServer(selectedId) {
  const status = document.getElementById("sql-saved-status");
  try {
    await loadSites();
    const sid = siteId();
    cachedSavedSqlQueries = await json(`${api}/api/insight/sql/saved?siteId=${encodeURIComponent(sid)}`);
    if (await maybeImportLocalSavedQueries(sid)) {
      cachedSavedSqlQueries = await json(`${api}/api/insight/sql/saved?siteId=${encodeURIComponent(sid)}`);
      if (status) status.textContent = "Imported browser saved queries to the server.";
    }
    refreshSavedSqlSelect(selectedId);
  } catch (err) {
    cachedSavedSqlQueries = [];
    refreshSavedSqlSelect();
    if (status && !String(err.message || "").includes("Select a site")) status.textContent = "";
  }
}

function setupSqlTab() {
  const form = document.getElementById("sql-form");
  if (!form) return;

  refreshSavedSqlQueriesFromServer();

  document.getElementById("site-select")?.addEventListener("change", () => {
    refreshSavedSqlQueriesFromServer();
  });

  document.getElementById("sql-saved-save")?.addEventListener("click", async () => {
    const title = document.getElementById("sql-saved-title")?.value?.trim();
    const prompt = document.getElementById("sql-saved-prompt")?.value?.trim() || "";
    const sql = document.getElementById("sql-text")?.value?.trim();
    const status = document.getElementById("sql-saved-status");
    const saveBtn = document.getElementById("sql-saved-save");
    if (!title) {
      if (status) status.textContent = "Enter a title before saving.";
      return;
    }
    if (!sql) {
      if (status) status.textContent = "Enter SQL in the editor before saving.";
      return;
    }
    saveBtn.disabled = true;
    try {
      await loadSites();
      const existingId = document.getElementById("sql-saved-select")?.value || null;
      const saved = await json(`${api}/api/insight/sql/saved`, {
        method: "POST",
        body: JSON.stringify({
          queryId: existingId || null,
          siteId: siteId(),
          title,
          aiPrompt: prompt,
          sql,
        }),
      });
      await refreshSavedSqlQueriesFromServer(saved.queryId);
      if (status) status.textContent = `Saved “${title}” to server.`;
    } catch (err) {
      if (status) status.textContent = err.message;
    } finally {
      saveBtn.disabled = false;
    }
  });

  document.getElementById("sql-saved-load")?.addEventListener("click", () => {
    const id = document.getElementById("sql-saved-select")?.value;
    const status = document.getElementById("sql-saved-status");
    if (!id) {
      if (status) status.textContent = "Select a saved query first.";
      return;
    }
    const item = cachedSavedSqlQueries.find((q) => q.queryId === id);
    if (!item) {
      if (status) status.textContent = "Saved query not found.";
      return;
    }
    document.getElementById("sql-saved-title").value = item.title || "";
    document.getElementById("sql-saved-prompt").value = item.aiPrompt || "";
    document.getElementById("sql-text").value = item.sql || "";
    if (status) status.textContent = `Loaded “${item.title}”.`;
  });

  document.getElementById("sql-saved-delete")?.addEventListener("click", async () => {
    const id = document.getElementById("sql-saved-select")?.value;
    const status = document.getElementById("sql-saved-status");
    const delBtn = document.getElementById("sql-saved-delete");
    if (!id) {
      if (status) status.textContent = "Select a saved query to delete.";
      return;
    }
    delBtn.disabled = true;
    try {
      await loadSites();
      await json(`${api}/api/insight/sql/saved/${encodeURIComponent(id)}?siteId=${encodeURIComponent(siteId())}`, {
        method: "DELETE",
      });
      document.getElementById("sql-saved-title").value = "";
      document.getElementById("sql-saved-prompt").value = "";
      await refreshSavedSqlQueriesFromServer();
      if (status) status.textContent = "Deleted from server.";
    } catch (err) {
      if (status) status.textContent = err.message;
    } finally {
      delBtn.disabled = false;
    }
  });

  document.getElementById("sql-saved-select")?.addEventListener("change", (e) => {
    const id = e.target.value;
    if (!id) return;
    const item = cachedSavedSqlQueries.find((q) => q.queryId === id);
    if (!item) return;
    document.getElementById("sql-saved-title").value = item.title || "";
    document.getElementById("sql-saved-prompt").value = item.aiPrompt || "";
  });

  document.getElementById("sql-schema-hint")?.addEventListener("click", async () => {
    const status = document.getElementById("sql-status");
    const out = document.getElementById("sql-schema-out");
    const btn = document.getElementById("sql-schema-hint");
    btn.disabled = true;
    if (status) status.textContent = "Reading _btblInvoiceLines schema from Sage…";
    if (out) out.classList.add("hidden");
    try {
      await loadSites();
      const hint = await json(`${api}/api/insight/sql/invoice-lines-hint?siteId=${encodeURIComponent(siteId())}`);
      const lines = [
        `Table: ${hint.tableName}`,
        `Qty column: ${hint.qtyColumn}`,
        `Value source: ${hint.valueSource}`,
        `Use for SUM(qty): ${hint.qtyExpression}`,
        `Use for SUM(value): ${hint.valueExpression}`,
        "",
        "Sample product-by-month SQL (schema-aware) — loaded into editor.",
      ];
      if (out) {
        out.textContent = lines.join("\n");
        out.classList.remove("hidden");
      }
      if (hint.sampleProductMonthlySql)
        document.getElementById("sql-text").value = hint.sampleProductMonthlySql.trim();
      if (status) status.textContent = "Schema hint applied — review SQL then Run SQL.";
    } catch (err) {
      if (status) status.textContent = err.message;
    } finally {
      btn.disabled = false;
    }
  });

  form.addEventListener("submit", async (e) => {
    e.preventDefault();
    const sql = document.getElementById("sql-text")?.value?.trim();
    const status = document.getElementById("sql-status");
    const runBtn = document.getElementById("sql-run");
    if (!sql) {
      if (status) status.textContent = "Enter a SELECT query.";
      return;
    }

    const maxRows = parseInt(document.getElementById("sql-max-rows")?.value || "500", 10);
    runBtn.disabled = true;
    window.__sqlGridSort = null;
    if (status) status.textContent = "Running on Sage company database…";
    renderGrid(null, { ...sqlGridIds, emptyText: "Running…" });

    try {
      await loadSites();
      const res = await json(`${api}/api/insight/sql`, {
        method: "POST",
        body: JSON.stringify({
          siteId: siteId(),
          sql,
          maxRows: Number.isFinite(maxRows) ? maxRows : 500,
        }),
      });
      window.__lastSqlGrid = res.grid;
      renderGrid(res.grid, { ...sqlGridIds, emptyText: "Query returned no rows." });
      const meta = document.getElementById("sql-grid-meta");
      if (meta && res.message) meta.textContent = res.message;
      if (status) status.textContent = res.truncated ? "Results truncated — increase filters or max rows." : "Done.";
      if (res.jobId) lastJobId = res.jobId;
    } catch (err) {
      const msg = err.message || "";
      if (/fLineTotal|fLineTot|fQuantity/i.test(msg)) {
        if (status) {
          status.textContent = `${msg} — Click “Invoice line schema” for column names that work on your Sage database.`;
        }
      } else if (status) {
        status.textContent = msg;
      }
      renderGrid(null, { ...sqlGridIds, emptyText: "Query failed." });
    } finally {
      runBtn.disabled = false;
    }
  });
}

function SafeExecutionBoundaryLooksLikeStack(msg) {
  return /Exception:|StackTrace|HEADERS|at Microsoft\.|at System\./i.test(msg || "");
}

function escapeHtml(s) {
  return String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
}

loadSites();
loadApiVersion();
setInterval(loadSites, 20000);
setInterval(loadApiVersion, 20000);
