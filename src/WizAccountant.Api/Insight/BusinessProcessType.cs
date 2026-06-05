namespace WizAccountant.Api.Insight;

/// <summary>High-level business process detected before SQL handler routing (SAGE-CONSOLIDATION-001).</summary>
internal enum BusinessProcessType
{
    Unknown = 0,
    PaymentBehavior,
    Reconciliation,
    Explainability,
    Treasury,
    VatCompliance,
    InventoryLifecycle,
    MonthEndClose,
    DiscountGovernance,
    SupplierRisk,
    BankReconciliation,
    FraudAudit,
    CashflowIntelligence,
    CustomerCollections
}
