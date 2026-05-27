#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const pwPath = path.join(root, 'qa', 'e2e-report', 'results.json');
const outPath = path.join(root, 'docs', 'QA', 'results', 'qa-cycle-003.json');

const PW_MAP = {
  'Multi-Tab Tests': 'multiTab',
  'Long-Duration Stability': 'longDuration',
  'Frontend Sync Tests': 'frontendSync',
};

function collectPlaywrightBuckets(suites) {
  const buckets = { multiTab: [], longDuration: [], frontendSync: [] };
  function walk(list) {
    for (const suite of list ?? []) {
      const key = PW_MAP[suite.title];
      if (key) {
        for (const spec of suite.specs ?? []) {
          for (const t of spec.tests ?? []) {
            const st = t.status ?? t.results?.[0]?.status;
            const ok = st === 'expected' || st === 'passed';
            const err = t.results?.[0]?.error?.message ?? '';
            const attachments = t.results?.[0]?.attachments ?? [];
            buckets[key].push({
              test: spec.title,
              status: st === 'skipped' ? 'SKIP' : ok ? 'PASS' : 'FAIL',
              durationMs: t.results?.[0]?.duration ?? 0,
              notes: err.split('\n')[0].slice(0, 400),
              assertions: ok ? 'UI state verified' : 'failed',
              evidence: !ok
                ? 'qa/e2e-report/test-results/ trace+screenshot+video'
                : '',
              reproduction: !ok ? `${suite.title} › ${spec.title}` : '',
            });
          }
        }
      }
      walk(suite.suites);
    }
  }
  walk(suites);
  return buckets;
}

function addIds(rows, prefix) {
  return rows.map((r, i) => ({
    id: `${prefix}-${String(i + 1).padStart(3, '0')}`,
    ...r,
  }));
}

const existing = fs.existsSync(outPath)
  ? JSON.parse(fs.readFileSync(outPath, 'utf8'))
  : { duplicatePrevention: [], raceCondition: [], sessionRecovery: [] };

const pw = fs.existsSync(pwPath) ? JSON.parse(fs.readFileSync(pwPath, 'utf8')) : { suites: [] };
const buckets = collectPlaywrightBuckets(pw.suites);
const prefixMap = { multiTab: 'MT', longDuration: 'LD', frontendSync: 'FS' };
for (const k of Object.keys(buckets)) {
  buckets[k] = addIds(buckets[k], prefixMap[k]);
}

existing.multiTab = buckets.multiTab;
existing.longDuration = buckets.longDuration;
existing.frontendSync = buckets.frontendSync;
existing.ranAt = new Date().toISOString();

const all = [
  ...existing.duplicatePrevention,
  ...existing.raceCondition,
  ...existing.sessionRecovery,
  ...existing.multiTab,
  ...existing.longDuration,
  ...existing.frontendSync,
];

const passed = all.filter((r) => r.status === 'PASS').length;
const failed = all.filter((r) => r.status === 'FAIL').length;
const blocked = all.filter((r) => r.status === 'BLOCKED').length;

existing.summary = {
  overall: failed === 0 && passed > 0 ? (blocked > 0 ? 'PASS_WITH_BLOCKED' : 'PASS') : 'FAIL',
  total: all.length,
  passed,
  failed,
  blocked,
  skipped: all.filter((r) => r.status === 'SKIP').length,
  duplicateCount: existing.duplicatePrevention.length,
  raceCount: existing.raceCondition.length,
  sessionCount: existing.sessionRecovery.length,
  multiTabCount: existing.multiTab.length,
  longDurationCount: existing.longDuration.length,
  frontendSyncCount: existing.frontendSync.length,
  totalNavActions: existing.longDuration.length * 50,
};

existing.criticalIssues = all
  .filter((r) => r.status === 'FAIL')
  .map((r, i) => ({
    id: `CI-${String(i + 1).padStart(3, '0')}`,
    severity: r.id?.startsWith('DP') ? 'Critical' : 'High',
    test: r.test,
    notes: r.notes,
    evidence: r.evidence || 'see qa-cycle-003.json',
  }));

existing.recommendedFixes = existing.criticalIssues.map((c, i) => ({
  id: `RF-${String(i + 1).padStart(3, '0')}`,
  priority: c.severity,
  action: `Fix: ${c.test}`,
  notes: c.notes,
}));

existing.uxFindings = [
  {
    id: 'UX-001',
    severity: 'High',
    finding: 'P1-22 auth stub: API does not validate Bearer token on most routes (SR-005 documents).',
  },
  {
    id: 'UX-002',
    severity: 'Info',
    finding: `Cycle 003: ${existing.summary.totalNavActions} tab navigations in long-duration suite.`,
  },
  ...existing.criticalIssues.slice(0, 5).map((c, i) => ({
    id: `UX-${String(i + 10).padStart(3, '0')}`,
    severity: 'Medium',
    finding: c.test,
  })),
];

existing.evidence = [
  { artifact: 'Cycle JSON', path: 'docs/QA/results/qa-cycle-003.json' },
  { artifact: 'Playwright JSON', path: 'qa/e2e-report/results.json' },
  { artifact: 'Playwright HTML', path: 'qa/e2e-report/html/index.html' },
  { artifact: 'Traces', path: 'qa/e2e-report/test-results/' },
  { artifact: 'Excel', path: 'QA-Test-003.xlsx' },
];

fs.writeFileSync(outPath, JSON.stringify(existing, null, 2), 'utf8');
console.log(
  `Merged: ${passed}/${all.length} passed, ${failed} failed, ${blocked} blocked`,
);
process.exit(failed > 0 ? 1 : 0);
