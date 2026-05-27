import type {
  ApprovalProposal,
  ChatResponse,
  JobDto,
  LoginResponse,
  Session,
  Site,
} from "../types";

export class ApiError extends Error {
  constructor(message: string) {
    super(message);
  }
}

async function request<T>(
  session: Session,
  path: string,
  options: RequestInit = {}
): Promise<T> {
  const base = session.apiBaseUrl.replace(/\/$/, "");
  const res = await fetch(`${base}${path}`, {
    ...options,
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
      Authorization: `Bearer ${session.token}`,
      "X-Tenant-Id": session.tenantId,
      ...(options.headers as Record<string, string>),
    },
  });
  const text = await res.text();
  let body: unknown = text;
  try {
    body = text ? JSON.parse(text) : null;
  } catch {
    /* plain text */
  }
  if (!res.ok) {
    const err =
      typeof body === "object" && body !== null && "error" in body
        ? String((body as { error: string }).error)
        : typeof body === "object" && body !== null && "message" in body
          ? String((body as { message: string }).message)
          : text || res.statusText;
    throw new ApiError(err);
  }
  return body as T;
}

export async function login(
  apiBaseUrl: string,
  email: string,
  password: string
): Promise<LoginResponse> {
  const base = apiBaseUrl.replace(/\/$/, "");
  const res = await fetch(`${base}/api/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json", Accept: "application/json" },
    body: JSON.stringify({ email, password }),
  });
  const text = await res.text();
  const body = text ? JSON.parse(text) : null;
  if (!res.ok) throw new ApiError("Login failed");
  return body as LoginResponse;
}

export async function health(apiBaseUrl: string): Promise<boolean> {
  try {
    const res = await fetch(`${apiBaseUrl.replace(/\/$/, "")}/health`);
    return res.ok;
  } catch {
    return false;
  }
}

export const api = {
  sites: (s: Session) => request<Site[]>(s, "/api/sites"),

  dashboard: (s: Session) =>
    request<JobDto>(s, `/api/insight/dashboard/${s.siteId}`, { method: "GET" }),

  runJob: (s: Session, operation: string, parameters: Record<string, string> = {}) =>
    request<JobDto>(s, "/api/jobs/run-wait", {
      method: "POST",
      body: JSON.stringify({
        siteId: s.siteId,
        operation,
        parameters,
        requestedBy: "mobile-app",
        timeoutSeconds: 90,
      }),
    }),

  search: (s: Session, query: string) =>
    request<JobDto>(s, "/api/insight/search", {
      method: "POST",
      body: JSON.stringify({ siteId: s.siteId, query }),
    }),

  chat: (s: Session, message: string, conversationId?: string) =>
    request<ChatResponse>(s, "/api/insight/chat", {
      method: "POST",
      body: JSON.stringify({ siteId: s.siteId, message, conversationId }),
    }),

  proposals: (s: Session, status?: number) => {
    const q = new URLSearchParams({ siteId: s.siteId! });
    if (status !== undefined) q.set("status", String(status));
    return request<ApprovalProposal[]>(s, `/api/act/proposals?${q}`);
  },

  propose: (
    s: Session,
    body: {
      proposalType: string;
      title: string;
      payloadJson: string;
    }
  ) =>
    request<ApprovalProposal>(s, "/api/act/proposals", {
      method: "POST",
      body: JSON.stringify({
        siteId: s.siteId,
        preparedByUserId: s.userId,
        ...body,
      }),
    }),

  approve: (s: Session, proposalId: string, comment?: string) =>
    request<ApprovalProposal>(s, `/api/act/proposals/${proposalId}/approve`, {
      method: "POST",
      body: JSON.stringify({
        approverUserId: s.userId,
        comment: comment ?? "Approved from mobile",
      }),
    }),

  reject: (s: Session, proposalId: string, reason: string) =>
    request<ApprovalProposal>(s, `/api/act/proposals/${proposalId}/reject`, {
      method: "POST",
      body: JSON.stringify({ approverUserId: s.userId, reason }),
    }),

  writeAudit: (s: Session) =>
    request<unknown[]>(s, `/api/act/write-audit?siteId=${s.siteId}`),
};
