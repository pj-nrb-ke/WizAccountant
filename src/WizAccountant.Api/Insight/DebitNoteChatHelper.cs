namespace WizAccountant.Api.Insight;

/// <summary>Customer debit notes — PostAR TrCode DN (SAGE-DISCOVERY-001).</summary>
internal static class DebitNoteChatHelper
{
    public const string CountOperation = "salesdebitnote.count";
    public const string ListOperation = "salesdebitnote.list";
    public const string SummaryOperation = "salesdebitnote.summary";
    public const string TopOperation = "salesdebitnote.top";
    public const string QuerySerial = "SAGE-AR-DEBIT-NOTE-001";

    public static bool IsSalesDebitNoteQuery(string messageLower)
    {
        if (string.IsNullOrWhiteSpace(messageLower))
            return false;

        if (!ContainsDebitNotePhrase(messageLower))
            return false;

        if (IsSupplierContext(messageLower))
            return false;

        if (messageLower.Contains("outstanding", StringComparison.Ordinal) &&
            !ContainsDebitNotePhrase(messageLower))
            return false;

        return true;
    }

    public static bool IsCountQuery(string m) =>
        QueryAggregationMode.IsAggregationQuery(m) ||
        m.Contains("how many", StringComparison.Ordinal) ||
        m.Contains("number of", StringComparison.Ordinal) ||
        m.Contains("total debit", StringComparison.Ordinal);

    public static bool IsListQuery(string m) =>
        m.Contains("list", StringComparison.Ordinal) ||
        m.Contains("show", StringComparison.Ordinal) ||
        m.Contains("detail", StringComparison.Ordinal);

    public static bool IsTopQuery(string m) =>
        (m.Contains("top", StringComparison.Ordinal) ||
         m.Contains("largest", StringComparison.Ordinal) ||
         m.Contains("highest", StringComparison.Ordinal)) &&
        m.Contains("customer", StringComparison.Ordinal);

    public static bool IsSummaryQuery(string m) =>
        m.Contains("summary", StringComparison.Ordinal) ||
        m.Contains("by month", StringComparison.Ordinal) ||
        m.Contains("monthly", StringComparison.Ordinal);

    public static string ResolveOperation(string messageLower)
    {
        if (IsTopQuery(messageLower))
            return TopOperation;
        if (IsListQuery(messageLower) && !IsCountQuery(messageLower))
            return ListOperation;
        if (IsSummaryQuery(messageLower))
            return SummaryOperation;
        if (IsCountQuery(messageLower))
            return CountOperation;
        return SummaryOperation;
    }

    private static bool ContainsDebitNotePhrase(string m) =>
        m.Contains("debit note", StringComparison.Ordinal) ||
        m.Contains("debit notes", StringComparison.Ordinal);

    private static bool IsSupplierContext(string m) =>
        m.Contains("supplier", StringComparison.Ordinal) ||
        m.Contains("vendor", StringComparison.Ordinal) ||
        m.Contains("creditor", StringComparison.Ordinal) ||
        m.Contains("purchase", StringComparison.Ordinal);
}
