namespace WizAccountant.Contracts;

public enum JobStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3
}

public sealed class CreatePairingCodeRequest
{
    public string TenantId { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public int ExpiresInMinutes { get; set; } = 15;
}

public sealed class PairingCodeResponse
{
    public string PairingCode { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
}

public sealed class PairSiteRequest
{
    public string PairingCode { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string ConnectorVersion { get; set; } = string.Empty;
}

public sealed class PairSiteResponse
{
    public Guid SiteId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string HubPath { get; set; } = "/hubs/connector";
}

public sealed class SiteDto
{
    public Guid SiteId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public DateTimeOffset? LastSeenUtc { get; set; }
}

public sealed class CreateJobRequest
{
    public Guid SiteId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public Dictionary<string, string> Parameters { get; set; } = new();
    public string? RequestedBy { get; set; }
    public string? IdempotencyKey { get; set; }
}

public sealed class JobDto
{
    public Guid JobId { get; set; }
    public Guid SiteId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public JobStatus Status { get; set; }
    public string? ResultJson { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}

public sealed class ConnectorHeartbeat
{
    public Guid SiteId { get; set; }
    public string ConnectorVersion { get; set; } = string.Empty;
    public string? EvolutionVersion { get; set; }
    public string? CompanyName { get; set; }
}

public sealed class RunJobMessage
{
    public Guid JobId { get; set; }
    public Guid SiteId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public Dictionary<string, string> Parameters { get; set; } = new();
    public string? IdempotencyKey { get; set; }
}

public sealed class SubmitJobResultRequest
{
    public string? ResultJson { get; set; }
    public string? Error { get; set; }
}

/// <summary>P1-24: submit a job and wait for the connector (sync read, capped timeout).</summary>
public sealed class RunJobWaitRequest
{
    public Guid SiteId { get; set; }
    public string Operation { get; set; } = "site.health";
    public Dictionary<string, string> Parameters { get; set; } = new();
    public string? RequestedBy { get; set; }
    public int TimeoutSeconds { get; set; } = 60;
}

public sealed class JobAuditDto
{
    public Guid AuditId { get; set; }
    public Guid JobId { get; set; }
    public Guid SiteId { get; set; }
    public string SiteName { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? RequestedBy { get; set; }
    public bool? Success { get; set; }
    public string? Detail { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
}

public sealed class StartLocalProgramsResponse
{
    public bool Ok { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Started { get; set; } = new();
    public List<string> AlreadyRunning { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public sealed class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "Preparer";
}

public sealed class TenantDto
{
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class UserDto
{
    public Guid UserId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "Preparer";
}

/// <summary>P1-26: job claimed by connector REST long-poll.</summary>
public sealed class ConnectorJobPollResponse
{
    public bool HasJob { get; set; }
    public RunJobMessage? Job { get; set; }
}

public sealed class InsightSearchRequest
{
    public Guid SiteId { get; set; }
    public string Query { get; set; } = string.Empty;
}

public sealed class InsightSearchResponse
{
    public string Query { get; set; } = string.Empty;
    public List<InsightSearchHit> Hits { get; set; } = new();
    public DateTimeOffset DataAsOfUtc { get; set; }
}

public sealed class InsightSearchHit
{
    public string Type { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class ChatMessageRequest
{
    public Guid SiteId { get; set; }
    public Guid? ConversationId { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class ChatGridDto
{
    public List<string> Columns { get; set; } = new();
    public List<Dictionary<string, string?>> Rows { get; set; } = new();
}

public sealed class ChatMessageResponse
{
    public Guid ConversationId { get; set; }
    public string Reply { get; set; } = string.Empty;
    /// <summary>Narrative explanation for the right-hand panel.</summary>
    public string Explanation { get; set; } = string.Empty;
    /// <summary>Tabular results for the grid (when available).</summary>
    public ChatGridDto? Grid { get; set; }
    public List<string> ToolsUsed { get; set; } = new();
    public List<string> Citations { get; set; } = new();
    public DateTimeOffset DataAsOfUtc { get; set; }
    public string GuardrailNotice { get; set; } = "Read-only assistant. No posting to Sage.";
    /// <summary>Changes when chat routing is updated — compare with /health insightChatVersion.</summary>
    public string InsightChatVersion { get; set; } = string.Empty;
    /// <summary>Set when query logging is enabled — use for POST /api/insight/feedback.</summary>
    public Guid? QueryLogId { get; set; }
}

public sealed class InsightFeedbackRequest
{
    public Guid QueryLogId { get; set; }
    /// <summary>helpful | wrong | needs_improvement</summary>
    public string Rating { get; set; } = "";
    /// <summary>Quick reason: wrong_route, wrong_numbers, too_many_rows, missing_analysis, crashed, not_business_aware, incomplete_answer</summary>
    public string? Reason { get; set; }
    public string? Note { get; set; }
}

public sealed class ConversationDto
{
    public Guid ConversationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class NotificationStubRequest
{
    public Guid SiteId { get; set; }
    public string EventType { get; set; } = "site.offline";
    public string? Email { get; set; }
}

public enum ApprovalStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Posted = 3,
    Failed = 4
}

public sealed class ProposeApprovalRequest
{
    public Guid SiteId { get; set; }
    public Guid PreparedByUserId { get; set; }
    public string ProposalType { get; set; } = "gl.journal";
    public string Title { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string? IdempotencyKey { get; set; }
    public string? Comment { get; set; }
}

public sealed class ApprovalProposalDto
{
    public Guid ProposalId { get; set; }
    public Guid SiteId { get; set; }
    public string SiteName { get; set; } = string.Empty;
    public string ProposalType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public ApprovalStatus Status { get; set; }
    public Guid PreparedByUserId { get; set; }
    public string PreparedByName { get; set; } = string.Empty;
    public Guid? ApprovedByUserId { get; set; }
    public string? ApprovedByName { get; set; }
    public string? IdempotencyKey { get; set; }
    public Guid? JobId { get; set; }
    public string? Comment { get; set; }
    public string? RejectReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ResolvedAtUtc { get; set; }
}

public sealed class ApproveProposalRequest
{
    public Guid ApproverUserId { get; set; }
    public string? Comment { get; set; }
}

public sealed class RejectProposalRequest
{
    public Guid ApproverUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class AiDraftRequest
{
    public Guid SiteId { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class AiDraftResponse
{
    public string ProposalType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string Notice { get; set; } = "Draft only — submit for approval before posting to Sage.";
}

public sealed class WriteAuditDto
{
    public Guid WriteAuditId { get; set; }
    public Guid ProposalId { get; set; }
    public Guid SiteId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public Guid? PreparerUserId { get; set; }
    public Guid? ApproverUserId { get; set; }
    public string? EvolutionRef { get; set; }
    public bool Success { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
}

public sealed class WorkflowTemplateDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> Steps { get; set; } = new();
}

public sealed class SiteConfigDto
{
    public Guid SiteId { get; set; }
    public string ConfigJson { get; set; } = "{}";
    public DateTimeOffset SyncedAtUtc { get; set; }
}
