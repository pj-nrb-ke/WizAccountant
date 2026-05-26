using Microsoft.EntityFrameworkCore;
using WizAccountant.Contracts;

namespace WizAccountant.Api;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<SiteRecord> Sites => Set<SiteRecord>();
    public DbSet<PairingCodeRecord> PairingCodes => Set<PairingCodeRecord>();
    public DbSet<JobRecord> Jobs => Set<JobRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SiteRecord>().HasKey(x => x.SiteId);
        modelBuilder.Entity<PairingCodeRecord>().HasKey(x => x.PairingCodeId);
        modelBuilder.Entity<JobRecord>().HasKey(x => x.JobId);
    }
}

public sealed class SiteRecord
{
    public Guid SiteId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public DateTimeOffset? LastSeenUtc { get; set; }
}

public sealed class PairingCodeRecord
{
    public Guid PairingCodeId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public bool IsUsed { get; set; }
}

public sealed class JobRecord
{
    public Guid JobId { get; set; }
    public Guid SiteId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string ParametersJson { get; set; } = "{}";
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public string? ResultJson { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public string? RequestedBy { get; set; }
    public string? IdempotencyKey { get; set; }
}

