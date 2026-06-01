using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace WizAccountant.Api.Insight;

public sealed class InsightTriageService(AppDbContext db)
{
    public async Task<InsightTriageReport> BuildReportAsync(string tenantId, int days, CancellationToken ct)
    {
        days = Math.Clamp(days, 1, 90);
        var since = DateTimeOffset.UtcNow.AddDays(-days).ToString("O");

        var rows = await db.InsightQueryLogs.AsNoTracking()
            .Where(x => x.TenantId == tenantId && string.Compare(x.CreatedAtUtc, since) >= 0)
            .ToListAsync(ct);

        var unmatched = rows
            .Where(r => r.RouteStatus is "unmatched" or "mega_digest" or "blocked")
            .GroupBy(r => NormalizeQuery(r.UserQuery))
            .Select(g => new TriageBucket(g.Key, g.Count(), g.First().UserQuery, g.Select(x => x.RouteStatus).Distinct()))
            .OrderByDescending(b => b.Count)
            .Take(25)
            .ToList();

        var failures = rows
            .Where(r => r.JobStatus == "Failed" || !string.IsNullOrEmpty(r.ErrorSummary))
            .GroupBy(r => r.Operation ?? "(none)")
            .Select(g => new TriageBucket(g.Key, g.Count(), g.First().UserQuery, []))
            .OrderByDescending(b => b.Count)
            .Take(15)
            .ToList();

        var negativeFeedback = rows
            .Where(r => r.FeedbackRating is "wrong" or "bad" or "no" or "needs_improvement")
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(20)
            .Select(r => new FeedbackRow(r.LogId, r.UserQuery, r.Operation, r.FeedbackRating, r.FeedbackNote))
            .ToList();

        return new InsightTriageReport(
            tenantId,
            days,
            rows.Count,
            rows.Count(r => !string.IsNullOrEmpty(r.Operation)),
            rows.Count(r => r.RouteStatus == "mega_digest"),
            rows.Count(r => r.CompatibilityBlocked),
            unmatched,
            failures,
            negativeFeedback);
    }

    public string ExportCandidateTestsJson(InsightTriageReport report)
    {
        var cases = new List<object>();
        var n = 0;
        foreach (var bucket in report.UnmatchedQueries.Take(15))
        {
            n++;
            var suggested = GuessOperation(bucket.SampleQuery);
            cases.Add(new
            {
                id = $"cand-{n:D2}",
                query = bucket.SampleQuery,
                preferredOperation = suggested,
                note = "Auto-generated from InsightQueryLog triage — human review required",
                mustNotRoute = new[] { "customer.list", "customer.openitems" }
            });
        }

        return JsonSerializer.Serialize(cases, new JsonSerializerOptions { WriteIndented = true });
    }

    public string FormatMarkdown(InsightTriageReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Insight triage — {report.TenantId} (last {report.Days} days)");
        sb.AppendLine();
        sb.AppendLine($"- Total queries: **{report.TotalQueries}**");
        sb.AppendLine($"- Routed to operation: **{report.RoutedCount}**");
        sb.AppendLine($"- Mega-digest / weak route: **{report.MegaDigestCount}**");
        sb.AppendLine($"- Compatibility blocked: **{report.CompatibilityBlockedCount}**");
        sb.AppendLine();

        sb.AppendLine("## Top unmatched / weak routes");
        foreach (var b in report.UnmatchedQueries)
            sb.AppendLine($"- ({b.Count}) `{Truncate(b.SampleQuery, 80)}` — statuses: {string.Join(", ", b.Statuses)}");

        sb.AppendLine();
        sb.AppendLine("## Job failures by operation");
        foreach (var b in report.FailuresByOperation)
            sb.AppendLine($"- ({b.Count}) `{b.Key}`");

        if (report.NegativeFeedback.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## User feedback (wrong / needs improvement)");
            foreach (var f in report.NegativeFeedback)
                sb.AppendLine($"- [{f.Rating}] `{f.Operation}` — {Truncate(f.Query, 60)} — {f.Note}");
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("**Next steps:** Classify using `DOCS/Query_Triage_Priority.md` · Add queries to `DOCS/Real_Insight_Queries.md` · Weekly SOP: `DOCS/Pilot_Stabilization_Workflow.md`");

        return sb.ToString();
    }

    private static string? GuessOperation(string query)
    {
        var m = query.ToLowerInvariant();
        if (ProductOrderAnalysisChatMatcher.IsProductMonthlyOrderQuery(m))
            return ProductOrderAnalysisChatMatcher.Operation;
        if (CustomerPaymentBehaviorHelper.IsPaymentBehaviorQuery(m))
            return "customer.payment.prompt.top";
        return null;
    }

    private static string NormalizeQuery(string q) =>
        string.Join(' ', q.ToLowerInvariant().Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).Take(12));

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}

public sealed record InsightTriageReport(
    string TenantId,
    int Days,
    int TotalQueries,
    int RoutedCount,
    int MegaDigestCount,
    int CompatibilityBlockedCount,
    IReadOnlyList<TriageBucket> UnmatchedQueries,
    IReadOnlyList<TriageBucket> FailuresByOperation,
    IReadOnlyList<FeedbackRow> NegativeFeedback);

public sealed record TriageBucket(string Key, int Count, string SampleQuery, IEnumerable<string> Statuses);

public sealed record FeedbackRow(Guid LogId, string Query, string? Operation, string? Rating, string? Note);
