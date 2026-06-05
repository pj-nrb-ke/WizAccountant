namespace WizAccountant.Api.Insight;

/// <summary>Declared capabilities per handler operation (Layer 3).</summary>
public sealed record HandlerCapability(
    string Operation,
    IReadOnlyList<string> SupportsGroupBy,
    IReadOnlyList<string> SupportsMetrics,
    bool SupportsDateRangeFilter,
    bool SupportsTopN,
    bool SupportsMonthlyBreakdown,
    bool SupportsExplainability,
    IReadOnlyList<string> SupportsOutputShapes,
    string EvidenceSource,
    bool SupportsAsOfDate = false,
    bool SupportsSegmentedPeriods = false,
    bool SupportsRelativePeriods = true,
    bool SupportsHalfYearPeriods = true,
    bool SupportsQuarterPeriods = true)
{
    [Obsolete("Use SupportsDateRangeFilter")]
    public bool SupportsDateFromFilter => SupportsDateRangeFilter;
}

internal static class HandlerCapabilityRegistry
{
    private static readonly Dictionary<string, HandlerCapability> Capabilities =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["salesinvoice.discount.count"] = Cap(
                "salesinvoice.discount.count", [], ["discount"],
                dateFilter: true, shapes: ["aggregation"], evidence: "InvNum"),
            [CreditNoteChatHelper.SalesCreditNoteCountOperation] = Cap(
                CreditNoteChatHelper.SalesCreditNoteCountOperation, [], ["credit_note"],
                dateFilter: true, shapes: ["aggregation"], evidence: "InvNum"),
            [DebitNoteChatHelper.CountOperation] = Cap(
                DebitNoteChatHelper.CountOperation, [], ["debit_note"],
                dateFilter: true, shapes: ["aggregation"], evidence: "PostAR+TrCodes"),
            [DebitNoteChatHelper.ListOperation] = Cap(
                DebitNoteChatHelper.ListOperation, [], ["debit_note"],
                dateFilter: true, topN: true, shapes: ["tabular"], evidence: "PostAR+TrCodes"),
            [DebitNoteChatHelper.SummaryOperation] = Cap(
                DebitNoteChatHelper.SummaryOperation, [], ["debit_note"],
                dateFilter: true, monthly: true, shapes: ["aggregation"], evidence: "PostAR+TrCodes"),
            [DebitNoteChatHelper.TopOperation] = Cap(
                DebitNoteChatHelper.TopOperation, ["customer"], ["debit_note"],
                dateFilter: true, topN: true, shapes: ["ranking"], evidence: "PostAR+TrCodes"),
            [SupplierCreditNoteChatHelper.CountOperation] = Cap(
                SupplierCreditNoteChatHelper.CountOperation, [], ["credit_note"],
                dateFilter: true, shapes: ["aggregation"], evidence: "InvNum RTS"),
            [SupplierCreditNoteChatHelper.ListOperation] = Cap(
                SupplierCreditNoteChatHelper.ListOperation, [], ["credit_note"],
                dateFilter: true, topN: true, shapes: ["tabular"], evidence: "InvNum RTS"),
            [SupplierCreditNoteChatHelper.SummaryOperation] = Cap(
                SupplierCreditNoteChatHelper.SummaryOperation, [], ["credit_note"],
                dateFilter: true, monthly: true, shapes: ["aggregation"], evidence: "InvNum RTS"),
            [SupplierCreditNoteChatHelper.TopOperation] = Cap(
                SupplierCreditNoteChatHelper.TopOperation, ["supplier"], ["credit_note"],
                dateFilter: true, topN: true, shapes: ["ranking"], evidence: "InvNum RTS"),
            [ProductOrderAnalysisChatMatcher.Operation] = Cap(
                ProductOrderAnalysisChatMatcher.Operation,
                ["product", "month"], ["quantity", "value"],
                dateFilter: true, topN: true, monthly: true, explain: false,
                shapes: ["tabular", "monthly_breakdown"],
                evidence: "InvNum+_btblInvoiceLines",
                segmented: true),
            [PurchaseProductQuarterlyChatMatcher.Operation] = Cap(
                PurchaseProductQuarterlyChatMatcher.Operation,
                ["product", "quarter", "month"], ["quantity", "value"],
                dateFilter: true, monthly: true, explain: false,
                shapes: ["tabular", "quarterly_breakdown", "period_breakdown"],
                evidence: "InvNum+_btblInvoiceLines+StkItem",
                segmented: true),
            [DynamicAnalyticalQueryBuilder.PurchaseProductQuarterlyOperation] = Cap(
                DynamicAnalyticalQueryBuilder.PurchaseProductQuarterlyOperation,
                ["product", "quarter"], ["quantity", "value"],
                dateFilter: true, explain: false,
                shapes: ["tabular", "quarterly_breakdown"],
                evidence: "InvNum+_btblInvoiceLines+StkItem",
                segmented: true),

