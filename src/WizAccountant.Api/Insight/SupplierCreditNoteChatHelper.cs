namespace WizAccountant.Api.Insight;

/// <summary>Supplier credit notes — InvNum RTS DocType 3 (SAGE-DISCOVERY-001).</summary>
internal static class SupplierCreditNoteChatHelper
{
    public const string CountOperation = "suppliercreditnote.count";
    public const string ListOperation = "suppliercreditnote.list";
    public const string SummaryOperation = "suppliercreditnote.summary";
    public const string TopOperation = "suppliercreditnote.top";
    public const string QuerySerial = "SAGE-AP-SUPPLIER-CREDIT-NOTE-001";

    public static bool IsSupplierCreditNoteQuery(string messageLower)
    {
        if (string.IsNullOrWhiteSpace(messageLower))
            return false;

        if (!ContainsSupplierCreditNotePhrase(messageLower))
            return false;

        if (IsCustomerCreditNoteContext(messageLower))
            return false;

        if (IsSupplierCreditBalanceContext(messageLower))
            return false;

        return true;
    }

    public static bool IsCountQuery(string m) =>
        QueryAggregationMode.IsAggregationQuery(m) ||
        m.Contains("how many", StringComparison.Ordinal) ||
        m.Contains("number of", StringComparison.Ordinal) ||
        m.Contains("total credit", StringComparison.Ordinal);

    public static bool IsListQuery(string m) =>
        m.Contains("list", StringComparison.Ordinal) ||
        m.Contains("show", StringComparison.Ordinal);

    public static bool IsTopQuery(string m) =>
        (m.Contains("top", StringComparison.Ordinal) ||
         m.Contains("largest", StringComparison.Ordinal) ||
         m.Contains("highest", StringComparison.Ordinal)) &&
        (m.Contains("supplier", StringComparison.Ordinal) || m.Contains("vendor", StringComparison.Ordinal));

    public static bool IsSummaryQuery(string m) =>
        m.Contains("summary", StringComparison.Ordinal) ||
        m.Contains("by month", StringComparison.Ordinal);

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

    private static bool ContainsSupplierCreditNotePhrase(string m) =>
        (m.Contains("credit note", StringComparison.Ordinal) || m.Contains("credit notes", StringComparison.Ordinal)) &&
        (m.Contains("supplier", StringComparison.Ordinal) ||
         m.Contains("vendor", StringComparison.Ordinal) ||
         m.Contains("creditor", StringComparison.Ordinal) ||
         m.Contains("purchase", StringComparison.Ordinal) ||
         m.Contains("rts", StringComparison.Ordinal) ||
         m.Contains("return to supplier", StringComparison.Ordinal));

    private static bool IsCustomerCreditNoteContext(string m) =>
        m.Contains("customer", StringComparison.Ordinal) ||
        m.Contains("sales credit", StringComparison.Ordinal);

    private static bool IsSupplierCreditBalanceContext(string m) =>
        (m.Contains("credit balance", StringComparison.Ordinal) ||
         m.Contains("unallocated credit", StringComparison.Ordinal) ||
         m.Contains("overpaid", StringComparison.Ordinal)) &&
        !m.Contains("credit note", StringComparison.Ordinal);
}
