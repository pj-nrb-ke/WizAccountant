/** Shared Insight shell — site selector + left navigation */
(function () {
  const api = window.location.origin;

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

  function escapeHtml(s) {
    return String(s ?? "").replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
  }

  window.WizShell = {
    api,
    json,
    escapeHtml,
    siteId() {
      const sel = document.getElementById("site-select");
      if (!sel?.value) throw new Error("Select an online site in the left panel first.");
      return sel.value;
    },
    async runWait(operation, parameters = {}) {
      return json(`${api}/api/jobs/run-wait`, {
        method: "POST",
        body: JSON.stringify({
          siteId: WizShell.siteId(),
          operation,
          parameters,
          requestedBy: "insight-ui",
          timeoutSeconds: 90,
        }),
      });
    },
    async runSql(sql, maxRows = 500) {
      return json(`${api}/api/insight/sql`, {
        method: "POST",
        body: JSON.stringify({ siteId: WizShell.siteId(), sql, maxRows }),
      });
    },
    parseJobItems(job) {
      if (!job?.resultJson) return [];
      try {
        const root = JSON.parse(job.resultJson);
        return root.items ?? root.rows ?? [];
      } catch {
        return [];
      }
    },
    fillSelect(selectEl, items, valueKey, labelFn, placeholder) {
      if (!selectEl) return;
      const opts = [`<option value="">${escapeHtml(placeholder || "— Select —")}</option>`];
      for (const item of items) {
        const val = item[valueKey] ?? item.code ?? "";
        if (!val) continue;
        opts.push(`<option value="${escapeHtml(val)}">${escapeHtml(labelFn(item))}</option>`);
      }
      selectEl.innerHTML = opts.join("");
    },
    async loadSites() {
      const sel = document.getElementById("site-select");
      if (!sel) return;
      const previous = sel.value;
      const sites = await json(`${api}/api/sites`);
      const online = sites.filter((s) => s.isOnline);
      sel.innerHTML = online.length
        ? online.map((s) => `<option value="${s.siteId}">${escapeHtml(s.siteName)}</option>`).join("")
        : '<option value="">No online sites</option>';
      if (previous && online.some((s) => s.siteId === previous)) sel.value = previous;
      else if (online.length === 1) sel.value = online[0].siteId;
    },
    markActiveNav() {
      const path = window.location.pathname.toLowerCase();
      document.querySelectorAll(".nav-link[data-nav]").forEach((a) => {
        const target = (a.getAttribute("data-nav") || "").toLowerCase();
        a.classList.toggle("active", target && path.includes(target));
      });
    },
    initSideNavToggle() {
      const shell = document.querySelector(".app-shell");
      const btn = document.getElementById("side-nav-toggle");
      if (!shell || !btn) return;

      const key = "wiz-side-nav-collapsed";
      const apply = (collapsed) => {
        shell.classList.toggle("side-nav-collapsed", collapsed);
        btn.textContent = collapsed ? "▶" : "◀";
        btn.title = collapsed ? "Show side panel" : "Hide side panel";
        btn.setAttribute("aria-expanded", collapsed ? "false" : "true");
      };

      apply(localStorage.getItem(key) === "1");
      btn.addEventListener("click", () => {
        const collapsed = !shell.classList.contains("side-nav-collapsed");
        apply(collapsed);
        localStorage.setItem(key, collapsed ? "1" : "0");
      });
    },
  };

  document.addEventListener("DOMContentLoaded", () => {
    WizShell.markActiveNav();
    WizShell.initSideNavToggle();
    WizShell.loadSites().catch(() => {
      const sel = document.getElementById("site-select");
      if (sel) sel.innerHTML = '<option value="">API offline</option>';
    });
  });
})();
