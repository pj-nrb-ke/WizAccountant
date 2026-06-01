namespace WizAccountant.Api.Insight;

/// <summary>Declared capabilities per handler operation (Layer 3).</summary>
public sealed record HandlerCapability(
    string Operation,
    IReadOnlyList<string> SupportsGroupBy,
    IReadOnlyList<string> SupportsMetrics,
    bool SupportsDateFromFilter,
    bool SupportsTopN,
    bool SupportsMonthlyBreakdown,
    bool SupportsExplainability,
    IReadOnlyList<string> SupportsOutputShapes,
    string EvidenceSource);

internal static class HandlerCapabilityRegistry
{
    private static readonly Dictionary<string, HandlerCapability> Capabilities =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [ProductOrderAnalysisChatMatcher.Operation] = Cap(
                ProductOrderAnalysisChatMatcher.Operation,
                ["product", "month"], ["quantity", "value"],
                dateFilter: true, topN: true, monthly: true, explain: false,
                shapes: ["tabular", "monthly_breakdown"],
                evidence: "InvNum+_btblInvoiceLines"),

            ["customer.payment.prompt.top"] = Cap(
                "customer.payment.prompt.top", ["customer"], ["payment_discipline"],
                topN: true, shapes: ["ranking"], evidence: "InvNum+PostAR"),
            ["customer.payment.late.top"] = Cap(
                "customer.payment.late.top", ["customer"], ["payment_discipline"],
                topN: true, shapes: ["ranking"], evidence: "InvNum+PostAR"),
            ["customer.payment.behavior.summary"] = Cap(
                "customer.payment.behavior.summary", [], ["payment_discipline"],
                shapes: ["aggregation"], evidence: "InvNum+PostAR"),
            ["customer.outstanding.debit.top"] = Cap(
                "customer.outstanding.debit.top", ["customer"], ["balance"],
                topN: true, shapes: ["ranking"], evidence: "PostAR"),
            ["customer.unpaid.summary"] = Cap(
                "customer.unpaid.summary", ["customer"], ["balance"],
                topN: true, shapes: ["ranking"], evidence: "CustomerTransaction"),
            ["customer.sales.top"] = Cap(
                "customer.sales.top", ["customer"], ["value"],
                dateFilter: true, topN: true, shapes: ["ranking"], evidence: "InvNum"),
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
                explain: true, shapes: ["explainability", "reconciliation"], evidence: "Bank+GL"),
            ["treasury.dashboard"] = Cap(
                "treasury.dashboard", [], ["cashflow"],
                shapes: ["dashboard"], evidence: "InvNum+Bank"),
            ["inventory.slow.moving.top"] = Cap(
                "inventory.slow.moving.top", ["product"], ["quantity"],
                topN: true, shapes: ["ranking"], evidence: "StockTransactions"),
            ["inventory.nonmoving"] = Cap(
                "inventory.nonmoving", ["product"], ["quantity"],
                shapes: ["listing"], evidence: "StockTransactions"),
            ["salesinvoice.discount.count"] = Cap(
                "salesinvoice.discount.count", [], ["discount"],
                dateFilter: true, shapes: ["aggregation"], evidence: "InvNum"),
        };

    private static HandlerCapability Cap(
        string operation,
        string[] groupBy,
        string[] metrics,
        bool dateFilter = false,
        bool topN = false,
        bool monthly = false,
        bool explain = false,
        string[]? shapes = null,
        string evidence = "unknown") =>
        new(operation, groupBy, metrics, dateFilter, topN, monthly, explain, shapes ?? [], evidence);

    public static HandlerCapability? Get(string? operation)
    {
        if (string.IsNullOrWhiteSpace(operation))
            return null;
        return Capabilities.TryGetValue(operation, out var cap) ? cap : null;
    }

    public static HandlerCapability GetOrPermissive(string operation) =>
        Get(operation) ?? Cap(operation, [], [], evidence: "unknown");
}
