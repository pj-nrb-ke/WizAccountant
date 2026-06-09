/**
 * WizAccountant — Insight Chat Load Test Baseline (B5-E)
 * Tool: k6 (https://k6.io)
 *
 * Target:   50 concurrent virtual users, 2-minute soak
 * SLO:      p(95) < 2 000 ms   (Insight chat round-trip)
 *
 * Prerequisites:
 *   1. Install k6: https://k6.io/docs/get-started/installation/
 *   2. Set env vars:
 *        WIZ_BASE_URL   (default http://localhost:5000)
 *        WIZ_JWT        (Bearer token for a Reader/Preparer user)
 *        WIZ_SITE_ID    (UUID of a connected site)
 *
 * Run:
 *   k6 run tests/load/insight-chat-baseline.js
 *
 * Output:
 *   k6 will print a summary table. Pass/fail is set via thresholds below.
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Counter } from 'k6/metrics';

// ── Custom metrics ──────────────────────────────────────────────────────────
const chatDuration    = new Trend('insight_chat_duration', true);
const chatErrors      = new Counter('insight_chat_errors');
const toolsReqDuration = new Trend('insight_tools_duration', true);

// ── Options ─────────────────────────────────────────────────────────────────
export const options = {
  scenarios: {
    insight_chat_ramp: {
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        { duration: '30s', target: 10  },   // ramp up
        { duration: '60s', target: 50  },   // soak at 50 VUs
        { duration: '30s', target: 0   },   // ramp down
      ],
    },
  },
  thresholds: {
    // SLO: p95 of chat round-trip < 2s
    'insight_chat_duration': ['p(95)<2000'],
    // SLO: tools list p95 < 500ms
    'insight_tools_duration': ['p(95)<500'],
    // SLO: error rate < 1%
    'insight_chat_errors': ['count<5'],
    // k6 built-in HTTP error rate
    'http_req_failed': ['rate<0.01'],
  },
};

// ── Config ───────────────────────────────────────────────────────────────────
const BASE = __ENV.WIZ_BASE_URL || 'http://localhost:5000';
const JWT  = __ENV.WIZ_JWT      || '';
const SITE_ID = __ENV.WIZ_SITE_ID || '00000000-0000-0000-0000-000000000001';

const HEADERS = {
  'Content-Type': 'application/json',
  ...(JWT ? { Authorization: `Bearer ${JWT}` } : {}),
};

// Representative Insight questions cycling across workloads
const QUESTIONS = [
  'Show me top 10 customers by outstanding balance',
  'What are my overdue invoices from the last 30 days?',
  'Summarise cash on hand vs 30-day payables',
  'List inventory items below reorder level',
  'Show supplier invoices pending approval',
  'What is my gross profit margin this month?',
  'List all sales orders created this week',
  'Show GL transactions for account 1000 this month',
  'Which customers have exceeded their credit limit?',
  'Give me a VAT summary for the current period',
];

// ── Default function (VU loop) ───────────────────────────────────────────────
export default function () {
  // 1. Health probe (warm-up / smoke check)
  const health = http.get(`${BASE}/health`, { headers: HEADERS });
  check(health, { 'health 200': r => r.status === 200 });

  // 2. Tools list (metadata endpoint — lightweight)
  const toolsStart = Date.now();
  const tools = http.get(`${BASE}/api/v1/insight/tools`, { headers: HEADERS });
  toolsReqDuration.add(Date.now() - toolsStart);
  check(tools, { 'tools 200': r => r.status === 200 });

  // 3. Insight chat ask — the primary SLO test
  const question = QUESTIONS[Math.floor(Math.random() * QUESTIONS.length)];
  const chatPayload = JSON.stringify({
    siteId: SITE_ID,
    query: question,
    conversationId: `load-${__VU}-${__ITER}`,
  });

  const chatStart = Date.now();
  const chat = http.post(
    `${BASE}/api/v1/insight/ask`,
    chatPayload,
    { headers: HEADERS, timeout: '15s' }
  );
  const chatMs = Date.now() - chatStart;
  chatDuration.add(chatMs);

  const chatOk = check(chat, {
    'chat 2xx': r => r.status >= 200 && r.status < 300,
    'chat has response': r => r.body && r.body.length > 0,
  });
  if (!chatOk) chatErrors.add(1);

  // Think time — realistic user pause between questions
  sleep(Math.random() * 2 + 1);  // 1–3 seconds
}

// ── Summary ──────────────────────────────────────────────────────────────────
export function handleSummary(data) {
  const p95 = data.metrics.insight_chat_duration
    ? data.metrics.insight_chat_duration.values['p(95)']
    : 'n/a';
  const sloPass = typeof p95 === 'number' && p95 < 2000;

  return {
    stdout: `
=== WizAccountant Insight Chat Load Test ===
  p95 chat duration : ${typeof p95 === 'number' ? p95.toFixed(0) : p95} ms
  SLO target        : < 2 000 ms
  SLO pass          : ${sloPass ? '✅ PASS' : '❌ FAIL'}

Full results saved to tests/load/results/insight-chat-baseline.json
`,
    'tests/load/results/insight-chat-baseline.json': JSON.stringify(data, null, 2),
  };
}
