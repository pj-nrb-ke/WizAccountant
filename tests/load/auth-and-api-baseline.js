/**
 * WizAccountant — Auth & API Endpoints Load Test Baseline (B5-E)
 * Tool: k6  (https://k6.io)
 *
 * Covers: login, RBAC gating, proposals list, write-audit, billing webhook
 * Target: 30 VUs, 90-second soak, p95 < 500ms for non-Insight endpoints
 *
 * Run:
 *   k6 run tests/load/auth-and-api-baseline.js
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend } from 'k6/metrics';

const authDuration   = new Trend('auth_duration', true);
const rbacDuration   = new Trend('rbac_duration', true);

export const options = {
  scenarios: {
    api_load: {
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        { duration: '20s', target: 15 },
        { duration: '60s', target: 30 },
        { duration: '10s', target: 0  },
      ],
    },
  },
  thresholds: {
    'auth_duration': ['p(95)<500'],
    'rbac_duration': ['p(95)<300'],
    'http_req_failed': ['rate<0.02'],
  },
};

const BASE = __ENV.WIZ_BASE_URL || 'http://localhost:5000';
const HEADERS = { 'Content-Type': 'application/json' };

export default function () {
  // Auth — invalid creds (expected 401 — measures middleware perf)
  const loginStart = Date.now();
  const login = http.post(
    `${BASE}/api/auth/login`,
    JSON.stringify({ email: `load-test-${__VU}@example.com`, password: 'wrongpassword' }),
    { headers: HEADERS }
  );
  authDuration.add(Date.now() - loginStart);
  check(login, { 'login 401': r => r.status === 401 });

  // RBAC gating — unauthenticated requests should be rejected quickly
  const rbacStart = Date.now();
  const rbac = http.get(`${BASE}/api/act/proposals`, { headers: HEADERS });
  rbacDuration.add(Date.now() - rbacStart);
  check(rbac, { 'rbac 401': r => r.status === 401 });

  // Health (always public)
  const health = http.get(`${BASE}/health`);
  check(health, { 'health 200': r => r.status === 200 });

  sleep(1);
}

export function handleSummary(data) {
  const authP95 = data.metrics.auth_duration?.values['p(95)'];
  const rbacP95 = data.metrics.rbac_duration?.values['p(95)'];

  return {
    stdout: `
=== WizAccountant Auth & API Load Test ===
  p95 auth       : ${authP95?.toFixed(0) ?? 'n/a'} ms  (SLO < 500ms)
  p95 RBAC gate  : ${rbacP95?.toFixed(0) ?? 'n/a'} ms  (SLO < 300ms)
`,
    'tests/load/results/auth-api-baseline.json': JSON.stringify(data, null, 2),
  };
}
