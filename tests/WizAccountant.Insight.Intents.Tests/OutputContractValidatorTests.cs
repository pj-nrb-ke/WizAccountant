using WizAccountant.Api.Insight;

namespace WizAccountant.Insight.Intents.Tests;

/// <summary>
/// Tests for OutputContractValidator extended shapes (GAP-012).
/// Each domain group verifies: valid passes, missing required field fails, empty JSON fails.
/// </summary>
public sealed class OutputContractValidatorTests
{
    private static QueryIntentContract DefaultContract() =>
        QueryIntentContract.Parse("test query", SageIntentEngine.Classify("show me"), null);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void AssertValid(string operation, string json) =>
        Assert.True(OutputContractValidator.Validate(DefaultContract(), operation, json).IsValid,
            $"{operation}: expected valid");

    private static void AssertInvalid(string operation, string json, string expectedMissingSubstring) =>
        Assert.Contains(
            expectedMissingSubstring,
            OutputContractValidator.Validate(DefaultContract(), operation, json).MissingFields,
            StringComparer.OrdinalIgnoreCase);

    private static void AssertFailsEmpty(string operation) =>
        Assert.False(OutputContractValidator.Validate(DefaultContract(), operation, null).IsValid);

    // ── Payment behaviour ─────────────────────────────────────────────────────

    [Fact]
    public void PaymentBehaviorSummary_valid_passes()
        => AssertValid("customer.payment.behavior.summary", """
            { "finding": "OK", "promptPayers": [], "slowPayers": [], "averageCollectionDays": 30, "customersAnalyzed": 5 }
            """);

    [Fact]
    public void PaymentBehaviorSummary_missing_finding_fails()
        => AssertInvalid("customer.payment.behavior.summary",
            """{ "promptPayers": [], "slowPayers": [], "averageCollectionDays": 30, "customersAnalyzed": 5 }""",
            "finding");

    [Fact]
    public void PaymentBehaviorSummary_missing_promptPayers_fails()
        => AssertInvalid("customer.payment.behavior.summary",
            """{ "finding": "OK", "slowPayers": [], "averageCollectionDays": 30, "customersAnalyzed": 5 }""",
            "promptPayers");

    [Fact]
    public void PaymentLateTop_valid_passes()
        => AssertValid("customer.payment.late.top", """
            { "finding": "late", "customers": [], "requestedTop": 10 }
            """);

    [Fact]
    public void PaymentLateTop_missing_customers_fails()
        => AssertInvalid("customer.payment.late.top",
            """{ "finding": "late", "requestedTop": 10 }""",
            "customers");

    [Fact]
    public void PaymentPromptTop_valid_passes()
        => AssertValid("customer.payment.prompt.top", """
            { "finding": "prompt", "customers": [], "requestedTop": 5 }
            """);

    [Fact]
    public void PaymentDetail_valid_passes()
        => AssertValid("customer.payment.detail", """
            { "finding": "detail", "customer": "CUST001" }
            """);

    [Fact]
    public void PaymentDetail_missing_customer_fails()
        => AssertInvalid("customer.payment.detail",
            """{ "finding": "detail" }""",
            "customer");

    // ── VAT ───────────────────────────────────────────────────────────────────

    [Fact]
    public void VatReconcile_valid_passes()
        => AssertValid("vat.reconcile", """
            { "variance": 0, "reconciled": true, "finding": "OK",
              "invNumNetVat": 1000, "glVatControlMovement": 1000 }
            """);

    [Fact]
    public void VatReconcile_missing_variance_fails()
        => AssertInvalid("vat.reconcile",
            """{ "reconciled": true, "finding": "OK", "invNumNetVat": 1000, "glVatControlMovement": 1000 }""",
            "variance");

    [Fact]
    public void VatAnomalies_valid_passes()
        => AssertValid("vat.anomalies", """{ "invoices": [] }""");

    [Fact]
    public void VatAnomalies_missing_invoices_fails()
        => AssertInvalid("vat.anomalies", """{ "note": "none found" }""", "invoices");

    [Fact]
    public void VatSummary_valid_passes()
        => AssertValid("vat.summary", """
            { "outputVat": 500, "inputVat": 200, "estimatedVatPayable": 300, "finding": "payable" }
            """);

