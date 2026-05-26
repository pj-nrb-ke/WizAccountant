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
