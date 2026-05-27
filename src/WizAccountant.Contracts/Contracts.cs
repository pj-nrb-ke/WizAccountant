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
}

/// <summary>P1-26: job claimed by connector REST long-poll.</summary>
public sealed class ConnectorJobPollResponse
{
    public bool HasJob { get; set; }
    public RunJobMessage? Job { get; set; }
}
