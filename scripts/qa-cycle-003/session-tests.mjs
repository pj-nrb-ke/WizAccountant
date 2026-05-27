import { api, login, getSites, pickSite, uid, baseUrl } from './api-client.mjs';

export async function runSessionTests() {
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
      console.log(`OK [SR] ${name}`);
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      rows.push({
        id,
        test: name,
        status: 'FAIL',
        notes: msg,
        assertions: 'failed',
        durationMs: Date.now() - t0,
        evidence: `API ${baseUrl}`,
        reproduction: name,
      });
      console.log(`FAIL [SR] ${name} — ${msg}`);
    }
  }

  await run('SR-001', 'SR-01 bad password login → 401', async () => {
    const r = await api('POST', '/api/auth/login', {
      body: { email: 'admin@pilot.local', password: 'wrong' },
    });
    if (r.status !== 401) throw new Error(`got ${r.status}`);
    return '401';
  });

  await run('SR-002', 'SR-02 unknown user → 401', async () => {
    const r = await api('POST', '/api/auth/login', {
      body: { email: 'nobody@pilot.local', password: 'pilot' },
    });
    if (r.status !== 401) throw new Error(`got ${r.status}`);
    return '401';
  });

  await run('SR-003', 'SR-03 valid preparer login → token+role', async () => {
    const j = await login('preparer@pilot.local');
    if (!j.token || j.role !== 'Preparer') throw new Error(`role=${j.role}`);
    return `role=${j.role}`;
  });

  await run('SR-004', 'SR-04 valid approver login → token+role', async () => {
    const j = await login('approver@pilot.local');
    if (!j.token || j.role !== 'Approver') throw new Error(`role=${j.role}`);
    return `role=${j.role}`;
  });

  await run('SR-005', 'SR-05 tampered bearer on sites → still 200 (stub auth)', async () => {
    const r = await api('GET', '/api/sites', { token: 'invalid-token-xyz' });
    if (r.status !== 200) throw new Error(`got ${r.status}`);
    return 'FINDING: stub does not validate JWT on /api/sites';
  });

  for (let i = 6; i <= 20; i++) {
    const id = `SR-${String(i).padStart(3, '0')}`;
    await run(id, `SR-${String(i).padStart(2, '0')} re-login + API cycle`, async () => {
      const user = i % 3 === 0 ? 'admin@pilot.local' : i % 3 === 1 ? 'preparer@pilot.local' : 'approver@pilot.local';
      const a = await login(user);
      const sites = await api('GET', '/api/sites', { token: a.token });
      if (sites.status !== 200) throw new Error(`sites ${sites.status}`);
      const b = await login(user);
      if (!b.token) throw new Error('re-login failed');
      if (siteId && i % 2 === 0) {
        const chat = await api('POST', '/api/insight/chat', {
          body: { siteId, message: `sr-${i}-${uid()}` },
          timeoutMs: 45_000,
        });
        if (chat.status >= 500) throw new Error(`chat ${chat.status}`);
      }
      return `user=${user} token ok`;
    });
  }

  return rows;
}
