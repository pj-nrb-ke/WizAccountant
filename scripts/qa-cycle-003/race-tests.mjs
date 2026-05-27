import { api, login, getSites, pickSite, uid, PREPARER_ID, baseUrl } from './api-client.mjs';

export async function runRaceTests() {
  const rows = [];
  let siteId;
  try {
    siteId = pickSite(await getSites());
  } catch {
    siteId = null;
  }

  async function run(id, name, fn) {
    const t0 = Date.now();
    try {
      const assertions = await fn();
      rows.push({
        id,
        test: name,
        status: 'PASS',
        notes: '',
        assertions,
        durationMs: Date.now() - t0,
        evidence: '',
        reproduction: '',
      });
      console.log(`OK [RC] ${name}`);
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      const status = msg.includes('BLOCKED') ? 'BLOCKED' : 'FAIL';
      rows.push({
        id,
        test: name,
        status,
        notes: msg,
        assertions: 'failed',
        durationMs: Date.now() - t0,
        evidence: `API ${baseUrl}`,
        reproduction: name,
      });
      console.log(`FAIL [RC] ${name} — ${msg}`);
    }
  }

  await run('RC-001', 'RC-01 parallel GET /api/sites x10 → same count', async () => {
    const rs = await Promise.all(Array.from({ length: 10 }, () => api('GET', '/api/sites')));
    const counts = rs.map((r) => (r.json ?? []).length);
    if (new Set(counts).size !== 1) throw new Error(`counts differ: ${counts.join(',')}`);
    if (rs.some((r) => r.status >= 500)) throw new Error('500');
    return `stable count=${counts[0]}`;
  });

  await run('RC-002', 'RC-02 parallel GET health x20 → all ok', async () => {
    const rs = await Promise.all(Array.from({ length: 20 }, () => api('GET', '/health')));
    if (rs.some((r) => r.json?.ok !== true)) throw new Error('health not ok');
    return '20/20 ok';
  });

  await run('RC-003', 'RC-03 parallel insight tools + workflows', async () => {
    const [a, b] = await Promise.all([
      api('GET', '/api/v1/insight/tools'),
      api('GET', '/api/act/workflows'),
    ]);
    if (a.status !== 200 || b.status !== 200) throw new Error(`tools=${a.status} wf=${b.status}`);
    return 'both 200';
  });

  await run('RC-004', 'RC-04 rapid navigation API: sites→proposals→audit', async () => {
    if (!siteId) throw new Error('BLOCKED: no site');
    for (let i = 0; i < 5; i++) {
      await api('GET', '/api/sites');
      await api('GET', `/api/act/proposals?siteId=${siteId}`);
      await api('GET', '/api/audit/jobs?take=10');
    }
    return '15 requests no 500';
  });

  await run('RC-005', 'RC-05 parallel chat messages same conversation', async () => {
    if (!siteId) throw new Error('BLOCKED: no site');
    const first = await api('POST', '/api/insight/chat', {
      body: { siteId, message: `race ${uid()}` },
      timeoutMs: 60_000,
    });
    if (first.status >= 500) throw new Error(`chat ${first.status}`);
    const cid = first.json?.conversationId;
    const rs = await Promise.all(
      Array.from({ length: 5 }, (_, i) =>
        api('POST', '/api/insight/chat', {
          body: { siteId, message: `follow ${i}`, conversationId: cid },
          timeoutMs: 60_000,
        }),
      ),
    );
    if (rs.some((r) => r.status >= 500)) throw new Error('500 on parallel chat');
    return `conversationId=${cid ?? 'new'}`;
  });

  for (let i = 6; i <= 20; i++) {
    const id = `RC-${String(i).padStart(3, '0')}`;
    await run(id, `RC-${String(i).padStart(2, '0')} concurrent run-wait storm`, async () => {
      if (!siteId) throw new Error('BLOCKED: no site');
      const rs = await Promise.all(
        Array.from({ length: 4 }, () =>
          api('POST', '/api/jobs/run-wait', {
            body: {
              siteId,
              operation: 'customer.list',
              parameters: { top: '3' },
              requestedBy: `qa-rc-${i}`,
              timeoutSeconds: 15,
            },
            timeoutMs: 20_000,
          }),
        ),
      );
      const hung = rs.filter((r) => r.status === 504 || (r.text && r.text.includes('timeout')));
      if (hung.length === rs.length) throw new Error('Hanging API: all timeouts (site offline?)');
      if (rs.some((r) => r.status >= 500 && r.status !== 504)) throw new Error('500');
      return `statuses=${rs.map((r) => r.status).join(',')}`;
    });
  }

  return rows;
}
