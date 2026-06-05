namespace WizAccountant.Api.Insight;

/// <summary>Historical AR customer collections / receipts (SAGE-PATCH-010). Not treasury forecast or payment discipline.</summary>
internal static class CustomerCollectionsHelper
{
    public const string SummaryOperation = "customer.collections.summary";
    public const string ByMonthOperation = "customer.collections.by.month";
    public const string ByCustomerOperation = "customer.collections.by.customer";
    public const string TopOperation = "customer.collections.top";

    public static bool IsCustomerCollectionsQuery(string messageLower)
    {
        if (string.IsNullOrWhiteSpace(messageLower))
            return false;

        if (IsCollectionsForecastQuery(messageLower))
            return false;

        if (CustomerPaymentBehaviorHelper.IsPaymentBehaviorQuery(messageLower))
            return false;

        return ContainsCollectionReceiptPhrase(messageLower);
    }

    public static bool IsByCustomerQuery(string m) =>
        m.Contains("by customer", StringComparison.Ordinal) ||
        m.Contains("per customer", StringComparison.Ordinal) ||
        m.Contains("each customer", StringComparison.Ordinal);

    public static bool IsByMonthQuery(string m) =>
        m.Contains("by month", StringComparison.Ordinal) ||
        m.Contains("per month", StringComparison.Ordinal) ||
        m.Contains("monthly breakdown", StringComparison.Ordinal);

    public static bool IsTopCustomersQuery(string m) =>
        (m.Contains("top ", StringComparison.Ordinal) ||
         m.Contains("largest", StringComparison.Ordinal) ||
         m.Contains("highest", StringComparison.Ordinal)) &&
        m.Contains("customer", StringComparison.Ordinal) &&
        (m.Contains("collection", StringComparison.Ordinal) || m.Contains("receipt", StringComparison.Ordinal));

    public static string ResolveOperation(string messageLower)
    {
        if (IsTopCustomersQuery(messageLower))
            return TopOperation;
        if (IsByCustomerQuery(messageLower))
            return ByCustomerOperation;
        if (IsByMonthQuery(messageLower))
            return ByMonthOperation;
        return SummaryOperation;
    }

    public static bool IsCollectionsForecastQuery(string messageLower) =>
        (messageLower.Contains("expected", StringComparison.Ordinal) && messageLower.Contains("collection", StringComparison.Ordinal)) ||
        messageLower.Contains("expected collection", StringComparison.Ordinal) ||
        messageLower.Contains("expected customer collection", StringComparison.Ordinal) ||
        messageLower.Contains("collections next", StringComparison.Ordinal) ||
        messageLower.Contains("collection next", StringComparison.Ordinal) ||
        (messageLower.Contains("next month", StringComparison.Ordinal) && messageLower.Contains("collection", StringComparison.Ordinal)) ||
        messageLower.Contains("collections forecast", StringComparison.Ordinal) ||
        messageLower.Contains("forecast collection", StringComparison.Ordinal) ||
        (messageLower.Contains("forecast", StringComparison.Ordinal) && messageLower.Contains("collection", StringComparison.Ordinal) &&
         !ContainsHistoricalCollectionSignal(messageLower)) ||
        (messageLower.Contains("treasury", StringComparison.Ordinal) && messageLower.Contains("collection", StringComparison.Ordinal) &&
         !ContainsHistoricalCollectionSignal(messageLower));

    private static bool ContainsHistoricalCollectionSignal(string m) =>
        m.Contains("q1", StringComparison.Ordinal) || m.Contains("q2", StringComparison.Ordinal) ||
        m.Contains("q3", StringComparison.Ordinal) || m.Contains("q4", StringComparison.Ordinal) ||
        m.Contains("was the", StringComparison.Ordinal) || m.Contains("were the", StringComparison.Ordinal) ||
        m.Contains("in 20", StringComparison.Ordinal) || m.Contains("received in", StringComparison.Ordinal) ||
        m.Contains("collected in", StringComparison.Ordinal) || m.Contains("by month", StringComparison.Ordinal) ||
        m.Contains("by customer", StringComparison.Ordinal) || m.Contains("h1", StringComparison.Ordinal) ||
        m.Contains("h2", StringComparison.Ordinal);

    private static bool ContainsCollectionReceiptPhrase(string m)
    {
        if (m.Contains("collection", StringComparison.Ordinal))
        {
            if (m.Contains("customer", StringComparison.Ordinal) || m.Contains("from customer", StringComparison.Ordinal))
                return true;
            if (ContainsHistoricalCollectionSignal(m) || IsByMonthQuery(m) || IsByCustomerQuery(m))
                return true;
        }

        if (m.Contains("receipt", StringComparison.Ordinal) && m.Contains("customer", StringComparison.Ordinal))
            return true;

        if (m.Contains("cash collected", StringComparison.Ordinal) && m.Contains("customer", StringComparison.Ordinal))
            return true;

        if (m.Contains("money received", StringComparison.Ordinal) && m.Contains("customer", StringComparison.Ordinal))
            return true;

        if (m.Contains("customer payments received", StringComparison.Ordinal) ||
            m.Contains("payments received from customer", StringComparison.Ordinal))
            return true;

        if (m.Contains("amount collected", StringComparison.Ordinal) && m.Contains("customer", StringComparison.Ordinal))
            return true;

        return false;
    }
}
