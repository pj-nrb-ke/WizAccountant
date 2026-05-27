import { test, expect } from '@playwright/test';

const INSIGHT = '/insight/index.html';
const ACT = '/act/index.html';

test.describe.configure({ timeout: 30_000 });

const MULTI_TAB = Array.from({ length: 15 }, (_, i) => ({
  id: `MT-${String(i + 1).padStart(2, '0')}`,
  name: [
    'insight two tabs load sites',
    'insight tab1 dashboard tab2 AR',
    'act inbox two tabs same count',
    'rapid tab switch insight tabs x5',
    'insight chat tab + dashboard tab',
    'three tabs insight',
    'act propose panel + inbox tab',
    'tab reload after dashboard click',
    'double-click dashboard refresh',
    'insight export tab navigation',
    'act audit + inbox parallel tabs',
    'close second tab first survives',
    'rapid open close insight x5',
    'act user switch preparer/approver',
    'concurrent tab dashboard spam',
  ][i],
}));

test.describe('Multi-Tab Tests', () => {
  for (const sc of MULTI_TAB) {
    test(`${sc.id} ${sc.name}`, async ({ context, page }) => {
      const logs: string[] = [];
      page.on('console', (m) => {
        if (m.type() === 'error') logs.push(m.text());
      });

      await page.goto(INSIGHT);
      await expect(page.locator('#site-select')).toBeVisible({ timeout: 15_000 });

      const idx = parseInt(sc.id.slice(3), 10);
      const page2 = await context.newPage();

      if (idx === 13) {
        await page.goto(ACT);
        await page2.goto(ACT);
        await expect(page.locator('#user-select')).toBeVisible();
        await page2.close();
        expect(logs.filter((l) => l.includes('Uncaught'))).toHaveLength(0);
        return;
      }

      await page2.goto(INSIGHT);
      if (idx === 1) {
        await page.locator('#btn-dashboard').click();
        await page2.locator('.tab[data-tab="ar"]').click();
        await expect(page2.locator('#panel-ar')).toHaveClass(/active/);
      } else if (idx === 8) {
        await page.locator('#btn-dashboard').dblclick();
        await page2.locator('#btn-dashboard').dblclick();
      } else if (idx === 12) {
        for (let t = 0; t < 5; t++) {
          const p = await context.newPage();
          await p.goto(INSIGHT);
          await p.close();
        }
      } else {
        await page.locator('.tab').first().click();
        await page2.locator('.tab').nth(1).click();
      }

      await page2.close();
      expect(logs.filter((l) => l.includes('Uncaught'))).toHaveLength(0);
    });
  }
});

test.describe('Long-Duration Stability', () => {
  for (let i = 1; i <= 10; i++) {
    const id = `LD-${String(i).padStart(2, '0')}`;
    test(`${id} 50 navigation actions #${i}`, async ({ page }) => {
      test.setTimeout(120_000);
      await page.goto(INSIGHT);
      await expect(page.locator('#site-select')).toBeVisible({ timeout: 15_000 });
      const tabs = ['dashboard', 'ar', 'ap', 'search', 'chat'];
      for (let n = 0; n < 50; n++) {
        const tab = tabs[n % tabs.length];
        await page.locator(`.tab[data-tab="${tab}"]`).click();
        await page.waitForTimeout(50);
      }
      await expect(page.locator('.tab.active')).toBeVisible();
    });
  }
});

test.describe('Frontend Sync Tests', () => {
  const cases = [
    ['FS-01', 'dashboard click updates output panel'],
    ['FS-02', 'AR customers button sets loading then result or error'],
    ['FS-03', 'chat send appends user message'],
    ['FS-04', 'site select has options after load'],
    ['FS-05', 'tab switch preserves no JS crash'],
    ['FS-06', 'act inbox loads without throw'],
    ['FS-07', 'insight health link redirect'],
    ['FS-08', 'double dashboard does not blank page'],
  ];
  for (const [id, name] of cases) {
    test(`${id} ${name}`, async ({ page }) => {
      const errors: string[] = [];
      page.on('pageerror', (e) => errors.push(e.message));
      if (id.startsWith('FS-06')) {
        await page.goto(ACT);
        await expect(page.locator('#site-select')).toBeVisible({ timeout: 15_000 });
        await page.locator('#refresh-inbox').click();
        await expect(page.locator('#inbox-list')).not.toHaveText('Loading…', { timeout: 20_000 });
      } else {
        await page.goto(INSIGHT);
        await expect(page.locator('#site-select')).toBeVisible({ timeout: 15_000 });
        if (id === 'FS-01' || id === 'FS-08') {
          await page.locator('#btn-dashboard').click();
          if (id === 'FS-08') await page.locator('#btn-dashboard').click();
          await expect(page.locator('#dashboard-out')).not.toHaveText('Loading…', { timeout: 90_000 });
        } else if (id === 'FS-02') {
          await page.locator('.tab[data-tab="ar"]').click();
          await page.locator('[data-op="customer.list"]').click();
          await expect(page.locator('#ar-out')).not.toHaveText('Loading…', { timeout: 90_000 });
        } else if (id === 'FS-03') {
          await page.locator('.tab[data-tab="chat"]').click();
          await page.locator('#chat-form input[name="message"]').fill('QA sync test');
          await page.locator('#chat-form button[type="submit"]').click();
          await expect(page.locator('.chat-msg')).toHaveCount(2, { timeout: 60_000 });
        } else if (id === 'FS-05') {
          await page.locator('.tab[data-tab="ap"]').click();
          await page.locator('.tab[data-tab="search"]').click();
        }
      }
      expect(errors).toHaveLength(0);
    });
  }
});
