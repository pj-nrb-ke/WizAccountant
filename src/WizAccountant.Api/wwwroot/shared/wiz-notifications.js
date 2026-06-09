/**
 * WizAccountant — Real-time UI Notification Client  (B5-B / L1)
 * Connects to the UiNotificationHub SignalR hub at /hubs/ui.
 *
 * Usage: include AFTER the page's main script.
 *   <script src="https://unpkg.com/@microsoft/signalr@8.0.0/dist/browser/signalr.min.js"></script>
 *   <script src="/shared/wiz-notifications.js"></script>
 *
 * The script is self-bootstrapping — it reads `wiz_token` from localStorage,
 * connects to /hubs/ui, and injects a toast container into the page body.
 *
 * Events handled:
 *   notification: { event, siteId, jobId, proposalId, message, timestampUtc }
 *     - job-completed / job-failed  → green / red toast
 *     - approval-required           → yellow banner with link to /act
 *     - alert-threshold-hit         → orange alert
 */
(function () {
  'use strict';

  // ── Toast container ───────────────────────────────────────────────────────
  const CONTAINER_ID = 'wiz-toast-container';

  function ensureContainer() {
    if (document.getElementById(CONTAINER_ID)) return;
    const style = document.createElement('style');
    style.textContent = `
      #wiz-toast-container {
        position: fixed; bottom: 1.5rem; right: 1.5rem;
        z-index: 9999; display: flex; flex-direction: column; gap: 0.5rem;
        max-width: 360px;
      }
      .wiz-toast {
        padding: 0.75rem 1rem; border-radius: 8px; box-shadow: 0 4px 14px rgba(0,0,0,.18);
        font-size: 0.875rem; display: flex; align-items: flex-start; gap: 0.6rem;
        animation: wiz-slide-in 0.25s ease; cursor: pointer;
      }
      .wiz-toast.success  { background: #dcfce7; color: #15803d; border-left: 4px solid #22c55e; }
      .wiz-toast.error    { background: #fee2e2; color: #b91c1c; border-left: 4px solid #ef4444; }
      .wiz-toast.warning  { background: #fef9c3; color: #854d0e; border-left: 4px solid #eab308; }
      .wiz-toast.info     { background: #dbeafe; color: #1e40af; border-left: 4px solid #3b82f6; }
      .wiz-toast-icon     { font-size: 1.1rem; flex-shrink: 0; }
      .wiz-toast-body     { flex: 1; }
      .wiz-toast-title    { font-weight: 700; }
      .wiz-toast-msg      { margin-top: 2px; color: inherit; opacity: 0.85; }
      .wiz-toast-close    { opacity: 0.5; font-size: 1rem; cursor: pointer; padding: 0 4px; }
      .wiz-toast-close:hover { opacity: 1; }
      @keyframes wiz-slide-in {
        from { transform: translateX(120%); opacity: 0; }
        to   { transform: translateX(0);    opacity: 1; }
      }
    `;
    document.head.appendChild(style);
    const container = document.createElement('div');
    container.id = CONTAINER_ID;
    document.body.appendChild(container);
  }

  function showToast(title, message, type, link, ttl) {
    ensureContainer();
    const icons = { success: '✅', error: '❌', warning: '⚠️', info: 'ℹ️' };
    const toast = document.createElement('div');
    toast.className = `wiz-toast ${type}`;
    toast.innerHTML = `
      <span class="wiz-toast-icon">${icons[type] || 'ℹ️'}</span>
      <div class="wiz-toast-body">
        <div class="wiz-toast-title">${title}</div>
        <div class="wiz-toast-msg">${message}</div>
      </div>
      <span class="wiz-toast-close" title="Dismiss">✕</span>
    `;
    if (link) toast.addEventListener('click', () => { window.location.href = link; });
    toast.querySelector('.wiz-toast-close').addEventListener('click', e => {
      e.stopPropagation(); toast.remove();
    });
    document.getElementById(CONTAINER_ID).appendChild(toast);
    setTimeout(() => { if (toast.parentNode) toast.remove(); }, ttl || 6000);
  }

  // ── SignalR connection ────────────────────────────────────────────────────
  let connection = null;

  function connect() {
    if (typeof signalR === 'undefined') {
      console.warn('[WizNotifications] SignalR library not loaded — real-time notifications disabled.');
      return;
    }

    const token = localStorage.getItem('wiz_token');
    if (!token) {
      // No token yet — retry once after 5 seconds (in case login happens after page load)
      setTimeout(connect, 5000);
      return;
    }

    connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/ui', { accessTokenFactory: () => localStorage.getItem('wiz_token') || '' })
      .withAutomaticReconnect([1000, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connection.on('notification', function (evt) {
      handleEvent(evt);
    });

    connection.onreconnected(() => {
      console.info('[WizNotifications] Reconnected to notification hub.');
    });

    connection.start().catch(err => {
      console.warn('[WizNotifications] Hub connection failed:', err.message);
    });
  }

  function handleEvent(evt) {
    const ts = evt.timestampUtc
      ? new Date(evt.timestampUtc).toLocaleTimeString()
      : '';

    switch (evt.event) {
      case 'job-completed':
        showToast(
          'Job completed',
          `${evt.message || 'Sage operation finished.'} ${ts}`,
          'success', null, 5000);
        break;

      case 'job-failed':
        showToast(
          'Job failed',
          `${evt.message || 'Check audit log for details.'} ${ts}`,
          'error', '/audit', 10000);
        break;

      case 'approval-required':
        showToast(
          'Approval required',
          evt.message || 'A new proposal is waiting for your approval.',
          'warning', '/act', 12000);
        break;

      case 'alert-threshold-hit':
        showToast(
          'Alert',
          evt.message || 'An Insight threshold was triggered.',
          'warning', null, 8000);
        break;

      default:
        showToast('Notification', evt.message || evt.event, 'info');
    }
  }

  // ── Boot ──────────────────────────────────────────────────────────────────
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', connect);
  } else {
    connect();
  }

  // Expose for manual triggering / testing from console
  window.WizNotifications = { connect, showToast, getConnection: () => connection };
})();
