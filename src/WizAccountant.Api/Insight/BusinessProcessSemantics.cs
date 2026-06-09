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
        new("month-end ready", BusinessProcessType.MonthEndClose, "gl.period.close.readiness", "period-close readiness checklist"),
        new("ready to close", BusinessProcessType.MonthEndClose, "gl.period.close.readiness", "period-close readiness checklist"),
        new("close readiness", BusinessProcessType.MonthEndClose, "gl.period.close.readiness", "period-close readiness checklist"),
        new("can i close", BusinessProcessType.MonthEndClose, "gl.period.close.readiness", "period-close readiness checklist"),
        new("period close check", BusinessProcessType.MonthEndClose, "gl.period.close.readiness", "period-close readiness checklist"),
        new("ar vs gl", BusinessProcessType.Reconciliation, "ar.gl.reconcile", "AR subledger reconciliation"),
        new("supplier payment discipline", BusinessProcessType.PaymentBehavior, "supplier.payment.behavior.summary", "AP payment discipline"),
        new("how well do we pay", BusinessProcessType.PaymentBehavior, "supplier.payment.behavior.summary", "AP payment discipline"),
        new("pay our suppliers", BusinessProcessType.PaymentBehavior, "supplier.payment.behavior.summary", "AP payment discipline"),
        new("prompt supplier", BusinessProcessType.PaymentBehavior, "supplier.payment.prompt.top", "AP payment discipline"),
        new("late supplier", BusinessProcessType.PaymentBehavior, "supplier.payment.late.top", "AP payment discipline"),
        new("overdue supplier", BusinessProcessType.PaymentBehavior, "supplier.payment.late.top", "AP payment discipline"),
        new("collection from customer", BusinessProcessType.CustomerCollections, CustomerCollectionsHelper.SummaryOperation, "customer collections received"),
        new("receipts from customer", BusinessProcessType.CustomerCollections, CustomerCollectionsHelper.SummaryOperation, "customer collections received"),
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
