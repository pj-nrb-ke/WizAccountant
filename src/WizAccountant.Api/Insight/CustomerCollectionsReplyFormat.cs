using System.Text.Json;

namespace WizAccountant.Api.Insight;

internal static class CustomerCollectionsReplyFormat
{
    public static (string reply, string serial) Build(JsonElement root)
    {
        var serial = root.TryGetProperty("querySerial", out var qs)
            ? qs.GetString() ?? CustomerCollectionsEngineSerial
            : CustomerCollectionsEngineSerial;

        var from = root.TryGetProperty("dateFrom", out var df) ? df.GetString() : null;
        var to = root.TryGetProperty("dateTo", out var dt) ? dt.GetString() : null;
        var total = root.TryGetProperty("totalCollections", out var t) && t.ValueKind == JsonValueKind.Number
            ? t.GetDecimal().ToString("N2")
            : "0.00";

        var lines = new List<string>();

        if (root.TryGetProperty("segmentTotals", out var segTotals) && segTotals.ValueKind == JsonValueKind.Array &&
            segTotals.GetArrayLength() > 1)
        {
            foreach (var s in segTotals.EnumerateArray())
            {
                var label = s.TryGetProperty("label", out var lb) ? lb.GetString() : "";
                var amt = s.TryGetProperty("collectionAmount", out var sa) && sa.ValueKind == JsonValueKind.Number
                    ? sa.GetDecimal().ToString("N2")
                    : "0.00";
                if (!string.IsNullOrEmpty(label))
                    lines.Add($"{label}: {amt}");
            }

            lines.Add($"Total customer collections: {total}");
        }
        else
        {
            lines.Add($"Total customer collections: {total}");
        }

        if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to))
            lines.Add($"Period envelope: {from} to {to}");

        if (root.TryGetProperty("monthlyBreakdown", out var months) && months.ValueKind == JsonValueKind.Array)
        {
            var hasSegments = months.EnumerateArray().Any(m =>
                m.TryGetProperty("segmentLabel", out var sl) && sl.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(sl.GetString()));

            foreach (var m in months.EnumerateArray())
            {
                var name = m.TryGetProperty("month", out var mn) ? mn.GetString() : "";
                var year = m.TryGetProperty("year", out var y) && y.ValueKind == JsonValueKind.Number ? y.GetInt32() : 0;
                var amt = m.TryGetProperty("collectionAmount", out var a) && a.ValueKind == JsonValueKind.Number
                    ? a.GetDecimal().ToString("N2")
                    : "0.00";
                var seg = m.TryGetProperty("segmentLabel", out var sl) ? sl.GetString() : null;
                if (string.IsNullOrEmpty(name)) continue;
                lines.Add(hasSegments && !string.IsNullOrEmpty(seg)
                    ? $"{seg} — {name} {year}: {amt}"
                    : $"{name} {year}: {amt}");
            }
        }

        if (root.TryGetProperty("byCustomer", out var cust) && cust.ValueKind == JsonValueKind.Array &&
            cust.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add("By customer:");
            foreach (var c in cust.EnumerateArray())
            {
                var code = c.TryGetProperty("customerCode", out var cc) ? cc.GetString() : "";
                var name = c.TryGetProperty("customerName", out var cn) ? cn.GetString() : "";
                var amt = c.TryGetProperty("collectionAmount", out var ca) && ca.ValueKind == JsonValueKind.Number
                    ? ca.GetDecimal().ToString("N2")
                    : "0.00";
                lines.Add($"  {name} ({code}): {amt}");
            }
        }

        return (string.Join("\n", lines), serial);
    }

    public const string CustomerCollectionsEngineSerial = "SAGE-AR-COLLECTIONS-001";
}
