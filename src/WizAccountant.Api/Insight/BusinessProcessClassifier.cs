namespace WizAccountant.Api.Insight;

/// <summary>First major interpretation layer — business process before SQL handlers (SAGE-CONSOLIDATION-001).</summary>
internal static class BusinessProcessClassifier
{
    internal sealed record Classification(
        BusinessProcessType Process,
        string? CanonicalOperation,
        string? BusinessMeaning,
        double Confidence);

    public static Classification Classify(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return new Classification(BusinessProcessType.Unknown, null, null, 0);

        var m = message.ToLowerInvariant();

        var semantic = BusinessProcessSemantics.Match(m);
        if (semantic is not null)
            return new Classification(semantic.Process, semantic.CanonicalOperation, semantic.BusinessMeaning, 0.92);

        if (ChatIntentMatcher.IsInventoryBsNegativeLedgersQuery(m))
            return new Classification(
                BusinessProcessType.InventoryLifecycle,
                "inventory.bs.negative_ledgers",
                "negative stock GL on balance sheet",
                0.9);

        if ((m.Contains("ar") || m.Contains("receivable")) && m.Contains("match") && m.Contains("gl"))
            return new Classification(BusinessProcessType.Reconciliation, "ar.gl.reconcile", "AR subledger reconciliation", 0.85);

        if ((m.Contains("inventory") || m.Contains("stock")) && m.Contains("gl") &&
            (m.Contains("reconcil") || m.Contains("valuation") || m.Contains("match")))
            return new Classification(BusinessProcessType.Reconciliation, "inventory.gl.reconcile", "inventory GL reconciliation", 0.85);

        if (CustomerPaymentBehaviorHelper.IsPaymentBehaviorQuery(m))
        {
            var op = CustomerPaymentBehaviorHelper.IsLatePayerQuery(m)
                ? "customer.payment.late.top"
                : CustomerPaymentBehaviorHelper.IsPaymentSummaryQuery(m)
                    ? "customer.payment.behavior.summary"
                    : "customer.payment.prompt.top";
            return new Classification(BusinessProcessType.PaymentBehavior, op, "customer payment discipline", 0.88);
        }

        if (IsReconciliationQuery(m))
            return new Classification(BusinessProcessType.Reconciliation, null, "reconciliation workflow", 0.75);

        if (IsExplainabilityQuery(m))
            return new Classification(BusinessProcessType.Explainability, null, "causal explainability", 0.72);

        if (m.Contains("vat") || (m.Contains("tax") && !m.Contains("income tax")))
            return new Classification(BusinessProcessType.VatCompliance, null, "VAT compliance", 0.7);

        if (m.Contains("treasury") || m.Contains("cashflow") || m.Contains("cash flow") ||
            (m.Contains("cash") && (m.Contains("forecast") || m.Contains("low") || m.Contains("why"))))
            return new Classification(BusinessProcessType.CashflowIntelligence, null, "treasury / cashflow", 0.68);

        if (m.Contains("bank") && (m.Contains("reconcil") || m.Contains("unmatched") || m.Contains("deposit") || m.Contains("cheque")))
            return new Classification(BusinessProcessType.BankReconciliation, null, "bank reconciliation", 0.7);

        if ((m.Contains("stock") || m.Contains("inventory") || m.Contains("warehouse")) &&
            (m.Contains("slow") || m.Contains("dead") || m.Contains("non moving") || m.Contains("non-moving") ||
             m.Contains("overstock") || m.Contains("replenish")))
            return new Classification(BusinessProcessType.InventoryLifecycle, null, "inventory lifecycle", 0.7);

        if (m.Contains("discount") && (m.Contains("invoice") || m.Contains("sales") || m.Contains("gp")))
            return new Classification(BusinessProcessType.DiscountGovernance, null, "discount governance", 0.65);

        if (m.Contains("month-end") || m.Contains("month end") || m.Contains("period close") || m.Contains("ready to close"))
            return new Classification(BusinessProcessType.MonthEndClose, null, "month-end close", 0.65);

        if (m.Contains("supplier") && (m.Contains("critical") || m.Contains("dependency") || m.Contains("risk")))
            return new Classification(BusinessProcessType.SupplierRisk, null, "supplier dependency risk", 0.6);

        if (m.Contains("audit") || m.Contains("fraud") || m.Contains("suspicious journal"))
            return new Classification(BusinessProcessType.FraudAudit, null, "audit / fraud review", 0.6);

        return new Classification(BusinessProcessType.Unknown, null, null, 0);
    }

    private static bool IsReconciliationQuery(string m) =>
        m.Contains("reconcil") || m.Contains("not matching") || m.Contains("doesn't match") ||
        m.Contains("does not match") || m.Contains("mismatch") ||
        (m.Contains("variance") && (m.Contains("gl") || m.Contains("subledger") || m.Contains("control")));

    private static bool IsExplainabilityQuery(string m) =>
        (m.Contains("why") || m.Contains("explain") || m.Contains("root cause") || m.Contains("what caused")) &&
        !CustomerPaymentBehaviorHelper.IsPaymentBehaviorQuery(m);
}
