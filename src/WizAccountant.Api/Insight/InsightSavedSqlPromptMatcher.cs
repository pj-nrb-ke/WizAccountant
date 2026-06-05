using Microsoft.EntityFrameworkCore;
using WizAccountant.Contracts;

namespace WizAccountant.Api.Insight;

/// <summary>Routes AI Assistant prompts using server saved SQL queries as reference (title / aiPrompt match).</summary>
internal static class InsightSavedSqlPromptMatcher
{
    private static readonly string[] ProductMonthlySqlMarkers =
    [
        "_btblInvoiceLines",
        "StkItem",
        "InvNum",
        "ProductCode",
        "TotalQtySold",
        "TotalSalesValue",
        "TotalQuantity",
        "TotalValue"
    ];

    private static readonly string[] PurchaseProductQuarterlySqlMarkers =
    [
        "_btblInvoiceLines",
        "StkItem",
        "InvNum",
        "DocType",
        "QuarterName",
        "TotalQty",
        "TotalValue",
        "TotalQuantity"
    ];

    public static async Task<string?> TryApplyReferenceAsync(
        AppDbContext db,
        string tenantId,
        Guid siteId,
        string message,
        string? operation,
        Dictionary<string, string> parameters,
        List<string> tools,
        CancellationToken ct)
    {
        var saved = await db.InsightSavedSqlQueries.AsNoTracking()
            .Where(q => q.TenantId == tenantId && q.SiteId == siteId)
            .ToListAsync(ct);

        var match = FindMatch(message, saved);
        if (match is null)
            return operation;

        if (LooksLikePurchaseProductQuarterlySql(match.Sql))
        {
            var msgLower = message.ToLowerInvariant();
            if (!PurchaseProductQuarterlyChatMatcher.TryRoute(message, msgLower, parameters, tools, out var purchaseOp))
            {
                if (!PurchaseProductQuarterlyChatMatcher.IsPurchaseProductQuarterlyQuery(message, msgLower))
                    return operation;
                purchaseOp = PurchaseProductQuarterlyChatMatcher.Operation;
                parameters["message"] = message;
                var period = InsightDateRangeParser.ResolvePeriod(parameters);
                InsightDateRangeParser.ApplyToParameters(period, parameters);
                tools.Add(purchaseOp);
            }

            parameters["savedSqlReferenceTitle"] = match.Title;
            parameters["savedSqlReferenceId"] = match.QueryId.ToString();
            tools.Add($"savedSqlRef:{match.QueryId}");
            return purchaseOp;
        }

        if (!LooksLikeProductMonthlySql(match.Sql))
            return operation;

        var m = message.ToLowerInvariant();
        string routedOp;
        if (!ProductOrderAnalysisChatMatcher.TryRoute(message, m, parameters, tools, out routedOp))
        {
            if (!ProductOrderAnalysisChatMatcher.IsProductMonthlyOrderQuery(m))
                return operation;
            routedOp = ProductOrderAnalysisChatMatcher.Operation;
            parameters["message"] = message;
            parameters["top"] = "500";
            var year = ChatIntentMatcher.ExtractYearFromMessage(message);
            if (year.HasValue)
                parameters["year"] = year.Value.ToString();
            if (InsightDateRangeParser.TryParse(message, year, out var range))
            {
                parameters["dateFrom"] = range.From.ToString("yyyy-MM-dd");
                parameters["dateTo"] = range.To.ToString("yyyy-MM-dd");
            }
            tools.Add(routedOp);
        }

        parameters["savedSqlReferenceTitle"] = match.Title;
        parameters["savedSqlReferenceId"] = match.QueryId.ToString();
        tools.Add($"savedSqlRef:{match.QueryId}");
        return routedOp;
    }

    internal static InsightSavedSqlQueryRecord? FindMatch(string message, IReadOnlyList<InsightSavedSqlQueryRecord> saved)
    {
        var normMessage = Normalize(message);
        InsightSavedSqlQueryRecord? best = null;
        var bestScore = 0.0;

        foreach (var q in saved)
        {
            var score = ScoreMatch(normMessage, q);
            if (score > bestScore)
            {
                bestScore = score;
                best = q;
            }
        }

        return bestScore >= 0.45 ? best : null;
    }

    internal static bool LooksLikeProductMonthlySql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return false;
        var upper = sql.ToUpperInvariant();
        var hits = ProductMonthlySqlMarkers.Count(marker => upper.Contains(marker.ToUpperInvariant(), StringComparison.Ordinal));
        return hits >= 3 && upper.Contains("GROUP BY", StringComparison.Ordinal);
    }

    internal static bool LooksLikePurchaseProductQuarterlySql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return false;
        var upper = sql.ToUpperInvariant();
        if (!upper.Contains("DOCTYPE", StringComparison.Ordinal) || !upper.Contains("STKITEM", StringComparison.Ordinal))
            return false;
        var hits = PurchaseProductQuarterlySqlMarkers.Count(marker =>
            upper.Contains(marker.ToUpperInvariant(), StringComparison.Ordinal));
        return hits >= 4 && (upper.Contains("QUARTER", StringComparison.Ordinal) || upper.Contains("Q1", StringComparison.Ordinal));
    }

    private static double ScoreMatch(string normMessage, InsightSavedSqlQueryRecord q)
    {
        var score = 0.0;
        if (!string.IsNullOrWhiteSpace(q.AiPrompt))
        {
            var normPrompt = Normalize(q.AiPrompt);
            if (normMessage.Contains(normPrompt) || normPrompt.Contains(normMessage))
                score = 1.0;
            else
                score = Math.Max(score, TokenOverlap(normMessage, normPrompt));
        }

        if (!string.IsNullOrWhiteSpace(q.Title))
        {
            var normTitle = Normalize(q.Title);
            if (normTitle.Contains("frequently bought") &&
                normMessage.Contains("frequently") &&
                normMessage.Contains("bought"))
                score = Math.Max(score, 0.85);

            score = Math.Max(score, TokenOverlap(normMessage, normTitle));
            if (normMessage.Contains(normTitle))
                score = Math.Max(score, 0.9);
        }

        return score;
    }

    private static double TokenOverlap(string a, string b)
    {
        var tokensA = a.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 3)
            .ToHashSet(StringComparer.Ordinal);
        var tokensB = b.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 3)
            .ToList();
        if (tokensB.Count == 0 || tokensA.Count == 0)
            return 0;

        var overlap = tokensB.Count(t => tokensA.Contains(t));
        return overlap / (double)Math.Max(tokensB.Count, tokensA.Count);
    }

    private static string Normalize(string text) =>
        new string(text.ToLowerInvariant()
            .Where(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
            .ToArray())
        .Replace("  ", " ")
        .Trim();
}
