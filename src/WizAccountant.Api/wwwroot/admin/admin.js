const api = window.location.origin;

async function json(url, options) {
  const res = await fetch(url, {
    headers: { Accept: "application/json", "Content-Type": "application/json" },
    ...options,
  });
  const text = await res.text();
  let body = text;
  try { body = text ? JSON.parse(text) : null; } catch { /* plain text */ }
  if (!res.ok) {
    const msg = body?.error || body?.title || body?.detail || body?.message || body || res.statusText;
    throw new Error(typeof msg === "string" ? msg : JSON.stringify(msg));
  }
  return body;
}

function formatUtc(iso) {
  if (!iso) return "—";
  return new Date(iso).toISOString().replace("T", " ").slice(0, 19);
}

function setActionStatus(message, isError) {
  const el = document.getElementById("action-status");
  el.textContent = message;
  el.classList.toggle("error", !!isError);
  el.classList.remove("muted");
}

async function postAdminAction(path, busyLabel) {
  setActionStatus(busyLabel, false);
  try {
    const data = await json(`${api}${path}`, { method: "POST", body: "{}" });
    const parts = [];
    if (data.started?.length) parts.push(`Started: ${data.started.join(", ")}`);
    if (data.alreadyRunning?.length) parts.push(`Already running: ${data.alreadyRunning.join(", ")}`);
    setActionStatus(data.message || parts.join(". ") || "Done.", !data.ok);
    if (data.ok) await loadSites();
  } catch (err) {
    setActionStatus(err.message, true);
  }
}

document.getElementById("start-local").addEventListener("click", () =>
  postAdminAction("/api/admin/start-local-programs", "Starting connector programs…"));

document.getElementById("open-sage-setup").addEventListener("click", () =>
  postAdminAction("/api/admin/open-sage-setup", "Opening Sage setup…"));

document.getElementById("refresh-sites-top").addEventListener("click", loadSites);

document.getElementById("pairing-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const fd = new FormData(e.target);
  const result = document.getElementById("pairing-result");
  result.classList.remove("hidden");
  result.textContent = "Generating…";
  try {
    const data = await json(`${api}/api/pairing-codes`, {
      method: "POST",
      body: JSON.stringify({
        tenantId: fd.get("tenantId"),
        siteName: fd.get("siteName"),
        expiresInMinutes: Number(fd.get("expiresInMinutes")) || 30,
      }),
    });
    result.innerHTML = `<strong>Pairing code:</strong> ${data.pairingCode}<br><strong>Expires (UTC):</strong> ${formatUtc(data.expiresAtUtc)}<br><br>Enter this code in the WizConnector icon near the clock (system tray).`;
    await loadSites();
    await loadAudit();
  } catch (err) {
    result.textContent = `Error: ${err.message}`;
  }
});

async function loadSites() {
  const tbody = document.getElementById("sites-body");
  try {
    const sites = await json(`${api}/api/sites`);
    if (!sites.length) {
      tbody.innerHTML = '<tr><td colspan="5" class="muted">No sites yet. Create a pairing code.</td></tr>';
      return;
    }
    tbody.innerHTML = sites.map((s) => `
      <tr>
        <td><span class="badge ${s.isOnline ? "online" : "offline"}">${s.isOnline ? "Online" : "Offline"}</span></td>
        <td>${escapeHtml(s.siteName)}<br><span class="muted" style="font-size:0.75rem">${s.siteId}</span></td>
        <td>${escapeHtml(s.deviceId)}</td>
        <td>${formatUtc(s.lastSeenUtc)}</td>
        <td>
          <button type="button" class="small test-btn" data-site-id="${s.siteId}" data-online="${s.isOnline}" ${s.isOnline ? "" : "disabled"}>Test connection</button>
        </td>
      </tr>
    `).join("");
    tbody.querySelectorAll(".test-btn").forEach((btn) => {
      btn.addEventListener("click", () => runTest(btn.dataset.siteId, btn));
    });
  } catch (err) {
    tbody.innerHTML = `<tr><td colspan="5">Error: ${escapeHtml(err.message)}</td></tr>`;
  }
}

function escapeHtml(s) {
  return String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
}

async function runTest(siteId, btn) {
  const out = document.getElementById("test-output");
  out.classList.remove("muted");
  if (btn) btn.disabled = true;
  out.textContent = "Testing connection (may take up to 60 seconds)…";
  try {
    const job = await json(`${api}/api/sites/${siteId}/test-connection`, {
      method: "POST",
      body: "{}",
    });
    if (job.status === 3 || job.status === "Failed") {
      out.textContent = `Failed: ${job.error || "unknown"}`;
    } else {
      try {
        out.textContent = JSON.stringify(JSON.parse(job.resultJson || "{}"), null, 2);
      } catch {
        out.textContent = job.resultJson || "Completed.";
      }
    }
    await loadAudit();
  } catch (err) {
    out.textContent = err.message.includes("respond in time")
      ? `${err.message}\n\nClick “Start connector on this PC” above, wait a few seconds, refresh Sites, then try again.`
      : `Error: ${err.message}`;
  } finally {
    if (btn) btn.disabled = false;
  }
}

async function loadAudit() {
  const tbody = document.getElementById("audit-body");
  try {
    const rows = await json(`${api}/api/audit/jobs?take=50`);
    if (!rows.length) {
      tbody.innerHTML = '<tr><td colspan="5" class="muted">No activity yet.</td></tr>';
      return;
    }
    tbody.innerHTML = rows.map((a) => {
      const result =
        a.success === true ? '<span class="badge online">OK</span>' :
        a.success === false ? '<span class="badge offline">Fail</span>' :
        '<span class="muted">—</span>';
      return `
        <tr>
          <td>${formatUtc(a.timestampUtc)}</td>
          <td>${escapeHtml(a.siteName || a.siteId)}</td>
          <td>${escapeHtml(a.operation)}</td>
          <td>${escapeHtml(a.eventType)}</td>
          <td>${result}${a.detail ? `<br><span class="muted" style="font-size:0.75rem">${escapeHtml(a.detail)}</span>` : ""}</td>
        </tr>`;
    }).join("");
  } catch (err) {
    tbody.innerHTML = `<tr><td colspan="5">Error: ${escapeHtml(err.message)}</td></tr>`;
  }
}

document.getElementById("refresh-sites").addEventListener("click", loadSites);
document.getElementById("refresh-audit").addEventListener("click", loadAudit);

loadSites();
loadAudit();
setInterval(loadSites, 15000);
setInterval(loadAudit, 20000);
