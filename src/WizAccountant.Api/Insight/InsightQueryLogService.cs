using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace WizAccountant.Api.Insight;

public sealed class InsightQueryLogService(AppDbContext db)
{
    public async Task<Guid> LogAsync(InsightQueryLogEntry entry, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        db.InsightQueryLogs.Add(new InsightQueryLogRecord
        {
            LogId = id,
            TenantId = entry.TenantId,
            SiteId = entry.SiteId,
            ConversationId = entry.ConversationId,
            UserQuery = Truncate(entry.UserQuery, 4000),
            Operation = entry.Operation,
            RouteStatus = entry.RouteStatus,
            BusinessProcess = entry.BusinessProcess,
            ContractJson = entry.ContractJson,
            ToolsUsedJson = entry.ToolsUsedJson,
            JobStatus = entry.JobStatus,
            ErrorSummary = Truncate(entry.ErrorSummary, 2000),
            InsightChatVersion = entry.InsightChatVersion,
            CompatibilityBlocked = entry.CompatibilityBlocked,
            CompatibilityReason = Truncate(entry.CompatibilityReason, 500),
            CreatedAtUtc = DateTimeOffset.UtcNow.ToString("O")
        });
        await db.SaveChangesAsync(ct);
        return id;
    }

    public async Task<(bool Found, bool Duplicate)> SetFeedbackAsync(
        Guid logId,
        string? rating,
        string? reason,
        string? note,
        CancellationToken ct)
    {
        var row = await db.InsightQueryLogs.FindAsync([logId], ct);
        if (row is null) return (false, false);
        if (!string.IsNullOrEmpty(row.FeedbackRating))
            return (true, true);

        row.FeedbackRating = rating;
        var combined = string.IsNullOrWhiteSpace(reason)
            ? note
            : string.IsNullOrWhiteSpace(note)
                ? $"[{reason}]"
                : $"[{reason}] {note}";
        row.FeedbackNote = Truncate(combined, 1000);
        row.FeedbackAtUtc = DateTimeOffset.UtcNow.ToString("O");
        await db.SaveChangesAsync(ct);
        return (true, false);
    }

    private static string? Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? s : s.Length <= max ? s : s[..max];
}

public sealed class InsightQueryLogEntry
{
    public string TenantId { get; init; } = "";
    public Guid SiteId { get; init; }
    public Guid ConversationId { get; init; }
    public string UserQuery { get; init; } = "";
    public string? Operation { get; init; }
    public string RouteStatus { get; init; } = "";
    public string? BusinessProcess { get; init; }
    public string? ContractJson { get; init; }
    public string? ToolsUsedJson { get; init; }
    public string? JobStatus { get; init; }
    public string? ErrorSummary { get; init; }
    public string InsightChatVersion { get; init; } = "";
    public bool CompatibilityBlocked { get; init; }
    public string? CompatibilityReason { get; init; }
}
