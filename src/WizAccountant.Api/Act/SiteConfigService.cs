using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WizAccountant.Contracts;

namespace WizAccountant.Api.Act;

public sealed class SiteConfigService(AppDbContext db, JobService jobs)
{
    public async Task<SiteConfigDto> SyncAsync(Guid siteId, CancellationToken ct)
    {
        var codes = await jobs.RunAndWaitAsync(new CreateJobRequest
        {
            SiteId = siteId,
            Operation = "transactioncode.list",
            Parameters = new Dictionary<string, string> { ["top"] = "200" },
            RequestedBy = "site-config-sync"
        }, 90, ct);

        var config = new
        {
            transactionCodes = codes.ResultJson,
            syncedAtUtc = DateTimeOffset.UtcNow,
            rollbackNotice = "SDK posts are not auto-reversed by WizAccountant."
        };
        var json = JsonSerializer.Serialize(config);

        var row = await db.SiteConfigs.FindAsync([siteId], ct);
        if (row is null)
        {
            row = new SiteConfigRecord { SiteId = siteId };
            db.SiteConfigs.Add(row);
        }

        row.ConfigJson = json;
        row.SyncedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return new SiteConfigDto { SiteId = siteId, ConfigJson = json, SyncedAtUtc = row.SyncedAtUtc };
    }

    public async Task<SiteConfigDto?> GetAsync(Guid siteId, CancellationToken ct)
    {
        var row = await db.SiteConfigs.AsNoTracking().FirstOrDefaultAsync(s => s.SiteId == siteId, ct);
        return row is null ? null : new SiteConfigDto { SiteId = siteId, ConfigJson = row.ConfigJson, SyncedAtUtc = row.SyncedAtUtc };
    }
}
