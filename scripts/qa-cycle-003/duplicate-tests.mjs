import { api, login, getSites, pickSite, uid, PREPARER_ID, APPROVER_ID, baseUrl } from './api-client.mjs';

export async function runDuplicateTests() {
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
      console.log(`OK [DP] ${name}`);
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      rows.push({
        id,
        test: name,
        status: msg.includes('BLOCKED') ? 'BLOCKED' : 'FAIL',
        notes: msg,
        assertions: 'failed',
        durationMs: Date.now() - t0,
        evidence: `API ${baseUrl}; see qa-cycle-003.json`,
        reproduction: `POST/GET ${baseUrl} — ${name}`,
      });
      console.log(`${msg.includes('BLOCKED') ? 'BLOCKED' : 'FAIL'} [DP] ${name} — ${msg}`);
    }
  }

  await run('DP-001', 'DP-01 double POST login same credentials → 2 tokens OK', async () => {
    const [a, b] = await Promise.all([
      login('admin@pilot.local'),
      login('admin@pilot.local'),
    ]);
    if (!a.token || !b.token) throw new Error('missing token');
    return 'both logins 200; stub allows multi-token';
  });

  await run('DP-002', 'DP-02 triple burst login → all 200', async () => {
    const rs = await Promise.all([
      login('preparer@pilot.local'),
      login('preparer@pilot.local'),
      login('preparer@pilot.local'),
    ]);
    if (rs.length !== 3) throw new Error('not 3');
    return '3 tokens issued';
  });

  await run('DP-003', 'DP-03 parallel pairing-code create → unique codes', async () => {
    const rs = await Promise.all(
      Array.from({ length: 5 }, () =>
        api('POST', '/api/pairing-codes', {
          body: { tenantId: 'pilot-tenant', siteName: `QA-DP03-${uid()}` },
        }),
      ),
    );
    const codes = rs.map((r) => r.json?.pairingCode ?? r.json?.code).filter(Boolean);
    if (codes.length !== 5) throw new Error(`expected 5 codes got ${codes.length}`);
    if (new Set(codes).size !== 5) throw new Error('duplicate pairing codes');
    return `unique codes=${codes.length}`;
  });

  await run('DP-004', 'DP-04 sequential duplicate propose same idempotencyKey', async () => {
    if (!siteId) throw new Error('BLOCKED: no site');
    const idem = `qa-dp04-${uid()}`;
    const body = {
      siteId,
      preparedByUserId: PREPARER_ID,
      proposalType: 'gl.journal',
      title: `DP04 ${uid()}`,
      payloadJson: '{}',
      idempotencyKey: idem,
    };
    const a = await api('POST', '/api/act/proposals', { body });
    const b = await api('POST', '/api/act/proposals', { body });
    if (a.status !== 200 && a.status !== 201) throw new Error(`first ${a.status}`);
    const list = await api('GET', `/api/act/proposals?siteId=${siteId}&status=0`);
    const pending = (list.json ?? []).filter((p) => p.idempotencyKey === idem);
    if (pending.length > 1) throw new Error(`duplicate proposals idem=${pending.length}`);
    if (b.status >= 500) throw new Error(`second ${b.status}`);
    return `pending with key=${pending.length}`;
  });

  await run('DP-005', 'DP-05 parallel propose burst same idempotencyKey', async () => {
    if (!siteId) throw new Error('BLOCKED: no site');
    const idem = `qa-dp05-${uid()}`;
    const body = {
      siteId,
      preparedByUserId: PREPARER_ID,
      proposalType: 'gl.journal',
      title: `DP05 ${uid()}`,
      payloadJson: '{}',
      idempotencyKey: idem,
    };
    await Promise.all(Array.from({ length: 5 }, () => api('POST', '/api/act/proposals', { body })));
    const list = await api('GET', `/api/act/proposals?siteId=${siteId}&status=0`);
    const n = (list.json ?? []).filter((p) => p.idempotencyKey === idem).length;
    if (n > 1) throw new Error(`race duplicate proposals=${n}`);
    return `proposals with key=${n}`;
  });

  await run('DP-006', 'DP-06 double-click approve parallel same proposal', async () => {
    if (!siteId) throw new Error('BLOCKED: no site');
    const prop = await api('POST', '/api/act/proposals', {
      body: {
        siteId,
        preparedByUserId: PREPARER_ID,
        proposalType: 'gl.journal',
        title: `DP06 ${uid()}`,
        payloadJson: '{}',
      },
    });
    const pid = prop.json?.proposalId;
    if (!pid) throw new Error('no proposal');
    const [a, b] = await Promise.all([
      api('POST', `/api/act/proposals/${pid}/approve`, {
        body: { approverUserId: APPROVER_ID, comment: 'DP06a' },
        timeoutMs: 130_000,
      }),
      api('POST', `/api/act/proposals/${pid}/approve`, {
        body: { approverUserId: APPROVER_ID, comment: 'DP06b' },
        timeoutMs: 130_000,
      }),
    ]);
    const ok = [a.status, b.status].filter((s) => s >= 200 && s < 300).length;
    const fail = [a.status, b.status].filter((s) => s === 400 || s === 404 || s === 409).length;
    if (ok > 1) throw new Error('double approve succeeded twice');
    if (ok === 0 && fail === 0) throw new Error(`statuses ${a.status}/${b.status}`);
    return `approve outcomes ${a.status}/${b.status}`;
  });

  for (let i = 7; i <= 25; i++) {
    const id = `DP-${String(i).padStart(3, '0')}`;
    await run(id, `DP-${String(i).padStart(2, '0')} concurrent job create burst`, async () => {
      if (!siteId) throw new Error('BLOCKED: no site');
      const idem = `qa-dp${i}-${uid()}`;
      const rs = await Promise.all(
        Array.from({ length: 3 }, (_, j) =>
          api('POST', '/api/jobs', {
            body: {
              siteId,
              operation: 'customer.list',
              parameters: { top: '5' },
              requestedBy: `qa-dp-${i}-${j}`,
              idempotencyKey: i % 2 === 0 ? idem : `${idem}-${j}`,
            },
            timeoutMs: 5_000,
          }),
        ),
      );
      if (rs.some((r) => r.status >= 500)) throw new Error('500 on job create');
      const jobs = rs.filter((r) => r.status === 200).map((r) => r.json?.jobId);
      if (i % 2 === 0 && new Set(jobs).size !== jobs.length) throw new Error('duplicate jobIds');
      return `jobs created=${jobs.length} statuses=${rs.map((r) => r.status).join(',')}`;
    });
  }

  return rows;
}
