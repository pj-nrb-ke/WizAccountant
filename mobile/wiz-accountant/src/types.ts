export type Site = {
  siteId: string;
  tenantId: string;
  siteName: string;
  deviceId: string;
  isOnline: boolean;
  lastSeenUtc?: string;
};

export type LoginResponse = {
  token: string;
  tenantId: string;
  userId: string;
  displayName: string;
  role: string;
};

export type JobDto = {
  jobId: string;
  siteId: string;
  operation: string;
  status: number | string;
  resultJson?: string;
  error?: string;
};

export type ApprovalProposal = {
  proposalId: string;
  siteId: string;
  siteName: string;
  proposalType: string;
  title: string;
  payloadJson: string;
  status: number;
  preparedByUserId: string;
  preparedByName: string;
  approvedByUserId?: string;
  createdAtUtc: string;
};

export type ChatResponse = {
  conversationId: string;
  reply: string;
  toolsUsed: string[];
  dataAsOfUtc: string;
  guardrailNotice: string;
};

export type Session = {
  token: string;
  tenantId: string;
  userId: string;
  displayName: string;
  role: string;
  siteId?: string;
  siteName?: string;
  apiBaseUrl: string;
};
