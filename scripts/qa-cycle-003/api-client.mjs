export const baseUrl = (process.env.QA_API_URL ?? 'https://app.ascendbooks.biz').replace(/\/$/, '');
export const webUrl = (process.env.QA_BASE_URL ?? baseUrl).replace(/\/$/, '');
const password = process.env.QA_PASSWORD ?? 'pilot';
const tenantId = 'pilot-tenant';

export const PREPARER_ID = '33333333-3333-3333-3333-333333333333';
export const APPROVER_ID = '22222222-2222-2222-2222-222222222222';
export const ADMIN_ID = '11111111-1111-1111-1111-111111111111';

export async function api(method, pathSuffix, { token, body, headers, timeoutMs = 30_000 } = {}) {
  const ctrl = new AbortController();
  const t = setTimeout(() => ctrl.abort(), timeoutMs);
  try {
    const h = { Accept: 'application/json', 'X-Tenant-Id': tenantId, ...headers };
    if (token) h.Authorization = `Bearer ${token}`;
    if (body !== undefined) h['Content-Type'] = 'application/json';
    const res = await fetch(`${baseUrl}${pathSuffix}`, {
      method,
      headers: h,
      body: body !== undefined ? JSON.stringify(body) : undefined,
      signal: ctrl.signal,
    });
    const text = await res.text();
    let json = null;
    try {
      json = text ? JSON.parse(text) : null;
    } catch {
      json = { raw: text };
    }
    return { status: res.status, json, text };
  } finally {
    clearTimeout(t);
  }
}

export async function login(email) {
  const r = await api('POST', '/api/auth/login', {
    body: { email, password },
    timeoutMs: 15_000,
  });
  if (r.status !== 200 || !r.json?.token) throw new Error(`login ${email}: HTTP ${r.status}`);
  return r.json;
}

export async function getSites() {
  const r = await api('GET', '/api/sites');
  if (r.status !== 200) throw new Error(`sites HTTP ${r.status}`);
  return r.json ?? [];
}

export function pickSite(sites) {
  const online = sites.filter((s) => s.isOnline);
  return (online[0] ?? sites[0])?.siteId ?? null;
}

export function uid() {
  return `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
}
