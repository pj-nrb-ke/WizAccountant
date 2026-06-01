using System.Text.Json;

namespace WizAccountant.Api.Insight;

/// <summary>Sanity and execution checks for SAGE-INVVAL-RECON-CANONICAL-001 (DOCS/Sage_AI_Inventory_Fix_Workflow_Patch.md §10).</summary>
internal static class InventoryReconcileValidator
{
    private const decimal GlMagnitudeThreshold = 1_000_000m;
    private const decimal MinValuationToGlRatio = 0.25m;

    public sealed record ValidationResult(
        bool Passed,
        string? FailureReason,
        bool ExecutedSqlValuation,
        bool UsedSdkFallback,
        bool DetailTotalsMatchGrandTotal);

    public static ValidationResult Validate(JsonElement root)
    {
        var executedSql = GetBool(root, "executedSqlValuation", defaultValue: true);
        var sdkFallback = GetBool(root, "usedSdkFallback");
        var valuationLines = GetInt(root, "valuationLineCount");
        var gl = GetDecimal(root, "balanceSheetStockValue", "balanceSheetInventoryGl");
        var val = GetDecimal(root, "inventoryValuation");
        var detailMatch = GetBool(root, "detailTotalsMatchGrandTotal", defaultValue: true);

        if (!executedSql)
            return Fail("Sage SQL valuation was not executed.", executedSql, sdkFallback, detailMatch);

        if (sdkFallback)
            return Fail("SDK valuation fallback is forbidden for this reconciliation.", executedSql, sdkFallback, detailMatch);

        if (valuationLines <= 0 && val == 0 && gl != 0)
            return Fail("Valuation returned no lines while GL inventory is non-zero.", executedSql, sdkFallback, detailMatch);

        if (val == 0 && gl != 0)
            return Fail("Valuation is zero while GL inventory is non-zero.", executedSql, sdkFallback, detailMatch);

        if (gl > GlMagnitudeThreshold && gl != 0)
        {
            var ratio = Math.Abs(val / gl);
            if (ratio < MinValuationToGlRatio)
                return Fail(
                    "Valuation is suspiciously low compared with GL. Treat as incomplete result.",
                    executedSql, sdkFallback, detailMatch);
        }

        if (!detailMatch)
            return Fail("Detail rows do not reconcile to grand total.", executedSql, sdkFallback, detailMatch);

        return new ValidationResult(true, null, executedSql, sdkFallback, detailMatch);
    }

    public static IReadOnlyList<string> BuildExecutionCheckLines(JsonElement root, ValidationResult validation)
    {
        var query = root.TryGetProperty("querySerial", out var q) ? q.GetString() : InventoryGlReconcileFormat.QuerySerial;
        var lines = GetInt(root, "valuationLineCount");
        var valAccounts = GetInt(root, "valuationAccountCount");
        var glAccounts = GetInt(root, "glAccountCount");

        return new List<string>
        {
            "",
            "Execution Check:",
            $"Query: {query}",
            "Source: SQL valuation logic",
            $"SDK fallback used: {(validation.UsedSdkFallback ? "Yes" : "No")}",
            $"Valuation lines: {lines}",
            $"Inventory GL accounts: {glAccounts}",
            $"Valuation accounts (non-zero): {valAccounts}",
            $"Grand total detail check: {(validation.DetailTotalsMatchGrandTotal ? "Pass" : "Fail")}",
            $"Sanity check: {(validation.Passed ? "Pass" : "Fail")}"
        };
    }

    private static ValidationResult Fail(
        string reason, bool executedSql, bool sdkFallback, bool detailMatch) =>
        new(false, reason, executedSql, sdkFallback, detailMatch);

    private static decimal GetDecimal(JsonElement root, string primary, string? alt = null)
    {
        if (root.TryGetProperty(primary, out var el) && el.ValueKind == JsonValueKind.Number)
            return el.GetDecimal();
        if (alt is not null && root.TryGetProperty(alt, out el) && el.ValueKind == JsonValueKind.Number)
            return el.GetDecimal();
        return 0;
    }

    private static int GetInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number
            ? el.GetInt32()
            : 0;

    private static bool GetBool(JsonElement root, string name, bool defaultValue = false) =>
        root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.True
            ? true
            : root.TryGetProperty(name, out el) && el.ValueKind == JsonValueKind.False
                ? false
                : defaultValue;
}
