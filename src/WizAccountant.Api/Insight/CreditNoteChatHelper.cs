namespace WizAccountant.Api.Insight;

/// <summary>Sales credit note count / total queries (AR InvNum DocType 1).</summary>
internal static class CreditNoteChatHelper
{
    public const string SalesCreditNoteCountOperation = "salescreditnote.count";
    public const string QuerySerial = "SAGE-AR-CREDIT-NOTE-COUNT-001";

    public static bool IsSalesCreditNoteCountQuery(string messageLower)
    {
        if (string.IsNullOrWhiteSpace(messageLower))
            return false;

        if (!ContainsCreditNotePhrase(messageLower))
            return false;

        if (IsSupplierCreditNoteContext(messageLower))
            return false;

        if (IsCustomerCreditBalanceContext(messageLower))
            return false;

        return IsCountOrTotalIntent(messageLower);
    }

    private static bool ContainsCreditNotePhrase(string m) =>
        m.Contains("credit note", StringComparison.Ordinal) ||
        m.Contains("credit notes", StringComparison.Ordinal);

    private static bool IsSupplierCreditNoteContext(string m) =>
        m.Contains("supplier", StringComparison.Ordinal) ||
        m.Contains("purchase", StringComparison.Ordinal) ||
        m.Contains("vendor", StringComparison.Ordinal) ||
        m.Contains("creditor", StringComparison.Ordinal);

    private static bool IsCustomerCreditBalanceContext(string m) =>
        (m.Contains("credit balance", StringComparison.Ordinal) ||
         m.Contains("aged credit", StringComparison.Ordinal) ||
         (m.Contains("credit", StringComparison.Ordinal) && m.Contains("customer", StringComparison.Ordinal) &&
          (m.Contains("oldest", StringComparison.Ordinal) || m.Contains("aged", StringComparison.Ordinal)))) &&
        !ContainsCreditNotePhrase(m);

    private static bool IsCountOrTotalIntent(string m) =>
        QueryAggregationMode.IsAggregationQuery(m) ||
        m.Contains("total credit note", StringComparison.Ordinal) ||
        m.Contains("credit notes issued", StringComparison.Ordinal) ||
        m.Contains("credit notes issues", StringComparison.Ordinal) ||
        m.Contains("credit note issued", StringComparison.Ordinal) ||
        m.Contains("credit note issues", StringComparison.Ordinal) ||
        m.Contains("issued", StringComparison.Ordinal) ||
        m.Contains("issues", StringComparison.Ordinal) ||
        m.Contains("raised", StringComparison.Ordinal);
}
