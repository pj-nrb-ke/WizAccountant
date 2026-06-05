namespace WizAccountant.Api.Insight;

/// <summary>Period application policy (SAGE-DATE-001A). Fiscal-year parsing is Phase 2.</summary>
internal static class InsightPeriodPolicy
{
    /// <summary>Snapshot / as-at handlers — do not apply calendar period ranges from natural language.</summary>
    private static readonly HashSet<string> SnapshotOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "customer.aged.top",
        "customer.aged.credit.top",
        "supplier.aged.top",
        "customer.openitems",
        "supplier.openitems",
        "customer.unpaid.summary",
        "customer.outstanding.debit.top",
        "customer.over.creditlimit",
        "customer.list",
        "supplier.list",
        "inventoryitem.list",
        "customer.credit.balances",
        "supplier.credit.balances",
        "bank.reconcile.variance",
        "ar.invoice.overdue.buckets",
        "ap.invoice.overdue.count",
        "dashboard.summary",
        "search.global"
    };

    /// <summary>Handlers using dedicated relative windows (minDays, horizonDays) — skip general period parser.</summary>
    private static readonly HashSet<string> DedicatedWindowOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "customer.invoice.unpaid.olderthan",
        "supplier.invoice.unpaid.olderthan"
    };

    public static bool IsSnapshotOperation(string? operation) =>
        !string.IsNullOrWhiteSpace(operation) && SnapshotOperations.Contains(operation);

    public static bool UsesDedicatedWindow(string? operation) =>
        !string.IsNullOrWhiteSpace(operation) && DedicatedWindowOperations.Contains(operation);

    public static bool ShouldApplyPeriodRange(string? operation)
    {
        if (string.IsNullOrWhiteSpace(operation))
            return false;
        if (IsSnapshotOperation(operation) || UsesDedicatedWindow(operation))
            return false;

        var cap = HandlerCapabilityRegistry.Get(operation);
        if (cap?.SupportsAsOfDate == true && cap.SupportsDateRangeFilter == false)
            return false;

        return cap?.SupportsDateRangeFilter != false;
    }

    public static string FormatSegmentedBlockMessage(string? operation) =>
        "The requested split-period analysis was understood, but this handler does not yet support non-contiguous period comparison. " +
        "Try the SQL Query tab for custom multi-period analysis, or ask for a single contiguous range (e.g. Q3–Q4 2025).";
}