            ["customer.payment.prompt.top"] = Cap(
                "customer.payment.prompt.top", ["customer"], ["payment_discipline"],
                dateFilter: true, topN: true, shapes: ["ranking"], evidence: "InvNum+PostAR"),
            ["customer.payment.late.top"] = Cap(
                "customer.payment.late.top", ["customer"], ["payment_discipline"],
                dateFilter: true, topN: true, shapes: ["ranking"], evidence: "InvNum+PostAR"),
            ["customer.payment.behavior.summary"] = Cap(
                "customer.payment.behavior.summary", [], ["payment_discipline"],
                dateFilter: true, shapes: ["aggregation"], evidence: "InvNum+PostAR"),
            [CustomerCollectionsHelper.SummaryOperation] = Cap(
                CustomerCollectionsHelper.SummaryOperation, [], ["collections"],
                dateFilter: true, monthly: true, shapes: ["aggregation"], evidence: "PostAR+Client",
                segmented: true),
            [CustomerCollectionsHelper.ByMonthOperation] = Cap(
                CustomerCollectionsHelper.ByMonthOperation, ["month"], ["collections"],
                dateFilter: true, monthly: true, shapes: ["tabular"], evidence: "PostAR",
                segmented: true),
            [CustomerCollectionsHelper.ByCustomerOperation] = Cap(
                CustomerCollectionsHelper.ByCustomerOperation, ["customer"], ["collections"],
                dateFilter: true, topN: true, shapes: ["tabular"], evidence: "PostAR+Client",
                segmented: true),
            [CustomerCollectionsHelper.TopOperation] = Cap(
                CustomerCollectionsHelper.TopOperation, ["customer"], ["collections"],
                dateFilter: true, topN: true, shapes: ["ranking"], evidence: "PostAR+Client",
                segmented: true),
            ["customer.outstanding.debit.top"] = Cap(
                "customer.outstanding.debit.top", ["customer"], ["balance"],
                topN: true, asOf: true, shapes: ["ranking"], evidence: "PostAR"),
            ["customer.unpaid.summary"] = Cap(
                "customer.unpaid.summary", ["customer"], ["balance"],
                topN: true, asOf: true, shapes: ["ranking"], evidence: "CustomerTransaction"),
            [ChatIntentMatcher.SupplierUnpaidCountOp] = Cap(
                ChatIntentMatcher.SupplierUnpaidCountOp, ["supplier"], ["balance"],
                topN: false, asOf: true, shapes: ["aggregation", "single_count_with_total_amount"],
                evidence: "SupplierTransaction"),
            [ChatIntentMatcher.SupplierUnpaidListOp] = Cap(
                ChatIntentMatcher.SupplierUnpaidListOp, ["supplier"], ["balance"],
                topN: true, asOf: true, shapes: ["tabular"], evidence: "SupplierTransaction"),
            [ChatIntentMatcher.SupplierUnpaidTopOp] = Cap(
                ChatIntentMatcher.SupplierUnpaidTopOp, ["supplier"], ["balance"],
                topN: true, asOf: true, shapes: ["ranking"], evidence: "SupplierTransaction"),
            [ChatIntentMatcher.SupplierUnpaidSummaryOp] = Cap(
                ChatIntentMatcher.SupplierUnpaidSummaryOp, ["supplier"], ["balance"],
                topN: true, asOf: true, shapes: ["ranking", "aggregation"], evidence: "SupplierTransaction"),
            ["customer.sales.top"] = Cap(
                "customer.sales.top", ["customer"], ["value"],
                dateFilter: true, topN: true, shapes: ["ranking"], evidence: "InvNum"),
            ["customer.aged.top"] = Cap(
                "customer.aged.top", ["customer"], ["balance"],
                topN: true, asOf: true, shapes: ["ranking"], evidence: "PostAR"),
            ["supplier.aged.top"] = Cap(
                "supplier.aged.top", ["supplier"], ["balance"],
                topN: true, asOf: true, shapes: ["ranking"], evidence: "PostAP"),
            ["supplier.purchases.top"] = Cap(
                "supplier.purchases.top", ["supplier"], ["value"],
                dateFilter: true, topN: true, shapes: ["ranking"], evidence: "InvNum"),
            ["inventory.movement.top"] = Cap(
                "inventory.movement.top", ["product"], ["quantity", "value"],
                dateFilter: true, topN: true, shapes: ["ranking"], evidence: "StockTransactions"),
            ["gl.expense.top"] = Cap(
                "gl.expense.top", ["gl"], ["value"],
                dateFilter: true, topN: true, shapes: ["ranking"], evidence: "PostGL"),
            ["gl.expense.trend"] = Cap(
                "gl.expense.trend", ["gl"], ["value"],
                dateFilter: true, shapes: ["tabular"], evidence: "PostGL"),
            ["bank.cashbook"] = Cap(
                "bank.cashbook", ["bank"], ["value"],
                dateFilter: true, shapes: ["listing"], evidence: "Cashbook"),
            ["bank.daily.cash"] = Cap(
                "bank.daily.cash", ["bank"], ["value"],
                dateFilter: true, shapes: ["tabular"], evidence: "Cashbook"),
            ["purchaseinvoice.count"] = Cap(
                "purchaseinvoice.count", [], ["value"],
                dateFilter: true, shapes: ["aggregation"], evidence: "InvNum"),
            ["vat.payable.estimate"] = Cap(
                "vat.payable.estimate", [], ["vat"],
                dateFilter: true, shapes: ["aggregation"], evidence: "InvNum"),
            ["vat.reconcile"] = Cap(
                "vat.reconcile", ["vat", "gl"], ["variance"],
                explain: true, shapes: ["explainability", "reconciliation"], evidence: "InvNum+GL"),
            ["vat.anomalies"] = Cap(
                "vat.anomalies", [], ["vat"],
                dateFilter: true, explain: true, shapes: ["explainability"], evidence: "InvNum"),
            ["inventory.gl.reconcile"] = Cap(
                "inventory.gl.reconcile", ["inventory", "gl"], ["variance"],
                explain: true, shapes: ["explainability", "reconciliation"], evidence: "PostGL+valuation"),
            ["inventory.gl.explain"] = Cap(
                "inventory.gl.explain", ["inventory", "gl"], ["variance"],
                explain: true, shapes: ["explainability"], evidence: "PostGL+valuation"),
            ["ar.gl.reconcile"] = Cap(
                "ar.gl.reconcile", ["ar", "gl"], ["variance"],
                explain: true, shapes: ["explainability", "reconciliation"], evidence: "AR+GL"),
            ["ap.gl.reconcile"] = Cap(
                "ap.gl.reconcile", ["ap", "gl"], ["variance"],
                explain: true, shapes: ["explainability", "reconciliation"], evidence: "AP+GL"),
            ["bank.reconcile.variance"] = Cap(
                "bank.reconcile.variance", ["bank"], ["variance"],
                explain: true, asOf: true, shapes: ["explainability", "reconciliation"], evidence: "Bank+GL"),
            ["treasury.dashboard"] = Cap(
                "treasury.dashboard", [], ["cashflow"],
                shapes: ["dashboard"], evidence: "InvNum+Bank"),
            ["inventory.slow.moving.top"] = Cap(
                "inventory.slow.moving.top", ["product"], ["quantity"],
                topN: true, shapes: ["ranking"], evidence: "StockTransactions"),
            ["inventory.nonmoving"] = Cap(
                "inventory.nonmoving", ["product"], ["quantity"],
                shapes: ["listing"], evidence: "StockTransactions"),
        };

    private static HandlerCapability Cap(
        string operation,
        string[] groupBy,
        string[] metrics,
        bool dateFilter = false,
        bool topN = false,
        bool monthly = false,
        bool explain = false,
        bool asOf = false,
        bool segmented = false,
        string[]? shapes = null,
        string evidence = "unknown") =>
        new(operation, groupBy, metrics, dateFilter, topN, monthly, explain, shapes ?? [], evidence,
            SupportsAsOfDate: asOf,
            SupportsSegmentedPeriods: segmented,
            SupportsRelativePeriods: true,
            SupportsHalfYearPeriods: true,
            SupportsQuarterPeriods: true);

    public static HandlerCapability? Get(string? operation)
    {
        if (string.IsNullOrWhiteSpace(operation))
            return null;
        return Capabilities.TryGetValue(operation, out var cap) ? cap : null;
    }

    public static HandlerCapability GetOrPermissive(string operation) =>
        Get(operation) ?? Cap(operation, [], [], evidence: "unknown");
}
