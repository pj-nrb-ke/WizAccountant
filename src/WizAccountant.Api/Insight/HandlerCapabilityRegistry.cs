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

            // ── AR analytical ─────────────────────────────────────────────────
            ["ar.invoice.overdue.buckets"] = Cap(
                "ar.invoice.overdue.buckets", ["customer"], ["balance"],
                dateFilter: true, shapes: ["tabular", "aggregation"], evidence: "PostAR"),
            ["ar.unallocated"] = Cap(
                "ar.unallocated", ["customer"], ["balance"],
                asOf: true, shapes: ["listing", "aggregation"], evidence: "PostAR"),
            ["ar.variance.contributors"] = Cap(
                "ar.variance.contributors", ["customer", "gl"], ["variance"],
                explain: true, shapes: ["explainability"], evidence: "PostAR+GL"),
            ["customer.aged.credit.top"] = Cap(
                "customer.aged.credit.top", ["customer"], ["balance"],
                topN: true, asOf: true, shapes: ["ranking"], evidence: "PostAR"),
            ["customer.credit.balances"] = Cap(
                "customer.credit.balances", ["customer"], ["balance"],
                asOf: true, shapes: ["listing", "aggregation"], evidence: "PostAR"),
            ["customer.invoice.unpaid.olderthan"] = Cap(
                "customer.invoice.unpaid.olderthan", ["customer"], ["balance"],
                dateFilter: true, topN: true, shapes: ["tabular"], evidence: "PostAR"),
            ["customer.openitems"] = Cap(
                "customer.openitems", ["customer"], ["balance"],
                asOf: true, shapes: ["tabular"], evidence: "PostAR"),
            ["customer.over.creditlimit"] = Cap(
                "customer.over.creditlimit", ["customer"], ["balance"],
                asOf: true, shapes: ["listing", "tabular"], evidence: "PostAR+Client"),
            ["customer.payment.detail"] = Cap(
                "customer.payment.detail", ["customer"], ["payment_discipline"],
                dateFilter: true, shapes: ["tabular"], evidence: "InvNum+PostAR"),

            // ── AP supplier payment behaviour ─────────────────────────────────
            ["supplier.payment.prompt.top"] = Cap(
                "supplier.payment.prompt.top", ["supplier"], ["payment_discipline"],
                dateFilter: true, topN: true, shapes: ["ranking"], evidence: "InvNum+Vendor"),
            ["supplier.payment.late.top"] = Cap(
                "supplier.payment.late.top", ["supplier"], ["payment_discipline"],
                dateFilter: true, topN: true, shapes: ["ranking"], evidence: "InvNum+Vendor"),
            ["supplier.payment.behavior.summary"] = Cap(
                "supplier.payment.behavior.summary", [], ["payment_discipline"],
                dateFilter: true, shapes: ["aggregation"], evidence: "InvNum+Vendor"),
            ["supplier.payment.detail"] = Cap(
                "supplier.payment.detail", ["supplier"], ["payment_discipline"],
                dateFilter: true, shapes: ["tabular"], evidence: "InvNum+Vendor"),

            // ── AP analytical ─────────────────────────────────────────────────
            ["ap.invoice.overdue.count"] = Cap(
                "ap.invoice.overdue.count", ["supplier"], ["balance"],
                dateFilter: true, shapes: ["aggregation"], evidence: "PostAP"),
            ["ap.unallocated"] = Cap(
                "ap.unallocated", ["supplier"], ["balance"],
                asOf: true, shapes: ["listing", "aggregation"], evidence: "PostAP"),
            ["ap.variance.contributors"] = Cap(
                "ap.variance.contributors", ["supplier", "gl"], ["variance"],
                explain: true, shapes: ["explainability"], evidence: "PostAP+GL"),
            ["supplier.credit.balances"] = Cap(
                "supplier.credit.balances", ["supplier"], ["balance"],
                asOf: true, shapes: ["listing", "aggregation"], evidence: "PostAP"),
            ["supplier.invoice.unpaid.olderthan"] = Cap(
                "supplier.invoice.unpaid.olderthan", ["supplier"], ["balance"],
                dateFilter: true, topN: true, shapes: ["tabular"], evidence: "PostAP"),
            ["supplier.openitems"] = Cap(
                "supplier.openitems", ["supplier"], ["balance"],
                asOf: true, shapes: ["tabular"], evidence: "PostAP"),
            ["supplier.outstanding.top"] = Cap(
                "supplier.outstanding.top", ["supplier"], ["balance"],
                topN: true, asOf: true, shapes: ["ranking"], evidence: "PostAP"),
            ["supplier.payments.top"] = Cap(
                "supplier.payments.top", ["supplier"], ["value"],
                dateFilter: true, topN: true, shapes: ["ranking"], evidence: "PostAP"),

            // ── GL period-close readiness ─────────────────────────────────────
            ["gl.period.close.readiness"] = Cap(
                "gl.period.close.readiness",
                ["gl"], ["readiness"],
                dateFilter: true, explain: true,
                shapes: ["dashboard", "explainability", "checklist"],
                evidence: "PostGL+Bank"),

            // ── GL analytical ─────────────────────────────────────────────────
            ["gl.balance.unusual"] = Cap(
                "gl.balance.unusual", ["gl"], ["variance"],
                explain: true, shapes: ["listing", "explainability"], evidence: "PostGL"),
            ["gl.expense.variance"] = Cap(
                "gl.expense.variance", ["gl"], ["variance"],
                dateFilter: true, explain: true, shapes: ["explainability", "tabular"], evidence: "PostGL"),
            ["gl.journal.duplicate"] = Cap(
                "gl.journal.duplicate", ["gl"], ["value"],
                dateFilter: true, shapes: ["listing", "tabular"], evidence: "PostGL"),
            ["gl.journal.manual"] = Cap(
                "gl.journal.manual", ["gl"], ["value"],
                dateFilter: true, shapes: ["listing", "tabular"], evidence: "PostGL"),
            ["gl.journal.periodend"] = Cap(
                "gl.journal.periodend", ["gl"], ["value"],
                shapes: ["listing", "tabular"], evidence: "PostGL"),
            ["gl.journal.round"] = Cap(
                "gl.journal.round", ["gl"], ["value"],
                dateFilter: true, shapes: ["listing", "tabular"], evidence: "PostGL"),
            ["gl.journal.users.top"] = Cap(
                "gl.journal.users.top", ["gl", "user"], ["value"],
                dateFilter: true, topN: true, shapes: ["ranking"], evidence: "PostGL"),
            ["gl.ratios"] = Cap(
                "gl.ratios", ["gl"], ["ratio"],
                shapes: ["aggregation", "dashboard"], evidence: "PostGL"),
            ["gl.transaction.backdated"] = Cap(
                "gl.transaction.backdated", ["gl"], ["value"],
                dateFilter: true, shapes: ["listing", "tabular"], evidence: "PostGL"),
            ["gl.trialbalance.anomaly"] = Cap(
                "gl.trialbalance.anomaly", ["gl"], ["variance"],
                explain: true, shapes: ["listing", "explainability"], evidence: "PostGL"),

            // ── Bank analytical ───────────────────────────────────────────────
            ["bank.cheques.unpresented"] = Cap(
                "bank.cheques.unpresented", ["bank"], ["value"],
                asOf: true, shapes: ["listing", "tabular"], evidence: "Cashbook"),
            ["bank.deposits.outstanding"] = Cap(
                "bank.deposits.outstanding", ["bank"], ["value"],
                asOf: true, shapes: ["listing", "tabular"], evidence: "Cashbook"),
            ["bank.unmatched"] = Cap(
                "bank.unmatched", ["bank"], ["value"],
                asOf: true, shapes: ["listing", "tabular"], evidence: "Cashbook"),
            ["bank.unusual"] = Cap(
                "bank.unusual", ["bank"], ["value"],
                dateFilter: true, explain: true, shapes: ["listing", "explainability"], evidence: "Cashbook"),

            // ── VAT analytical ────────────────────────────────────────────────
            ["vat.by.account.top"] = Cap(
                "vat.by.account.top", ["gl", "vat"], ["vat"],
                dateFilter: true, topN: true, shapes: ["ranking"], evidence: "PostGL+InvNum"),
            ["vat.input"] = Cap(
                "vat.input", ["vat"], ["vat"],
                dateFilter: true, shapes: ["aggregation", "tabular"], evidence: "InvNum"),
            ["vat.missing"] = Cap(
                "vat.missing", ["vat"], ["vat"],
                dateFilter: true, shapes: ["listing"], evidence: "InvNum"),
            ["vat.output"] = Cap(
                "vat.output", ["vat"], ["vat"],
                dateFilter: true, shapes: ["aggregation", "tabular"], evidence: "InvNum"),
            ["vat.summary"] = Cap(
                "vat.summary", [], ["vat"],
                dateFilter: true, shapes: ["aggregation"], evidence: "InvNum"),
            ["vat.trend"] = Cap(
                "vat.trend", ["vat", "month"], ["vat"],
                dateFilter: true, monthly: true, shapes: ["tabular"], evidence: "InvNum",
                segmented: true),
            ["vat.variance.contributors"] = Cap(
                "vat.variance.contributors", ["vat", "gl"], ["variance"],
                explain: true, shapes: ["explainability", "reconciliation"], evidence: "InvNum+GL"),
            ["vat.zero.rated"] = Cap(
                "vat.zero.rated", ["vat"], ["vat"],
                dateFilter: true, shapes: ["listing", "aggregation"], evidence: "InvNum"),

            // ── Inventory analytical ──────────────────────────────────────────
            ["inventory.adjustment.top"] = Cap(
                "inventory.adjustment.top", ["product"], ["quantity", "value"],
                dateFilter: true, topN: true, shapes: ["ranking"], evidence: "StockTransactions"),
            ["inventory.below.reorder"] = Cap(
                "inventory.below.reorder", ["product"], ["quantity"],
                asOf: true, shapes: ["listing", "tabular"], evidence: "StkItem+valuation"),
            ["inventory.item.drilldown"] = Cap(
                "inventory.item.drilldown", ["product"], ["quantity", "value"],
                dateFilter: true, explain: true, shapes: ["tabular", "explainability"], evidence: "StockTransactions+StkItem"),
            ["inventory.negative.qty"] = Cap(
                "inventory.negative.qty", ["product"], ["quantity"],
                asOf: true, shapes: ["listing", "tabular"], evidence: "StkItem"),
            ["inventory.negative.valuation"] = Cap(
                "inventory.negative.valuation", ["product"], ["value"],
                asOf: true, shapes: ["listing", "tabular"], evidence: "StkItem+valuation"),
            ["inventory.overstocked"] = Cap(
                "inventory.overstocked", ["product"], ["quantity", "value"],
                asOf: true, shapes: ["listing", "tabular"], evidence: "StkItem+valuation"),
            ["inventory.stockgroup.reconcile"] = Cap(
                "inventory.stockgroup.reconcile", ["inventory", "gl"], ["variance"],
                explain: true, shapes: ["reconciliation", "explainability"], evidence: "PostGL+valuation"),
            ["inventory.value.top"] = Cap(
                "inventory.value.top", ["product"], ["value"],
                topN: true, asOf: true, shapes: ["ranking"], evidence: "StkItem+valuation"),
            ["inventory.warehouse.reconcile"] = Cap(
                "inventory.warehouse.reconcile", ["inventory", "warehouse"], ["variance"],
                explain: true, shapes: ["reconciliation", "explainability"], evidence: "StkItem+valuation"),

            // ── Warehouse analytical ──────────────────────────────────────────
            ["warehouse.discrepancy"] = Cap(
                "warehouse.discrepancy", ["warehouse", "product"], ["quantity"],
                explain: true, shapes: ["listing", "explainability"], evidence: "StkItem"),
            ["warehouse.negative.qty"] = Cap(
                "warehouse.negative.qty", ["warehouse", "product"], ["quantity"],
                asOf: true, shapes: ["listing", "tabular"], evidence: "StkItem"),
            ["warehouse.nonmoving"] = Cap(
                "warehouse.nonmoving", ["warehouse", "product"], ["quantity"],
                shapes: ["listing"], evidence: "StockTransactions"),
            ["warehouse.transfer.by.item"] = Cap(
                "warehouse.transfer.by.item", ["product", "warehouse"], ["quantity", "value"],
                dateFilter: true, topN: true, shapes: ["tabular", "ranking"], evidence: "StockTransactions"),
            ["warehouse.transfer.by.warehouse"] = Cap(
                "warehouse.transfer.by.warehouse", ["warehouse"], ["quantity", "value"],
                dateFilter: true, topN: true, shapes: ["tabular", "ranking"], evidence: "StockTransactions"),
            ["warehouse.transfer.detail"] = Cap(
                "warehouse.transfer.detail", ["warehouse", "product"], ["quantity", "value"],
                dateFilter: true, shapes: ["tabular"], evidence: "StockTransactions"),
            ["warehouse.transfer.summary"] = Cap(
                "warehouse.transfer.summary", ["warehouse"], ["quantity", "value"],
                dateFilter: true, monthly: true, shapes: ["aggregation"], evidence: "StockTransactions"),
            ["warehouse.transfer.top"] = Cap(
                "warehouse.transfer.top", ["warehouse", "product"], ["quantity", "value"],
                dateFilter: true, topN: true, shapes: ["ranking"], evidence: "StockTransactions"),
            ["warehouse.value.summary"] = Cap(
                "warehouse.value.summary", ["warehouse"], ["value"],
                asOf: true, shapes: ["aggregation", "tabular"], evidence: "StkItem+valuation"),
            ["warehouse.list"] = Cap(
                "warehouse.list", [], [],
                shapes: ["listing"], evidence: "Warehouse"),

            // ── Fixed assets ──────────────────────────────────────────────────
            ["fa.depreciation.reconcile"] = Cap(
                "fa.depreciation.reconcile", ["fa", "gl"], ["variance"],
                explain: true, shapes: ["reconciliation", "explainability"], evidence: "PostGL+FA"),
            ["fa.variance.contributors"] = Cap(
                "fa.variance.contributors", ["fa", "gl"], ["variance"],
                explain: true, shapes: ["explainability"], evidence: "PostGL+FA"),

            // ── Sales invoices / Purchase invoices analytical ─────────────────
            ["purchaseinvoice.discount.count"] = Cap(
                "purchaseinvoice.discount.count", [], ["discount"],
                dateFilter: true, shapes: ["aggregation"], evidence: "InvNum"),
            ["purchaseinvoice.discount.top"] = Cap(
                "purchaseinvoice.discount.top", ["supplier"], ["discount"],
                dateFilter: true, topN: true, shapes: ["ranking"], evidence: "InvNum"),
            ["purchaseinvoice.duplicate"] = Cap(
                "purchaseinvoice.duplicate", ["supplier"], ["value"],
                dateFilter: true, shapes: ["listing", "tabular"], evidence: "InvNum"),
            ["purchaseinvoice.partially.paid"] = Cap(
                "purchaseinvoice.partially.paid", ["supplier"], ["balance"],
                asOf: true, shapes: ["listing", "tabular"], evidence: "InvNum+PostAP"),
            ["purchaseinvoice.top"] = Cap(
                "purchaseinvoice.top", ["supplier"], ["value"],
                dateFilter: true, topN: true, shapes: ["ranking"], evidence: "InvNum"),
            ["salesinvoice.discount.top"] = Cap(
                "salesinvoice.discount.top", ["customer"], ["discount"],
                dateFilter: true, topN: true, shapes: ["ranking"], evidence: "InvNum"),
            ["salesinvoice.partially.paid"] = Cap(
                "salesinvoice.partially.paid", ["customer"], ["balance"],
                asOf: true, shapes: ["listing", "tabular"], evidence: "InvNum+PostAR"),

            // ── Purchase / Sales period summaries ─────────────────────────────
            ["purchase.item.period.summary"] = Cap(
                "purchase.item.period.summary", ["product", "month"], ["quantity", "value"],
                dateFilter: true, monthly: true, shapes: ["tabular", "monthly_breakdown"], evidence: "InvNum",
                segmented: true),

            // ── Treasury forecasts ────────────────────────────────────────────
            ["treasury.cash.forecast"] = Cap(
                "treasury.cash.forecast", [], ["cashflow"],
                dateFilter: true, monthly: true, shapes: ["tabular"], evidence: "InvNum+Bank",
                segmented: true),
            ["treasury.collections.forecast"] = Cap(
                "treasury.collections.forecast", [], ["cashflow"],
                dateFilter: true, monthly: true, shapes: ["tabular"], evidence: "InvNum+PostAR",
                segmented: true),
            ["treasury.netcashflow.forecast"] = Cap(
                "treasury.netcashflow.forecast", [], ["cashflow"],
                dateFilter: true, monthly: true, shapes: ["tabular"], evidence: "InvNum+Bank",
                segmented: true),
            ["treasury.payments.forecast"] = Cap(
                "treasury.payments.forecast", [], ["cashflow"],
                dateFilter: true, monthly: true, shapes: ["tabular"], evidence: "InvNum+PostAP",
                segmented: true),

            // ── Dashboard / summary ───────────────────────────────────────────
            ["dashboard.summary"] = Cap(
                "dashboard.summary", [], ["cashflow", "balance", "vat"],
                shapes: ["dashboard"], evidence: "InvNum+PostAR+PostAP+Bank"),

            // ── Lookups / reference data (minimal capabilities) ───────────────
            ["customer.get"] = Cap(
                "customer.get", [], [], shapes: ["single"], evidence: "Client"),
            ["customer.list"] = Cap(
                "customer.list", [], [], topN: true, shapes: ["tabular"], evidence: "Client"),
            ["customer.address"] = Cap(
                "customer.address", [], [], shapes: ["single"], evidence: "Client"),
            ["supplier.get"] = Cap(
                "supplier.get", [], [], shapes: ["single"], evidence: "Supplier"),
            ["supplier.list"] = Cap(
                "supplier.list", [], [], topN: true, shapes: ["tabular"], evidence: "Supplier"),
            ["glaccount.get"] = Cap(
                "glaccount.get", [], [], shapes: ["single"], evidence: "GLAccount"),
            ["glaccount.list"] = Cap(
                "glaccount.list", [], [], topN: true, shapes: ["tabular"], evidence: "GLAccount"),
            ["gltransaction.list"] = Cap(
                "gltransaction.list", ["gl"], ["value"], dateFilter: true, topN: true,
                shapes: ["tabular"], evidence: "PostGL"),
            ["inventoryitem.get"] = Cap(
                "inventoryitem.get", [], [], shapes: ["single"], evidence: "StkItem"),
            ["inventoryitem.list"] = Cap(
                "inventoryitem.list", [], [], topN: true, shapes: ["tabular"], evidence: "StkItem"),
            ["inventoryitem.salestax"] = Cap(
                "inventoryitem.salestax", [], [], shapes: ["single"], evidence: "StkItem"),
            ["inventoryitem.sellingprice"] = Cap(
                "inventoryitem.sellingprice", [], [], shapes: ["single"], evidence: "StkItem"),
            ["inventoryitem.stock.qty"] = Cap(
                "inventoryitem.stock.qty", [], [], shapes: ["single"], evidence: "StkItem"),
            ["inventoryitem.units"] = Cap(
                "inventoryitem.units", [], [], shapes: ["single"], evidence: "StkItem"),
            ["customertransaction.get"] = Cap(
                "customertransaction.get", [], [], shapes: ["single"], evidence: "CustomerTransaction"),
            ["customertransaction.list"] = Cap(
                "customertransaction.list", ["customer"], ["value"], dateFilter: true, topN: true,
                shapes: ["tabular"], evidence: "CustomerTransaction"),
            ["suppliertransaction.list"] = Cap(
                "suppliertransaction.list", ["supplier"], ["value"], dateFilter: true, topN: true,
                shapes: ["tabular"], evidence: "SupplierTransaction"),
            ["purchaseorder.list"] = Cap(
                "purchaseorder.list", ["supplier"], ["value"], dateFilter: true, topN: true,
                shapes: ["tabular"], evidence: "PurchaseOrder"),
            ["salesorder.list"] = Cap(
                "salesorder.list", ["customer"], ["value"], dateFilter: true, topN: true,
                shapes: ["tabular"], evidence: "SalesOrder"),
            ["salesorder.nextnumber"] = Cap(
                "salesorder.nextnumber", [], [], shapes: ["single"], evidence: "SalesOrder"),
            ["currency.list"] = Cap(
                "currency.list", [], [], shapes: ["listing"], evidence: "Currency"),
            ["orderstatus.list"] = Cap(
                "orderstatus.list", [], [], shapes: ["listing"], evidence: "OrderStatus"),
            ["priority.list"] = Cap(
                "priority.list", [], [], shapes: ["listing"], evidence: "Priority"),
            ["project.list"] = Cap(
                "project.list", [], [], topN: true, shapes: ["tabular"], evidence: "Project"),
            ["salesrepresentative.list"] = Cap(
                "salesrepresentative.list", [], [], shapes: ["listing"], evidence: "SalesRep"),
            ["settlementterms.list"] = Cap(
                "settlementterms.list", [], [], shapes: ["listing"], evidence: "SettlementTerms"),
            ["taxrate.list"] = Cap(
                "taxrate.list", [], [], shapes: ["listing"], evidence: "TaxRate"),
            ["transactioncode.list"] = Cap(
                "transactioncode.list", [], [], shapes: ["listing"], evidence: "TrCode"),
            ["search.global"] = Cap(
                "search.global", [], [], shapes: ["tabular"], evidence: "multi-table"),
            ["insight.sql.query"] = Cap(
                "insight.sql.query", [], [], shapes: ["tabular"], evidence: "dynamic"),
            ["site.health"] = Cap(
                "site.health", [], [], shapes: ["single"], evidence: "connector"),
            ["site.diagnostics"] = Cap(
                "site.diagnostics", [], [], shapes: ["single"], evidence: "connector"),
            ["site.schema.probe"] = Cap(
                "site.schema.probe", [], [], shapes: ["schema", "tabular"], evidence: "INFORMATION_SCHEMA"),
            ["site.metadata"] = Cap(
                "site.metadata", [], [], shapes: ["metadata", "single"], evidence: "INFORMATION_SCHEMA+connector"),

            // ── Write operations (registered for completeness; CompatibilityGate skips analytical checks) ──
            ["customer.save"] = Cap(
                "customer.save", [], [], shapes: ["single"], evidence: "Client"),
            ["supplier.save"] = Cap(
                "supplier.save", [], [], shapes: ["single"], evidence: "Supplier"),
            ["salesorder.save"] = Cap(
                "salesorder.save", [], [], shapes: ["single"], evidence: "SalesOrder"),
            ["allocation.save"] = Cap(
                "allocation.save", [], [], shapes: ["single"], evidence: "PostAR"),
            ["customertransaction.post"] = Cap(
                "customertransaction.post", [], [], shapes: ["single"], evidence: "CustomerTransaction"),
            ["suppliertransaction.post"] = Cap(
                "suppliertransaction.post", [], [], shapes: ["single"], evidence: "SupplierTransaction"),
            ["gltransaction.post"] = Cap(
                "gltransaction.post", [], [], shapes: ["single"], evidence: "PostGL"),
            // Phase 4 Block 3 — inventory, credit notes, order lifecycle
            ["inventory.adjustment.post"] = Cap(
                "inventory.adjustment.post", [], [], shapes: ["single"], evidence: "StkMovement"),
            ["warehouse.transfer.post"] = Cap(
                "warehouse.transfer.post", [], [], shapes: ["single"], evidence: "WhseStock"),
            ["salescreditnote.post"] = Cap(
                "salescreditnote.post", [], [], shapes: ["single"], evidence: "InvNum"),
            ["suppliercreditnote.post"] = Cap(
                "suppliercreditnote.post", [], [], shapes: ["single"], evidence: "InvNum"),
            ["salesorder.confirm"] = Cap(
                "salesorder.confirm", [], [], shapes: ["single"], evidence: "SalesOrder"),
            ["salesorder.ship"] = Cap(
                "salesorder.ship", [], [], shapes: ["single"], evidence: "SalesOrder"),
            ["purchaseorder.approve"] = Cap(
                "purchaseorder.approve", [], [], shapes: ["single"], evidence: "PurchaseOrder"),
            ["purchaseorder.receive"] = Cap(
                "purchaseorder.receive", [], [], shapes: ["single"], evidence: "PurchaseOrder"),
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

    /// <summary>All registered capabilities — keyed by operation name (case-insensitive).</summary>
    public static IReadOnlyDictionary<string, HandlerCapability> All => Capabilities;

    public static HandlerCapability? Get(string? operation)
    {
        if (string.IsNullOrWhiteSpace(operation))
            return null;
        return Capabilities.TryGetValue(operation, out var cap) ? cap : null;
    }

    public static HandlerCapability GetOrPermissive(string operation) =>
        Get(operation) ?? Cap(operation, [], [], evidence: "unknown");
}
