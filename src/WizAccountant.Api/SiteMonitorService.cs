using Microsoft.EntityFrameworkCore;

namespace WizAccountant.Api;

/// <summary>
/// Site SLA dashboard + failed-job alerts. Phase 4 Block 2 — Task 13.
/// Computes per-site health metrics from JobAuditRecord history.
/// </summary>
public sealed class SiteMonitorService(AppDbContext db)
{
    private const int AlertWindowHours = 24;
    private const int FailureAlertThreshold = 3;

    public async Task<List<SiteSlaDto>> GetSiteSlaAsync(CancellationToken ct)
    {
        var staleBefore = DateTimeOffset.UtcNow.AddSeconds(-90);
        var windowStart = DateTimeOffset.UtcNow.AddHours(-AlertWindowHours);

        // Load sites
        var sites = await db.Sites.AsNoTracking().ToListAsync(ct);

        // Load recent job audits (last 24h) for all sites
        var recentAudits = await db.JobAudits.AsNoTracking()
            .Where(a => a.TimestampUtc >= windowStart)
            .ToListAsync(ct);

        var auditsBySite = recentAudits
            .GroupBy(a => a.SiteId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return sites.OrderByDescending(s => s.LastSeenUtc ?? DateTimeOffset.MinValue)
            .Select(site =>
            {
                var audits = auditsBySite.TryGetValue(site.SiteId, out var a) ? a : [];
                var total = audits.Count(a2 => a2.Success.HasValue);
                var failed = audits.Count(a2 => a2.Success == false);
                var succeeded = audits.Count(a2 => a2.Success == true);
                var successRate = total > 0 ? Math.Round((double)succeeded / total * 100, 1) : 100.0;
                var hasAlerts = failed >= FailureAlertThreshold;
                var isOnline = site.IsOnline && site.LastSeenUtc.HasValue &&
                               site.LastSeenUtc.Value >= staleBefore;

                return new SiteSlaDto
                {
                    SiteId = site.SiteId,
                    TenantId = site.TenantId,
                    SiteName = site.SiteName,
                    IsOnline = isOnline,
                    LastSeenUtc = site.LastSeenUtc,
                    JobsLast24h = total,
                    FailedLast24h = failed,
                    SuccessRatePct = successRate,
                    HasAlerts = hasAlerts,
                    Status = isOnline ? (hasAlerts ? "degraded" : "healthy") : "offline"
                };
            }).ToList();
    }

    public async Task<SiteAlertsDto> GetSiteAlertsAsync(Guid siteId, CancellationToken ct)
    {
        var windowStart = DateTimeOffset.UtcNow.AddHours(-AlertWindowHours);

        var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.SiteId == siteId, ct);
        if (site is null) return new SiteAlertsDto { SiteId = siteId };

        var audits = await db.JobAudits.AsNoTracking()
            .Where(a => a.SiteId == siteId && a.TimestampUtc >= windowStart)
            .OrderByDescending(a => a.TimestampUtc)
            .ToListAsync(ct);

        var failedJobs = audits
            .Where(a => a.Success == false)
            .Select(a => new FailedJobAlert
            {
                JobId = a.JobId,
                Operation = a.Operation,
                Detail = a.Detail,
                FailedAtUtc = a.TimestampUtc
            }).ToList();

        return new SiteAlertsDto
        {
            SiteId = siteId,
            SiteName = site.SiteName,
            WindowHours = AlertWindowHours,
            TotalJobs = audits.Count(a => a.Success.HasValue),
            FailedCount = failedJobs.Count,
            AlertThreshold = FailureAlertThreshold,
            TriggersAlert = failedJobs.Count >= FailureAlertThreshold,
            FailedJobs = failedJobs
        };
    }
}

public sealed class SiteSlaDto
{
    public Guid SiteId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public DateTimeOffset? LastSeenUtc { get; set; }
    public int JobsLast24h { get; set; }
    public int FailedLast24h { get; set; }
    public double SuccessRatePct { get; set; }
    public bool HasAlerts { get; set; }
    public string Status { get; set; } = "unknown";
}

public sealed class SiteAlertsDto
{
    public Guid SiteId { get; set; }
    public string SiteName { get; set; } = string.Empty;
    public int WindowHours { get; set; }
    public int TotalJobs { get; set; }
    public int FailedCount { get; set; }
    public int AlertThreshold { get; set; }
    public bool TriggersAlert { get; set; }
    public List<FailedJobAlert> FailedJobs { get; set; } = [];
}

public sealed class FailedJobAlert
{
    public Guid JobId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public DateTimeOffset FailedAtUtc { get; set; }
}
