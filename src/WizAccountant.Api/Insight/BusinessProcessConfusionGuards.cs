namespace WizAccountant.Api.Insight;

/// <summary>Blocks semantically wrong handler pairings (SAGE-CONSOLIDATION-001).</summary>
internal static class BusinessProcessConfusionGuards
{
    private static readonly (Func<string, bool> Query, string WrongOperation, string? CanonicalOverride)[] Guards =
    [
        (CustomerCollectionsQuery, "customer.list", null),
        (CustomerCollectionsQuery, "customer.outstanding.debit.top", null),
        (CustomerCollectionsQuery, "customer.aged.top", null),
        (CustomerCollectionsQuery, "customer.unpaid.summary", null),
        (CustomerCollectionsQuery, "customer.openitems", null),
        (PaymentBehaviorQuery, "customer.outstanding.debit.top", "customer.payment.prompt.top"),
        (PaymentBehaviorQuery, "customer.unpaid.summary", "customer.payment.prompt.top"),
        (PaymentBehaviorQuery, "customer.aged.top", "customer.payment.late.top"),
        (PaymentBehaviorQuery, "customer.openitems", "customer.payment.prompt.top"),
        (PaymentBehaviorQuery, "customer.list", "customer.payment.prompt.top"),
        (PromptPayerQuery, "customer.outstanding.debit.top", "customer.payment.prompt.top"),
        (PromptPayerQuery, "customer.unpaid.summary", "customer.payment.prompt.top"),
        (VatAnalysisQuery, "customer.sales.top", "vat.summary"),
        (VatAnalysisQuery, "salesinvoice.discount.top", "vat.summary"),
        (VatPayableQuery, "vat.summary", "vat.payable.estimate"),
        (NegativeStockGlQuery, "inventory.negative.qty", "inventory.bs.negative_ledgers"),
        (NegativeStockGlQuery, "inventoryitem.list", "inventory.bs.negative_ledgers"),
        (CashLowQuery, "bank.cashbook", "treasury.dashboard"),
        (CashLowQuery, "bank.daily.cash", "treasury.dashboard"),
        (CashLowQuery, "customer.list", "treasury.dashboard"),
        (DeadStockQuery, "inventoryitem.list", "inventory.nonmoving"),
        (DeadStockQuery, "inventory.value.top", "inventory.nonmoving"),
        (SlowMovingQuery, "inventoryitem.list", "inventory.slow.moving.top"),
        (BankReconQuery, "bank.cashbook", "bank.reconcile.variance"),
        (BankReconQuery, "bank.unmatched", "bank.reconcile.variance"),
        (MonthEndCloseQuery, "gltransaction.list", "gl.journal.periodend"),
        (MonthEndCloseQuery, "customer.list", "gl.journal.periodend"),
        (GpDeclineQuery, "customer.sales.top", "salesinvoice.discount.top"),
        (ReconVarianceQuery, "customer.openitems", null),
        (ReconVarianceQuery, "supplier.openitems", null),
        (ProductMonthlyOrderQuery, "customer.sales.top", ProductOrderAnalysisChatMatcher.Operation),
        (ProductMonthlyOrderQuery, "customer.unpaid.summary", ProductOrderAnalysisChatMatcher.Operation),
        (ProductMonthlyOrderQuery, "customer.openitems", ProductOrderAnalysisChatMatcher.Operation),
        (ProductMonthlyOrderQuery, "inventory.movement.top", ProductOrderAnalysisChatMatcher.Operation),
        (ProductMonthlyOrderQuery, "inventoryitem.list", ProductOrderAnalysisChatMatcher.Operation),
        (PurchaseProductQuarterlyQuery, "product.monthly.orders.analysis", PurchaseProductQuarterlyChatMatcher.Operation),
        (PurchaseProductQuarterlyQuery, "customer.sales.top", PurchaseProductQuarterlyChatMatcher.Operation),
        (PurchaseProductQuarterlyQuery, "purchaseinvoice.count", PurchaseProductQuarterlyChatMatcher.Operation),
    ];

