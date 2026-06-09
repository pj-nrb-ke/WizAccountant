using System.Text.Json;

namespace WizAccountant.Api.Insight;

/// <summary>Standard explainability presentation across domains (SAGE-CONSOLIDATION-001).</summary>
internal static class ExplainabilityEnvelope
{
    public static bool IsExplainabilityOperation(string operation) =>
        operation.Contains("explain", StringComparison.Ordinal) ||
        operation.Contains("variance", StringComparison.Ordinal) ||
        operation.Contains("contributors", StringComparison.Ordinal) ||
        operation.Contains("reconcile", StringComparison.Ordinal) ||
        operation.Contains("anomal", StringComparison.Ordinal) ||
        operation.Contains("unusual", StringComparison.Ordinal) ||
        operation.StartsWith("treasury.", StringComparison.Ordinal);

    public static string EnhanceReply(string operation, string body, JsonElement root)
    {
        if (!IsExplainabilityOperation(operation))
            return body;

        var sections = new List<string> { body };

        if (root.TryGetProperty("finding", out var finding) && finding.ValueKind == JsonValueKind.String)
        {
            var f = finding.GetString();
            if (!string.IsNullOrWhiteSpace(f) && !body.Contains(f, StringComparison.Ordinal))
                sections.Add($"Finding: {f}");
        }

        if (root.TryGetProperty("topContributors", out var contrib) && contrib.ValueKind == JsonValueKind.Array &&
            contrib.GetArrayLength() > 0)
        {
            sections.Add("Top contributors:");
            var n = 0;
            foreach (var c in contrib.EnumerateArray())
            {
                if (n++ >= 5) break;
                sections.Add("  • " + FormatContributor(c));
            }
        }

        if (root.TryGetProperty("likelyCause", out var cause) && cause.ValueKind == JsonValueKind.String)
            sections.Add($"Likely cause: {cause.GetString()}");

        var confidence = ResolveConfidence(root);
        if (!string.IsNullOrEmpty(confidence))
            sections.Add($"Confidence: {confidence}");

        var drill = ResolveDrilldownHint(operation, root);
        if (!string.IsNullOrEmpty(drill))
            sections.Add($"Investigation path: {drill}");

        return string.Join(Environment.NewLine, sections.Distinct());
    }

    private static string? ResolveConfidence(JsonElement root)
    {
        if (root.TryGetProperty("confidence", out var c) && c.ValueKind == JsonValueKind.String)
            return c.GetString();

        if (root.TryGetProperty("reconciled", out var rec) && rec.ValueKind == JsonValueKind.True)
            return "High";
        if (root.TryGetProperty("reconciled", out rec) && rec.ValueKind == JsonValueKind.False)
            return root.TryGetProperty("difference", out var diff) && diff.TryGetDecimal(out var d) && Math.Abs(d) < 1
                ? "Medium"
                : "Low";

        return null;
    }

    private static string? ResolveDrilldownHint(string operation, JsonElement root)
    {
        if (operation.StartsWith("inventory.", StringComparison.Ordinal))
            return "Ask for warehouse details or item drilldown for a stock code.";
        if (operation.StartsWith("vat.", StringComparison.Ordinal))
            return "Ask for VAT by account or missing-VAT invoices.";
        if (operation.StartsWith("bank.", StringComparison.Ordinal))
            return "Ask for unmatched deposits, unpresented cheques, or daily cash movement.";
        if (operation.StartsWith("treasury.", StringComparison.Ordinal))
            return "Ask for top overdue customers (AR aging) to address inflow constraints, or top AP supplier outstanding to manage outflow pressure.";
        if (operation.Contains("ar.", StringComparison.Ordinal) || operation.Contains("ap.", StringComparison.Ordinal))
            return "Ask for variance contributors or unallocated payments.";
        return null;
    }

    private static string FormatContributor(JsonElement c)
    {
        if (c.ValueKind == JsonValueKind.String)
            return c.GetString() ?? "";
        if (c.TryGetProperty("description", out var d))
            return d.GetString() ?? c.ToString();
        if (c.TryGetProperty("account", out var a))
            return a.GetString() ?? c.ToString();
        return c.ToString();
    }
}
