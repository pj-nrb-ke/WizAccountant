using System.Text.Json;

namespace WizAccountant.Api.Insight;

internal static class InventoryBsNegativeLedgersFormat
{
    public const string QuerySerial = "SAGE-BS-STOCK-NEGATIVE-001";

    public static string BuildReply(JsonElement root)
    {
        var hasNegative = root.TryGetProperty("hasNegativeLedgers", out var h) &&
                          h.ValueKind == JsonValueKind.True;
        var total = GetDecimal(root, "totalNegativeStockValue");
        var asOf = root.TryGetProperty("asOfDate", out var d) ? d.GetString() : "";

        var lines = new List<string>
        {
            "Finding:",
            hasNegative
                ? "Yes, there are inventory Balance Sheet ledgers with credit balance."
                : "No inventory Balance Sheet stock ledgers currently have a credit/negative balance.",
            "",
            "Total Negative Stock Value:",
            total.ToString("N2")
        };

        if (!string.IsNullOrEmpty(asOf))
        {
            lines.Insert(1, $"As at: {asOf}");
            lines.Insert(2, "");
        }

        if (hasNegative && root.TryGetProperty("largestNegative", out var largest) &&
            largest.ValueKind == JsonValueKind.Object)
        {
            var acct = largest.TryGetProperty("glAccount", out var ga) ? ga.GetString() : "";
            var name = largest.TryGetProperty("glAccountName", out var gn) ? gn.GetString() : "";
            lines.Add("");
            lines.Add("Next Step:");
            lines.Add($"Show GL transactions for the largest negative stock ledger ({acct} — {name}).");
        }
        else if (!hasNegative)
        {
            lines.Add("");
            lines.Add("Next Step:");
            lines.Add("No drilldown required — all inventory stock GL accounts have zero or debit balances.");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static decimal GetDecimal(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number)
            return el.GetDecimal();
        return 0;
    }
}