    public static bool IsBlocked(string message, string operation)
    {
        var m = message.ToLowerInvariant();
        foreach (var (query, wrong, _) in Guards)
        {
            if (string.Equals(operation, wrong, StringComparison.OrdinalIgnoreCase) && query(m))
                return true;
        }

        return false;
    }

    public static bool TryGetCanonicalOverride(string message, string wrongOperation, out string? canonicalOperation)
    {
        canonicalOperation = null;
        var m = message.ToLowerInvariant();
        foreach (var (query, wrong, canonical) in Guards)
        {
            if (!string.Equals(wrongOperation, wrong, StringComparison.OrdinalIgnoreCase) || !query(m))
                continue;
            if (!string.IsNullOrEmpty(canonical))
            {
                canonicalOperation = canonical;
                return true;
            }
        }

        var bp = BusinessProcessClassifier.Classify(message);
        if (!string.IsNullOrEmpty(bp.CanonicalOperation) &&
            !string.Equals(bp.CanonicalOperation, wrongOperation, StringComparison.OrdinalIgnoreCase))
        {
            canonicalOperation = bp.CanonicalOperation;
            return true;
        }

        return false;
    }

    public static bool ShouldBlockOutstandingListing(string messageLower) =>
        CustomerCollectionsHelper.IsCustomerCollectionsQuery(messageLower) ||
        CustomerPaymentBehaviorHelper.IsPaymentBehaviorQuery(messageLower) ||
        PromptPayerQuery(messageLower) ||
        BusinessProcessClassifier.Classify(messageLower).Process == BusinessProcessType.PaymentBehavior;

    private static bool PaymentBehaviorQuery(string m) => CustomerPaymentBehaviorHelper.IsPaymentBehaviorQuery(m);

    private static bool PromptPayerQuery(string m) =>
        CustomerPaymentBehaviorHelper.IsPaymentBehaviorQuery(m) &&
        (m.Contains("prompt") || m.Contains("promptly") || m.Contains("on time") || m.Contains("within terms"));

    private static bool VatAnalysisQuery(string m) =>
        m.Contains("vat") && (m.Contains("why") || m.Contains("high") || m.Contains("increase") || m.Contains("anomal"));

    private static bool VatPayableQuery(string m) =>
        m.Contains("vat") && (m.Contains("payable") || m.Contains("estimate"));

    private static bool NegativeStockGlQuery(string m) =>
        ChatIntentMatcher.IsInventoryBsNegativeLedgersQuery(m);

    private static bool CashLowQuery(string m) =>
        (m.Contains("cash") || m.Contains("liquidity")) &&
        (m.Contains("low") || m.Contains("why") || m.Contains("explain") || m.Contains("shortage"));

    private static bool DeadStockQuery(string m) =>
        m.Contains("dead stock") || (m.Contains("dead") && m.Contains("stock"));

    private static bool SlowMovingQuery(string m) =>
        m.Contains("slow") && (m.Contains("stock") || m.Contains("inventory") || m.Contains("moving"));

    private static bool BankReconQuery(string m) =>
        m.Contains("bank") && (m.Contains("reconcil") || m.Contains("not balanc") || m.Contains("variance"));

    private static bool MonthEndCloseQuery(string m) =>
        m.Contains("month-end") || m.Contains("month end") || m.Contains("ready to close") ||
        m.Contains("close readiness");

    private static bool GpDeclineQuery(string m) =>
        (m.Contains("gp") || m.Contains("gross profit")) &&
        (m.Contains("declin") || m.Contains("drop") || m.Contains("fall") || m.Contains("why"));

    private static bool ReconVarianceQuery(string m) =>
        m.Contains("variance") && (m.Contains("reconcil") || m.Contains("contributor") || m.Contains("explain"));

    private static bool ProductMonthlyOrderQuery(string m) =>
        ProductOrderAnalysisChatMatcher.IsProductMonthlyOrderQuery(m);

    private static bool PurchaseProductQuarterlyQuery(string m) =>
        PurchaseProductQuarterlyChatMatcher.IsPurchaseProductQuarterlyQuery(m);

    private static bool CustomerCollectionsQuery(string m) => CustomerCollectionsHelper.IsCustomerCollectionsQuery(m);
}