    [Fact]
    public void VatSummary_missing_estimatedVatPayable_fails()
        => AssertInvalid("vat.summary",
            """{ "outputVat": 500, "inputVat": 200, "finding": "payable" }""",
            "estimatedVatPayable");

    // ── Reconciliation ────────────────────────────────────────────────────────

    [Fact]
    public void ArGlReconcile_valid_passes()
        => AssertValid("ar.gl.reconcile", """
            { "difference": 0.00, "reconciled": true, "finding": "balanced" }
            """);

    [Fact]
    public void ArGlReconcile_missing_difference_fails()
        => AssertInvalid("ar.gl.reconcile",
            """{ "reconciled": true, "finding": "balanced" }""",
            "difference");

    [Fact]
    public void ApGlReconcile_valid_passes()
        => AssertValid("ap.gl.reconcile", """
            { "difference": 100, "reconciled": false, "finding": "variance" }
            """);

    [Fact]
    public void InventoryGlReconcile_valid_passes()
        => AssertValid("inventory.gl.reconcile", """
            { "balanceSheetStockValue": 1000, "inventoryValuation": 1000,
              "difference": 0, "matches": true, "finding": "balanced" }
            """);

    [Fact]
    public void InventoryGlReconcile_missing_inventoryValuation_fails()
        => AssertInvalid("inventory.gl.reconcile",
            """{ "balanceSheetStockValue": 1000, "difference": 0, "matches": true, "finding": "balanced" }""",
            "inventoryValuation");

    // ── Collections ───────────────────────────────────────────────────────────

    [Fact]
    public void CollectionsSummary_valid_passes()
        => AssertValid("customer.collections.summary", """
            { "totalCollections": 50000, "monthlyBreakdown": [] }
            """);

    [Fact]
    public void CollectionsByCustomer_valid_passes()
        => AssertValid("customer.collections.by.customer", """{ "customers": [] }""");

    [Fact]
    public void CollectionsTop_valid_passes()
        => AssertValid("customer.collections.top", """{ "customers": [] }""");

    // ── Aged debtors / creditors ──────────────────────────────────────────────

    [Fact]
    public void AgedTop_valid_passes()
        => AssertValid("customer.aged.top", """{ "topCustomers": [] }""");

    [Fact]
    public void AgedTop_missing_topCustomers_fails()
        => AssertInvalid("customer.aged.top", """{ "note": "none" }""", "topCustomers");

    [Fact]
    public void SupplierAgedTop_valid_passes()
        => AssertValid("supplier.aged.top", """{ "topSuppliers": [] }""");

    // ── Treasury ──────────────────────────────────────────────────────────────

    [Fact]
    public void TreasuryDashboard_valid_passes()
        => AssertValid("treasury.dashboard", """
            { "cashPosition": 10000, "expectedInflows": 5000, "expectedOutflows": 3000,
              "projectedClosingCash": 12000, "finding": "healthy" }
            """);

    [Fact]
    public void TreasuryDashboard_missing_projectedClosingCash_fails()
        => AssertInvalid("treasury.dashboard",
            """{ "cashPosition": 10000, "expectedInflows": 5000, "expectedOutflows": 3000, "finding": "healthy" }""",
            "projectedClosingCash");

    [Fact]
    public void TreasuryCashForecast_valid_passes()
        => AssertValid("treasury.cash.forecast", """{ "cashForecast": [] }""");

    [Fact]
    public void TreasuryCollectionsForecast_valid_passes()
        => AssertValid("treasury.collections.forecast", """{ "collectionsForecast": [] }""");

    [Fact]
    public void TreasuryPaymentsForecast_valid_passes()
        => AssertValid("treasury.payments.forecast", """{ "paymentsForecast": [] }""");

    // ── Cross-cutting ─────────────────────────────────────────────────────────

    [Fact]
    public void Null_result_always_fails()
    {
        foreach (var op in new[]
        {
            "customer.payment.behavior.summary", "vat.reconcile",
            "ar.gl.reconcile", "treasury.dashboard", "customer.aged.top"
        })
            AssertFailsEmpty(op);
    }

    [Fact]
    public void Unknown_operation_with_empty_json_is_permissive()
    {
        // Unknown operations should not block — be permissive until explicitly contracted.
        var result = OutputContractValidator.Validate(DefaultContract(), "some.new.unregistered.op", "{}");
        Assert.True(result.IsValid);
    }
}
