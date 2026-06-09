using Microsoft.EntityFrameworkCore;
using WizAccountant.Contracts;

namespace WizAccountant.Api;

/// <summary>
/// Phase 4 Block 3 — multi-company connector support.
/// Fans out a single Insight query to all online sites belonging to a firm or tenant group,
/// returns per-site results for consolidated dashboard views.
/// </summary>
public sealed class MultiSiteQueryService(AppDbContext db, JobService jobs)
{
    /// <summary>
    /// Runs the given operation across all online sites for the specified firm.
    /// Returns per-site results; failed sites report an error and continue.
    /// </summary>
    public async Task<MultiSiteQueryResult> QueryFirmAsync(
        string firmId,
        string operation,
        Dictionary<string, string> parameters,
        int timeoutSeconds,
        CancellationToken ct)
    {
        // Resolve firm → tenants → sites
        var tenantIds = await db.Tenants.AsNoTracking()
            .Where(t => t.FirmId == firmId)
            .Select(t => t.TenantId)
            .ToListAsync(ct);

        var staleBefore = DateTimeOffset.UtcNow.AddSeconds(-90);
        var sites = await db.Sites.AsNoTracking()
            .Where(s => tenantIds.Contains(s.TenantId) && s.IsOnline &&
                        s.LastSeenUtc.HasValue && s.LastSeenUtc.Value >= staleBefore)
            .ToListAsync(ct);

        return await RunParallelAsync(operation, parameters, sites, timeoutSeconds, ct);
    }

    /// <summary>
    /// Runs the given operation across all online sites for the specified tenant.
    /// </summary>
    public async Task<MultiSiteQueryResult> QueryTenantAsync(
        string tenantId,
        string operation,
        Dictionary<string, string> parameters,
        int timeoutSeconds,
        CancellationToken ct)
    {
        var staleBefore = DateTimeOffset.UtcNow.AddSeconds(-90);
        var sites = await db.Sites.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.IsOnline &&
                        s.LastSeenUtc.HasValue && s.LastSeenUtc.Value >= staleBefore)
            .ToListAsync(ct);

        return await RunParallelAsync(operation, parameters, sites, timeoutSeconds, ct);
    }

    private async Task<MultiSiteQueryResult> RunParallelAsync(
        string operation,
        Dictionary<string, string> parameters,
        List<SiteRecord> sites,
        int timeoutSeconds,
        CancellationToken ct)
    {
        if (sites.Count == 0)
            return new MultiSiteQueryResult
            {
                Operation = operation,
                SiteCount = 0,
                Sites = [],
                Note = "No online sites found."
            };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var tasks = sites.Select(async site =>
        {
            try
            {
                var job = await jobs.CreateAndDispatchAsync(new CreateJobRequest
                {
                    SiteId = site.SiteId,
                    Operation = operation,
                    Parameters = parameters
                }, cts.Token);

                var result = await jobs.WaitForJobAsync(job.JobId, timeoutSeconds, cts.Token);
                return new SiteQueryResult
                {
                    SiteId = site.SiteId,
                    TenantId = site.TenantId,
                    SiteName = site.SiteName,
                    Success = result.Status == JobStatus.Completed,
                    ResultJson = result.ResultJson,
                    Error = result.Error
                };
            }
            catch (Exception ex)
            {
                return new SiteQueryResult
                {
                    SiteId = site.SiteId,
                    TenantId = site.TenantId,
                    SiteName = site.SiteName,
                    Success = false,
                    Error = ex.Message.Length > 200 ? ex.Message[..200] : ex.Message
                };
            }
        });

        var results = await Task.WhenAll(tasks);

        return new MultiSiteQueryResult
        {
            Operation = operation,
            SiteCount = results.Length,
            SuccessCount = results.Count(r => r.Success),
            FailedCount = results.Count(r => !r.Success),
            Sites = [.. results]
        };
    }
}

public sealed class MultiSiteQueryResult
{
    public string Operation { get; set; } = string.Empty;
    public int SiteCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<SiteQueryResult> Sites { get; set; } = [];
    public string? Note { get; set; }
}

public sealed class SiteQueryResult
{
    public Guid SiteId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ResultJson { get; set; }
    public string? Error { get; set; }
}
