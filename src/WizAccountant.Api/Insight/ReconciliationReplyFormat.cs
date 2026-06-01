using System.Text.Json;

namespace WizAccountant.Api.Insight;

internal static class ReconciliationReplyFormat
{
    public static bool TryFormat(string operation, JsonElement root, out string reply)
    {
        reply = "";
        if (!operation.Contains("reconcile", StringComparison.OrdinalIgnoreCase) &&
            !operation.Contains("unallocated", StringComparison.OrdinalIgnoreCase) &&
            !operation.Contains("unpresented", StringComparison.OrdinalIgnoreCase) &&
            !operation.Contains("deposits.outstanding", StringComparison.OrdinalIgnoreCase) &&
            !operation.Contains("unmatched", StringComparison.OrdinalIgnoreCase) &&
            !operation.Contains("variance.contributors", StringComparison.OrdinalIgnoreCase) &&
            !operation.Contains("gl.explain", StringComparison.OrdinalIgnoreCase) &&
            !operation.Contains("item.drilldown", StringComparison.OrdinalIgnoreCase))
            return false;

        if (operation == "inventory.gl.reconcile")
            return false;

        var lines = new List<string>();
        if (root.TryGetProperty("querySerial", out var qs))
            lines.Add($"Query run: {qs.GetString()}");

        if (root.TryGetProperty("reconciliationType", out var rt))
            lines.Add($"Reconciliation: {rt.GetString()}");

        if (root.TryGetProperty("finding", out var f))
            lines.Add($"Finding: {f.GetString()}");

        if (root.TryGetProperty("subledgerTotal", out var st) && st.ValueKind == JsonValueKind.Number)
        {
            lines.Add("");
            lines.Add($"Subledger Total:{Environment.NewLine}{st.GetDecimal():N2}");
        }

        if (root.TryGetProperty("glTotal", out var gt) && gt.ValueKind == JsonValueKind.Number)
            lines.Add($"GL Total:{Environment.NewLine}{gt.GetDecimal():N2}");

        if (root.TryGetProperty("difference", out var d) && d.ValueKind == JsonValueKind.Number)
            lines.Add($"Difference:{Environment.NewLine}{d.GetDecimal():N2}");

        if (root.TryGetProperty("topContributors", out var tc) && tc.ValueKind == JsonValueKind.Array && tc.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add("Top Contributors:");
            var n = 0;
            foreach (var c in tc.EnumerateArray())
            {
                n++;
                if (n > 10) break;
                var label = ContributorLabel(c);
                lines.Add($"{n}. {label}");
            }
        }

        if (root.TryGetProperty("dataAsOfUtc", out var da))
            lines.Add($"Data as of: {da.GetString()}");

        reply = lines.Count > 0 ? string.Join(Environment.NewLine, lines) : "Reconciliation complete.";
        return true;
    }

    private static string ContributorLabel(JsonElement c)
    {
        if (c.TryGetProperty("warehouse", out var w)) return $"Warehouse {w.GetString()}";
        if (c.TryGetProperty("customer", out var cu)) return $"Customer {cu.GetString()}";
        if (c.TryGetProperty("supplier", out var su)) return $"Supplier {su.GetString()}";
        if (c.TryGetProperty("invoiceNumber", out var inv)) return $"Invoice {inv.GetString()}";
        if (c.TryGetProperty("assetCode", out var ac)) return $"Asset {ac.GetString()}";
        if (c.TryGetProperty("code", out var code)) return code.GetString() ?? "";
        if (c.TryGetProperty("stockGroup", out var sg)) return $"Stock group {sg.GetString()}";
        return c.ToString();
    }
}
