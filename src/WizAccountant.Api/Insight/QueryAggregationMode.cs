using System.Text.RegularExpressions;

namespace WizAccountant.Api.Insight;

/// <summary>
/// Aggregation vs listing mode (DOCS/Sage_AI_Agent_Count_Query_Aggregation_Patch.md).
/// "How many" / "count" → single numeric answer, never transaction grids.
/// </summary>
internal static class QueryAggregationMode
{
    private static readonly Regex CountWord = new(@"\bcount\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly HashSet<string> ListingOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "customer.openitems",
        "customer.list",
        "supplier.openitems",
        "supplier.list",
        "gltransaction.list",
        "inventoryitem.list"
    };

    public static bool IsAggregationQuery(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        var m = message.ToLowerInvariant();
        if (m.Contains("total count") || m.Contains("number of") || m.Contains("how many"))
            return true;

        if (m.Contains("how much") && !WantsExplicitRowListing(m) &&
            (m.Contains("total") || m.Contains("sum") || m.Contains("outstanding") ||
             m.Contains("owed") || m.Contains("owing") || m.Contains("balance") ||
             m.Contains("invoice") || m.Contains("sales") || m.Contains("revenue") ||
             m.Contains("stock") || m.Contains("valuation") ||
             m.Contains("vat") || m.Contains("tax")))
            return true;

        if (m.Contains("count of") || m.Contains("count the"))
            return true;

        if (m.Contains("credit note") &&
            (m.Contains("total") || m.Contains("how many") || m.Contains("number of") ||
             m.Contains("issued") || m.Contains("issues") || m.Contains("raised")))
            return true;

        // Word-boundary count (avoids "discounted", "account", etc.)
        if (CountWord.IsMatch(m) && !WantsExplicitRowListing(m))
            return true;

        return false;
    }

    public static bool WantsExplicitRowListing(string messageLower)
    {
        return messageLower.Contains("list ") || messageLower.StartsWith("list ") ||
               messageLower.Contains("show me") || messageLower.Contains("show all") ||
               messageLower.Contains("get me") || messageLower.Contains("display") ||
               messageLower.Contains("grid") || messageLower.Contains("detail");
    }

    public static bool IsForbiddenListingTarget(string? operation) =>
        operation is not null && ListingOperations.Contains(operation);

    /// <summary>True when UI must not show a results grid.</summary>
    public static bool ShouldSuppressGrid(string? message, string? operation, string? resultJson)
    {
        if (IsCountOnlyResult(resultJson))
            return true;

        if (!string.IsNullOrWhiteSpace(message) && IsAggregationQuery(message))
            return true;

        if (!string.IsNullOrWhiteSpace(message) &&
            SageIntentEngine.Classify(message).PrimaryIntent == SageIntentEngine.IntentType.Aggregation)
            return true;

        if (operation is not null && IsForbiddenListingTarget(operation) &&
            !string.IsNullOrWhiteSpace(message) && IsAggregationQuery(message))
            return true;

        return false;
    }

    /// <summary>Block mis-routed list/open-item ops when user asked for a count.</summary>
    public static bool RejectMisroutedListing(string? message, string? operation) =>
        IsAggregationQuery(message) && IsForbiddenListingTarget(operation);

    public static bool IsCountOnlyResult(string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
            return false;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(resultJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("countOnly", out var c) && c.ValueKind == System.Text.Json.JsonValueKind.True)
                return true;
            if (root.TryGetProperty("aggregationMode", out var a) && a.ValueKind == System.Text.Json.JsonValueKind.True)
                return true;
            return false;
        }
        catch
        {
            return false;
        }
    }

    public static string BuildMisrouteMessage(string message, string operation)
    {
        var year = ChatIntentMatcher.ExtractYearFromMessage(message);
        var yearBit = year.HasValue ? $" in {year.Value}" : "";

        if (message.Contains("discount", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("invoice", StringComparison.OrdinalIgnoreCase))
        {
            return "Aggregation mode: your question asks for a **count** of discounted sales invoices" + yearBit +
                   ", not an open AR transaction list. " +
                   "Re-run after connector rebuild — expected operation: salesinvoice.discount.count (SAGE-SALES-INV-DISC-COUNT-001). " +
                   $"Mis-routed operation blocked: {operation}.";
        }

        if (message.Contains("credit note", StringComparison.OrdinalIgnoreCase))
        {
            return "Aggregation mode: your question asks for a **count/total of sales credit notes**" + yearBit +
                   ", not a customer credit balance listing. " +
                   $"Expected operation: {CreditNoteChatHelper.SalesCreditNoteCountOperation} ({CreditNoteChatHelper.QuerySerial}). " +
                   $"Mis-routed operation blocked: {operation}.";
        }

        return "Aggregation mode: your question asks for a **count** or total, not a transaction listing. " +
               $"Operation {operation} would return rows (e.g. showing 500 of 9000+) and is blocked. " +
               "Use a count-specific question or implement a dedicated aggregation handler for this filter.";
    }
}
