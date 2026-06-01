namespace WizAccountant.Api.Insight;

/// <summary>Canonical business phrase → preferred handler operation (semantic dictionary).</summary>
internal static class BusinessProcessSemantics
{
    internal sealed record CanonicalRoute(
        string PhrasePattern,
        BusinessProcessType Process,
        string CanonicalOperation,
        string BusinessMeaning);

    internal static readonly CanonicalRoute[] Routes =
    [
        new("pay promptly", BusinessProcessType.PaymentBehavior, "customer.payment.prompt.top", "customer payment discipline"),
        new("prompt payer", BusinessProcessType.PaymentBehavior, "customer.payment.prompt.top", "customer payment discipline"),
        new("slow payer", BusinessProcessType.PaymentBehavior, "customer.payment.late.top", "customer payment discipline"),
        new("pay late", BusinessProcessType.PaymentBehavior, "customer.payment.late.top", "customer payment discipline"),
        new("dead stock", BusinessProcessType.InventoryLifecycle, "inventory.nonmoving", "non-moving inventory"),
        new("slow moving", BusinessProcessType.InventoryLifecycle, "inventory.slow.moving.top", "inventory lifecycle analysis"),
        new("vat payable", BusinessProcessType.VatCompliance, "vat.payable.estimate", "VAT control calculation"),
        new("estimate vat", BusinessProcessType.VatCompliance, "vat.payable.estimate", "VAT control calculation"),
        new("cash low", BusinessProcessType.CashflowIntelligence, "treasury.dashboard", "treasury explainability"),
        new("why is cash", BusinessProcessType.CashflowIntelligence, "treasury.dashboard", "treasury explainability"),
        new("bank reconcil", BusinessProcessType.BankReconciliation, "bank.reconcile.variance", "bank reconciliation variance"),
        new("not balancing", BusinessProcessType.BankReconciliation, "bank.reconcile.variance", "bank reconciliation variance"),
        new("vat reconcil", BusinessProcessType.VatCompliance, "vat.reconcile", "VAT reconciliation"),
        new("inventory high", BusinessProcessType.Explainability, "inventory.gl.explain", "inventory valuation explainability"),
        new("gp declin", BusinessProcessType.Explainability, "salesinvoice.discount.top", "profitability explainability"),
        new("month-end ready", BusinessProcessType.MonthEndClose, "gl.journal.periodend", "finance close checklist"),
        new("ready to close", BusinessProcessType.MonthEndClose, "gl.journal.periodend", "finance close checklist"),
        new("ar vs gl", BusinessProcessType.Reconciliation, "ar.gl.reconcile", "AR subledger reconciliation"),
        new("ap vs gl", BusinessProcessType.Reconciliation, "ap.gl.reconcile", "AP subledger reconciliation"),
        new("inventory vs gl", BusinessProcessType.Reconciliation, "inventory.gl.reconcile", "inventory GL reconciliation"),
    ];

    public static CanonicalRoute? Match(string messageLower)
    {
        foreach (var route in Routes)
        {
            if (messageLower.Contains(route.PhrasePattern, StringComparison.Ordinal))
                return route;
        }

        return null;
    }
}
