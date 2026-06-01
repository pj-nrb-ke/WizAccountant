namespace WizAccountant.Api.Insight;

/// <summary>
/// TOP/ranking queries must respect row limits — never default to 500-row master dumps.
/// </summary>
internal static class RankingQueryPolicy
{
    public const int DefaultTop = 5;
    public const int MaxTop = 50;
    public const int MaxGridRows = 50;

    private static readonly HashSet<string> BulkListingOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "customer.list",
        "supplier.list",
        "customer.openitems",
        "supplier.openitems",
        "gltransaction.list",
        "inventoryitem.list"
    };

    private static readonly HashSet<string> RankingOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "customer.aged.top",
        "customer.unpaid.summary",
        "customer.credit.balances",
        "supplier.aged.top",
        "supplier.credit.balances",
        "supplier.outstanding.top",
        "supplier.payments.top",
        "supplier.purchases.top",
        "purchaseinvoice.top",
        "purchaseinvoice.discount.top",
        "inventory.slow.moving.top",
        "inventory.value.top",
        "inventory.movement.top",
        "warehouse.value.summary",
        "warehouse.transfer.summary"
    };

    public static bool IsRankingClassification(SageIntentEngine.Classification c) =>
        c.PrimaryIntent == SageIntentEngine.IntentType.Ranking &&
        c.Confidence >= 0.45;

    public static void ApplyRowLimits(string message, SageIntentEngine.Classification? classification, Dictionary<string, string> parameters)
    {
        var m = message.ToLowerInvariant();
        var isRanking = classification is not null && IsRankingClassification(classification) ||
                        IsRankingPhrase(m);

        if (!isRanking)
            return;

        var requested = ChatIntentMatcher.ResolveTopCount(m, DefaultTop);
        parameters["top"] = Math.Clamp(requested, 1, MaxTop).ToString();
        parameters["rankingMode"] = "true";
    }

    public static bool RejectMisroutedBulkList(SageIntentEngine.Classification classification, string? operation)
    {
        if (string.IsNullOrEmpty(operation))
            return false;

        if (!IsRankingClassification(classification))
            return false;

        return BulkListingOperations.Contains(operation);
    }

    public static string BuildBlockedMessage(string message, string operation, SageIntentEngine.Classification classification)
    {
        var top = ChatIntentMatcher.ResolveTopCount(message.ToLowerInvariant(), DefaultTop);
        return "Ranking mode: your question asks for a **top / ranked** result, not a bulk master or open-items dump. " +
               $"Expected at most **{top}** row(s) from a dedicated ranking handler (confidence {classification.Confidence:P0} for {classification.PrimaryIntent}). " +
               $"Mis-routed operation blocked: {operation}.";
    }

    public static bool ShouldCapGrid(string? operation, SageIntentEngine.Classification? classification, string? message)
    {
        if (operation is not null && RankingOperations.Contains(operation))
            return true;

        if (classification is not null && IsRankingClassification(classification))
            return true;

        return message is not null && IsRankingPhrase(message.ToLowerInvariant());
    }

    public static int ResolveMaxGridRows(string? operation, Dictionary<string, string> parameters)
    {
        if (parameters.TryGetValue("top", out var topStr) && int.TryParse(topStr, out var top))
            return Math.Clamp(top, 1, MaxGridRows);

        if (operation is not null && RankingOperations.Contains(operation))
            return DefaultTop;

        return MaxGridRows;
    }

    private static bool IsRankingPhrase(string m) =>
        m.Contains("top ") || m.Contains("highest") || m.Contains("oldest") ||
        m.Contains("lowest") || m.Contains("biggest") || m.Contains("largest");
}
