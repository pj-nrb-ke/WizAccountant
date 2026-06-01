using System.Text.Json;

namespace WizAccountant.Api.Insight;

/// <summary>Validates handler JSON satisfies QueryIntentContract before UI formatting (SAGE-NEXT-001).</summary>
internal static class OutputContractValidator
{
    internal sealed record ValidationResult(bool IsValid, string? SafeFailureMessage, IReadOnlyList<string> MissingFields);

    public static ValidationResult Validate(
        QueryIntentContract contract,
        string operation,
        string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
            return new ValidationResult(false, BuildFailureMessage(operation, contract),
                ["(empty response)"]);

        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;
            return ValidateRoot(contract, operation, root);
        }
        catch (JsonException)
        {
            return new ValidationResult(false, BuildFailureMessage(operation, contract), ["valid JSON"]);
        }
    }

    private static ValidationResult ValidateRoot(QueryIntentContract contract, string operation, JsonElement root)
    {
        if (string.Equals(operation, ProductOrderAnalysisChatMatcher.Operation, StringComparison.OrdinalIgnoreCase))
            return ValidateProductMonthly(contract, root);

        var cap = HandlerCapabilityRegistry.Get(operation);
        if (cap is null)
            return new ValidationResult(true, null, []);

        var missing = new List<string>();

        if (contract.OutputShape.Contains("explainability") && cap.SupportsExplainability)
        {
            if (!root.TryGetProperty("finding", out _) && !root.TryGetProperty("topContributors", out _))
                missing.Add("finding or topContributors");
        }

        if (contract.WantsAggregation && root.TryGetProperty("countOnly", out var co) && co.ValueKind == JsonValueKind.False)
        {
            if (!HasRows(root))
                missing.Add("aggregation count");
        }

        if (missing.Count > 0)
            return new ValidationResult(false, BuildFailureMessage(operation, contract), missing);

        return new ValidationResult(true, null, []);
    }

    private static ValidationResult ValidateProductMonthly(QueryIntentContract contract, JsonElement root)
    {
        var missing = new List<string>();

        if (!root.TryGetProperty("monthlyBreakdown", out var rows) || rows.ValueKind != JsonValueKind.Array)
            missing.Add("monthlyBreakdown");
        else if (rows.GetArrayLength() == 0)
            missing.Add("monthlyBreakdown rows");
        else
        {
            var first = rows[0];
            if (!first.TryGetProperty("productCode", out _))
                missing.Add("productCode");
            if (!first.TryGetProperty("month", out _))
                missing.Add("month");
            if (!first.TryGetProperty("quantity", out _))
                missing.Add("quantity");
            if (!first.TryGetProperty("value", out _))
                missing.Add("value");
        }

        if (!root.TryGetProperty("topProductByQuantity", out var top) || top.ValueKind != JsonValueKind.Object)
            missing.Add("topProductByQuantity");

        if (missing.Count > 0)
            return new ValidationResult(false, BuildFailureMessage(ProductOrderAnalysisChatMatcher.Operation, contract), missing);

        return new ValidationResult(true, null, []);
    }

    private static bool HasRows(JsonElement root) =>
        root.TryGetProperty("topCustomers", out var tc) && tc.ValueKind == JsonValueKind.Array ||
        root.TryGetProperty("topInvoices", out var ti) && ti.ValueKind == JsonValueKind.Array ||
        root.TryGetProperty("items", out var it) && it.ValueKind == JsonValueKind.Array ||
        root.TryGetProperty("invoiceCount", out _);

    private static string BuildFailureMessage(string operation, QueryIntentContract contract)
    {
        if (contract.Groupings.Contains("product") && contract.Groupings.Contains("month"))
        {
            return "The handler executed, but the response did not satisfy the required monthly product analysis structure " +
                   "(product, month, quantity, and value).";
        }

        if (contract.OutputShape.Contains("explainability"))
        {
            return "The handler executed, but the response did not include the required explainability fields " +
                   "(finding and contributors).";
        }

        return $"The handler executed for {operation}, but the response did not satisfy the required output contract.";
    }
}
