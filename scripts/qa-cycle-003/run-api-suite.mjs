#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { runDuplicateTests } from './duplicate-tests.mjs';
import { runRaceTests } from './race-tests.mjs';
import { runSessionTests } from './session-tests.mjs';
import { baseUrl, webUrl } from './api-client.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const resultsDir = path.join(root, 'docs', 'QA', 'results');

const duplicatePrevention = await runDuplicateTests();
const raceCondition = await runRaceTests();
const sessionRecovery = await runSessionTests();

fs.mkdirSync(resultsDir, { recursive: true });
const outPath = path.join(resultsDir, 'qa-cycle-003.json');
const payload = {
  cycle: '003',
  ranAt: new Date().toISOString(),
  baseUrl: webUrl,
  apiUrl: baseUrl,
  instructions: 'DOCS/WizAccounts-Enterprise-QA-Enforcement-Instructions.md',
  duplicatePrevention,
  raceCondition,
  sessionRecovery,
  multiTab: [],
  longDuration: [],
  frontendSync: [],
  uxFindings: [],
  criticalIssues: [],
  recommendedFixes: [],
  evidence: [],
};

fs.writeFileSync(outPath, JSON.stringify(payload, null, 2), 'utf8');

const failed =
  duplicatePrevention.filter((r) => r.status === 'FAIL').length +
  raceCondition.filter((r) => r.status === 'FAIL').length +
  sessionRecovery.filter((r) => r.status === 'FAIL').length;

console.log(
  `\nAPI suite: DP=${duplicatePrevention.length} RC=${raceCondition.length} SR=${sessionRecovery.length} fails=${failed}`,
);
process.exit(failed > 0 ? 1 : 0);
