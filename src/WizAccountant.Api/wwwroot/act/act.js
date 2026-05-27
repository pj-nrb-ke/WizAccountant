const api = window.location.origin;
const preparerId = "33333333-3333-3333-3333-333333333333";
const approverId = "22222222-2222-2222-2222-222222222222";

async function json(url, options) {
  const res = await fetch(url, {
    headers: { Accept: "application/json", "Content-Type": "application/json" },
    ...options,
  });
  const text = await res.text();
  let body = text;
  try { body = text ? JSON.parse(text) : null; } catch { /* */ }
  if (!res.ok) throw new Error(body?.error || body?.message || body || res.statusText);
  return body;
}

function siteId() {
  const v = document.getElementById("site-select").value;
  if (!v) throw new Error("Select a site.");
  return v;
}

function userId() {
  return document.getElementById("user-select").value;
}

function escapeHtml(s) {
  return String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
}

async function loadSites() {
  const sel = document.getElementById("site-select");
  const sites = await json(`${api}/api/sites`);
  sel.innerHTML = sites.map((s) =>
    `<option value="${s.siteId}">${escapeHtml(s.siteName)} ${s.isOnline ? "●" : "○"}</option>`).join("");
}

document.querySelectorAll(".tab").forEach((tab) => {
  tab.addEventListener("click", () => {
    document.querySelectorAll(".tab").forEach((t) => t.classList.remove("active"));
    document.querySelectorAll(".panel").forEach((p) => p.classList.remove("active"));
    tab.classList.add("active");
    document.getElementById(`panel-${tab.dataset.tab}`).classList.add("active");
  });
});

async function loadInbox() {
  const el = document.getElementById("inbox-list");
  el.textContent = "Loading…";
  try {
    const pending = await json(`${api}/api/act/proposals?siteId=${siteId()}&status=0`);
    if (!pending.length) {
      el.innerHTML = '<p class="muted">No pending proposals.</p>';
      return;
    }
    el.innerHTML = pending.map((p) => `
      <div class="proposal" data-id="${p.proposalId}">
        <h3>${escapeHtml(p.title)}</h3>
        <div class="meta">${escapeHtml(p.proposalType)} · ${escapeHtml(p.preparedByName)} · ${new Date(p.createdAtUtc).toLocaleString()}</div>
        <pre class="small">${escapeHtml(p.payloadJson)}</pre>
        <div class="actions">
          <button type="button" class="approve-btn primary">Approve &amp; post</button>
          <button type="button" class="reject-btn secondary">Reject</button>
        </div>
      </div>`).join("");

    el.querySelectorAll(".approve-btn").forEach((btn) => {
      btn.addEventListener("click", async (e) => {
        const id = e.target.closest(".proposal").dataset.id;
        if (!confirm("Post this to live Sage?")) return;
        try {
          await json(`${api}/api/act/proposals/${id}/approve`, {
            method: "POST",
            body: JSON.stringify({ approverUserId: userId(), comment: "Approved from Act UI" }),
          });
          await loadInbox();
          await loadAudit();
        } catch (err) { alert(err.message); }
      });
    });

    el.querySelectorAll(".reject-btn").forEach((btn) => {
      btn.addEventListener("click", async (e) => {
        const id = e.target.closest(".proposal").dataset.id;
        const reason = prompt("Reject reason:");
        if (!reason) return;
        try {
          await json(`${api}/api/act/proposals/${id}/reject`, {
            method: "POST",
            body: JSON.stringify({ approverUserId: userId(), reason }),
          });
          await loadInbox();
        } catch (err) { alert(err.message); }
      });
    });
  } catch (err) {
    el.textContent = err.message;
  }
}

document.getElementById("refresh-inbox").addEventListener("click", loadInbox);

document.getElementById("propose-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const fd = new FormData(e.target);
  const out = document.getElementById("propose-result");
  out.textContent = "Submitting…";
  try {
    const res = await json(`${api}/api/act/proposals`, {
      method: "POST",
      body: JSON.stringify({
        siteId: siteId(),
        preparedByUserId: userId(),
        proposalType: fd.get("proposalType"),
        title: fd.get("title"),
        payloadJson: fd.get("payloadJson"),
      }),
    });
    out.textContent = `Submitted: ${res.proposalId}\nStatus: ${res.status}`;
    await loadInbox();
  } catch (err) { out.textContent = err.message; }
});

document.getElementById("ai-draft").addEventListener("click", async () => {
  const title = document.querySelector('[name="title"]').value || "Draft";
  const msg = prompt("Describe the posting (e.g. 'GL journal 500 debit to 1000'):", title);
  if (!msg) return;
  const draft = await json(`${api}/api/act/ai-draft`, {
    method: "POST",
    body: JSON.stringify({ siteId: siteId(), message: msg }),
  });
  document.querySelector('[name="proposalType"]').value = draft.proposalType;
  document.querySelector('[name="title"]').value = draft.title;
  document.querySelector('[name="payloadJson"]').value = JSON.stringify(JSON.parse(draft.payloadJson), null, 2);
});

async function loadAudit() {
  const out = document.getElementById("audit-out");
  out.textContent = "Loading…";
  try {
    const rows = await json(`${api}/api/act/write-audit?siteId=${siteId()}`);
    out.textContent = JSON.stringify(rows, null, 2);
  } catch (err) { out.textContent = err.message; }
}

document.getElementById("refresh-audit").addEventListener("click", loadAudit);

async function loadWorkflows() {
  const el = document.getElementById("workflows-list");
  const rows = await json(`${api}/api/act/workflows`);
  el.innerHTML = rows.map((w) => `
    <div class="proposal">
      <h3>${escapeHtml(w.name)}</h3>
      <ol>${w.steps.map((s) => `<li>${escapeHtml(s)}</li>`).join("")}</ol>
    </div>`).join("");
}

document.getElementById("sync-config").addEventListener("click", async () => {
  const out = document.getElementById("config-out");
  out.textContent = "Syncing…";
  try {
    const cfg = await json(`${api}/api/act/sites/${siteId()}/sync-config`, { method: "POST", body: "{}" });
    out.textContent = JSON.stringify(cfg, null, 2);
  } catch (err) { out.textContent = err.message; }
});

loadSites().then(() => {
  loadInbox();
  loadAudit();
  loadWorkflows();
});
