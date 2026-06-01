using System.Text.Json;

namespace WizAccountant.Api.Insight;

internal static class InventoryGlReconcileFormat
{
    public const string QuerySerial = "SAGE-INVVAL-RECON-CANONICAL-001";

    public static string BuildReply(JsonElement root, string? userMessage = null)
    {
        var validation = InventoryReconcileValidator.Validate(root);
        var wantsFix = InventoryFixWorkflow.WantsFixWorkflow(userMessage);
        var asOf = root.TryGetProperty("asOfDate", out var d) ? d.GetString() : "";

        var lines = new List<string>();

        if (wantsFix)
            lines.AddRange(InventoryFixWorkflow.BuildOpeningLines());

        if (!string.IsNullOrEmpty(asOf))
        {
            lines.Add("");
            lines.Add($"As at: {asOf}");
        }

        lines.AddRange(InventoryReconcileValidator.BuildExecutionCheckLines(root, validation));

        if (!validation.Passed)
        {
            lines.AddRange(InventoryFixWorkflow.BuildSanityFailureLines(root, validation));
            return string.Join(Environment.NewLine, lines);
        }

        var gl = GetDecimal(root, "balanceSheetStockValue", "balanceSheetInventoryGl");
        var val = GetDecimal(root, "inventoryValuation");
        var diff = GetDecimal(root, "difference");
        var matches = root.TryGetProperty("matches", out var m) && m.ValueKind == JsonValueKind.True;

        if (lines.Count > 0 && !lines[^1].Equals(""))
            lines.Add("");

        lines.Add("Finding:");
        lines.Add(matches
            ? "Inventory valuation is matching Balance Sheet stock value."
            : "Inventory valuation does not match Balance Sheet stock value.");

        lines.Add("");
        lines.Add($"Balance Sheet Stock Value:{Environment.NewLine}{gl:N2}");
        lines.Add("");
        lines.Add($"Inventory Valuation:{Environment.NewLine}{val:N2}");
        lines.Add("");
        lines.Add($"Difference:{Environment.NewLine}{diff:N2}");
        lines.Add("");
        lines.Add($"Match:{Environment.NewLine}{(matches ? "Yes" : "No")}");

        if (root.TryGetProperty("mainVariance", out var mv) && mv.ValueKind == JsonValueKind.Object)
        {
            var acct = mv.TryGetProperty("glAccount", out var ga) ? ga.GetString() : "";
            var name = mv.TryGetProperty("glAccountName", out var gn) ? gn.GetString() : "";
            var varDiff = GetDecimal(mv, "difference");
            lines.Add("");
            lines.Add("Main Variance:");
            lines.Add($"{acct} — {name}");
            lines.Add($"Difference on this account: {varDiff:N2}");
        }

        if (!matches && wantsFix)
            lines.AddRange(InventoryFixWorkflow.BuildValidMismatchFixPlan(root));

        return string.Join(Environment.NewLine, lines);
    }

    private static decimal GetDecimal(JsonElement parent, string name, string? altName = null, decimal defaultValue = 0)
    {
        if (parent.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number)
            return el.GetDecimal();
        if (altName is not null && parent.TryGetProperty(altName, out el) && el.ValueKind == JsonValueKind.Number)
            return el.GetDecimal();
        return defaultValue;
    }
}
