import { defineConfig } from '@playwright/test';

const baseURL = process.env.QA_BASE_URL ?? 'https://app.ascendbooks.biz';

export default defineConfig({
  testDir: './e2e',
  timeout: 30_000,
  expect: { timeout: 15_000 },
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [
    ['list'],
    ['json', { outputFile: 'e2e-report/results.json' }],
    ['html', { outputFolder: 'e2e-report/html', open: 'never' }],
  ],
  use: {
    baseURL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
});
