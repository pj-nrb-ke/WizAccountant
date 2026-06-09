using System.Text.Json;

namespace WizAccountant.Api.Insight;

/// <summary>
/// Validates handler JSON satisfies the expected output shape before UI formatting.
/// Each shape is defined as a static method below. Add a new one when a handler is built.
/// </summary>
internal static class OutputContractValidator
{
    internal sealed record ValidationResult(bool IsValid, string? SafeFailureMessage, IReadOnlyList<string> MissingFields);

    // ── Entry point ──────────────────────────────────────────────────────────

    public static ValidationResult Validate(
        QueryIntentContract contract,
        string operation,
        string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
            return Fail(operation, contract, ["(empty response)"]);

        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            return ValidateRoot(contract, operation, doc.RootElement);
        }
        catch (JsonException)
        {
            return Fail(operation, contract, ["valid JSON"]);
        }
    }

    // ── Router ────────────────────────────────────────────────────────────────

    private static ValidationResult ValidateRoot(QueryIntentContract contract, string operation, JsonElement root)
    {
        return operation.ToLowerInvariant() switch
        {
            // ── Product monthly ──────────────────────────────────────────────
            ProductOrderAnalysisChatMatcher.Operation => ValidateProductMonthly(contract, root),

            // ── Supplier unpaid count ────────────────────────────────────────
            ChatIntentMatcher.SupplierUnpaidCountOp => ValidateShape(root,
                "totalUnpaidSuppliers", "totalOutstandingPayable", "asOfDate"),

            // ── AR/AP payment behaviour ──────────────────────────────────────
            "customer.payment.behavior.summary" => ValidateShape(root,
                "finding", "promptPayers", "slowPayers", "averageCollectionDays", "customersAnalyzed"),
            "customer.payment.late.top" => ValidateShape(root,
                "finding", "customers", "requestedTop"),
            "customer.payment.prompt.top" => ValidateShape(root,
                "finding", "customers", "requestedTop"),
            "customer.payment.detail" => ValidateShape(root,
                "finding", "customer"),

            // ── VAT ──────────────────────────────────────────────────────────
            "vat.reconcile" => ValidateShape(root,
                "variance", "reconciled", "finding", "invNumNetVat", "glVatControlMovement"),
            "vat.anomalies" => ValidateShape(root,
                "invoices"),
            "vat.summary" => ValidateShape(root,
                "outputVat", "inputVat", "estimatedVatPayable", "finding"),
            "vat.variance.contributors" => ValidateReconcileEnvelope(root),

            // ── GL / AR / AP reconciliation ──────────────────────────────────
            "ar.gl.reconcile" => ValidateReconcileEnvelope(root),
            "ap.gl.reconcile" => ValidateReconcileEnvelope(root),
            "inventory.gl.reconcile" => ValidateShape(root,
                "balanceSheetStockValue", "inventoryValuation", "difference", "matches", "finding"),
            "inventory.warehouse.reconcile" => ValidateReconcileEnvelope(root),

            // ── Collections ──────────────────────────────────────────────────
            "customer.collections.summary" => ValidateShape(root,
                "totalCollections", "monthlyBreakdown"),
            "customer.collections.by.customer" => ValidateShape(root,
                "customers"),
            "customer.collections.top" => ValidateShape(root,
                "customers"),
            "customer.collections.by.month" => ValidateShape(root,
                "monthlyBreakdown"),

            // ── Aged debtors / creditors ─────────────────────────────────────
            "customer.aged.top" => ValidateShape(root,
                "topCustomers"),
            "customer.aged.credit.top" => ValidateShape(root,
                "topCustomers"),
            "supplier.aged.top" => ValidateShape(root,
                "topSuppliers"),

            // ── Treasury ────────────────────────────────────────────────────
            "treasury.dashboard" => ValidateShape(root,
                "cashPosition", "expectedInflows", "expectedOutflows", "projectedClosingCash", "finding"),
            "treasury.cash.forecast" => ValidateShape(root,
                "cashForecast"),
            "treasury.collections.forecast" => ValidateShape(root,
                "collectionsForecast"),
            "treasury.payments.forecast" => ValidateShape(root,
                "paymentsForecast"),

            // ── Default: use capability registry ────────────────────────────
            _ => ValidateFromCapabilityRegistry(contract, operation, root)
        };
    }

    // ── Shape validators ──────────────────────────────────────────────────────

    /// <summary>Validates that all named top-level fields are present.</summary>
    private static ValidationResult ValidateShape(JsonElement root, params string[] requiredFields)
    {
        var missing = requiredFields
            .Where(f => !root.TryGetProperty(f, out _))
            .ToList();
        return missing.Count == 0
            ? Ok()
            : Fail(null, null, missing);
    }

    /// <summary>Validates the standard ReconcileEnvelope shape (difference, reconciled, finding).</summary>
    private static ValidationResult ValidateReconcileEnvelope(JsonElement root)
        => ValidateShape(root, "difference", "reconciled", "finding");

    /// <summary>Product monthly analysis — strict shape including monthlyBreakdown rows.</summary>
    private static ValidationResult ValidateProductMonthly(QueryIntentContract contract, JsonElement root)
    {
        var missing = new List<string>();

        if (contract.Period is not null)
        {
            if (!root.TryGetProperty("dateFrom", out _)) missing.Add("dateFrom echo");
            if (!root.TryGetProperty("dateTo", out _)) missing.Add("dateTo echo");
            if (!root.TryGetProperty("periodType", out _)) missing.Add("periodType");
        }

        if (!root.TryGetProperty("monthlyBreakdown", out var breakdown) || breakdown.ValueKind != JsonValueKind.Array)
            missing.Add("monthlyBreakdown");
        else if (breakdown.GetArrayLength() == 0)
            missing.Add("monthlyBreakdown rows");
        else
        {
            var first = breakdown[0];
            foreach (var field in new[] { "productCode", "month", "quantity", "value" })
                if (!first.TryGetProperty(field, out _)) missing.Add(field);
        }

        if (!root.TryGetProperty("topProductByQuantity", out _))
            missing.Add("topProductByQuantity");

        return missing.Count == 0
            ? Ok()
            : Fail(ProductOrderAnalysisChatMatcher.Operation, contract, missing);
    }

    /// <summary>Capability-registry fallback for handlers not explicitly listed above.</summary>
    private static ValidationResult ValidateFromCapabilityRegistry(
        QueryIntentContract contract, string operation, JsonElement root)
    {
        var cap = HandlerCapabilityRegistry.Get(operation);
        if (cap is null)
            return Ok(); // unknown operation — be permissive

        var missing = new List<string>();

        if (contract.OutputShape.Contains("explainability") && cap.SupportsExplainability)
        {
            if (!root.TryGetProperty("finding", out _) && !root.TryGetProperty("topContributors", out _))
                missing.Add("finding or topContributors");
        }

        if (contract.WantsAggregation && root.TryGetProperty("countOnly", out var co) &&
            co.ValueKind == JsonValueKind.False && !HasAnyRows(root))
        {
            missing.Add("aggregation count");
        }

        return missing.Count == 0 ? Ok() : Fail(operation, contract, missing);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ValidationResult Ok() => new(true, null, []);

    private static ValidationResult Fail(string? operation, QueryIntentContract? contract, List<string> missing)
    {
        var msg = BuildMessage(operation, contract, missing);
        return new ValidationResult(false, msg, missing);
    }

    private static string BuildMessage(string? operation, QueryIntentContract? contract, List<string> missing)
    {
        if (contract?.Groupings.Contains("product") == true && contract.Groupings.Contains("month"))
            return "The handler executed, but the response did not satisfy the required monthly product analysis structure (product, month, quantity, value).";

        if (contract?.OutputShape.Contains("explainability") == true)
            return "The handler executed, but the response did not include the required explainability fields (finding and contributors).";

        if (missing.Count > 0)
            return $"The handler executed but the response was missing required fields: {string.Join(", ", missing)}.";

        return $"The handler executed for {operation ?? "unknown"}, but the response did not satisfy the required output contract.";
    }

    private static bool HasAnyRows(JsonElement root) =>
        (root.TryGetProperty("topCustomers", out var tc) && tc.ValueKind == JsonValueKind.Array) ||
        (root.TryGetProperty("topInvoices", out var ti) && ti.ValueKind == JsonValueKind.Array) ||
        (root.TryGetProperty("items", out var it) && it.ValueKind == JsonValueKind.Array) ||
        root.TryGetProperty("invoiceCount", out _);
}
