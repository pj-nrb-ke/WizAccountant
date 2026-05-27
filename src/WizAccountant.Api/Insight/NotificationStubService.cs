using WizAccountant.Contracts;

namespace WizAccountant.Api.Insight;

public sealed class NotificationStubService(ILogger<NotificationStubService> logger, AppDbContext db)
{
    public async Task SendSiteEventAsync(NotificationStubRequest request, CancellationToken ct)
    {
        var site = await db.Sites.FindAsync([request.SiteId], ct);
        if (site is null) throw new InvalidOperationException("Site not found.");

        db.NotificationLogs.Add(new NotificationLogRecord
        {
            NotificationId = Guid.NewGuid(),
            SiteId = request.SiteId,
            EventType = request.EventType,
            Email = request.Email ?? "stub@wizaccountant.local",
            Status = "logged-stub",
            TimestampUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "P2 notification stub: {EventType} for site {SiteName} → {Email}",
            request.EventType, site.SiteName, request.Email);
    }
}
