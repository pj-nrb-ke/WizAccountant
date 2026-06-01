using System.Text.Json;

namespace WizAccountant.Api.Insight;

internal static class ArSalesReplyFormat
{
    public static string Wrap(string queryRun, string resultBody) =>
        $"Query run: {queryRun}\n\nResult:\n{resultBody}";

    public static string FormatOverdueBuckets(JsonElement root)
    {
        var total = root.TryGetProperty("totalOverdueInvoices", out var t) ? t.GetInt32() : 0;
        var lines = new List<string> { $"Total overdue open invoice lines: {total:N0}", "" };
        if (root.TryGetProperty("buckets", out var buckets) && buckets.ValueKind == JsonValueKind.Array)
        {
            foreach (var b in buckets.EnumerateArray())
            {
                var name = b.TryGetProperty("bucket", out var bn) ? bn.GetString() : "?";
                var count = b.TryGetProperty("count", out var c) ? c.GetInt32() : 0;
                lines.Add($"  {name}: {count:N0}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string FormatTopCustomers(JsonElement root, string arrayProperty, string valueLabel)
    {
        if (!root.TryGetProperty(arrayProperty, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return "No rows returned.";

        var rows = arr.EnumerateArray().ToList();
        if (rows.Count == 0)
            return "No matching customers.";

        var table = string.Join("\n", rows.Select(r =>
        {
            var rank = r.TryGetProperty("rank", out var rk) ? rk.GetInt32() : 0;
            var code = r.TryGetProperty("code", out var c) ? c.GetString() : "";
            var name = r.TryGetProperty("name", out var n) ? n.GetString() : "";
            decimal? val = null;
            if (r.TryGetProperty(valueLabel, out var v) && v.ValueKind == JsonValueKind.Number)
                val = v.GetDecimal();
            else if (r.TryGetProperty("totalOutstanding", out var t) && t.ValueKind == JsonValueKind.Number)
                val = t.GetDecimal();
            else if (r.TryGetProperty("salesValue", out var s) && s.ValueKind == JsonValueKind.Number)
                val = s.GetDecimal();
            var valBit = val.HasValue ? $" — {val.Value:N2}" : "";
            return $"  {rank}. {name} ({code}){valBit}";
        }));

        var requested = root.TryGetProperty("requestedTop", out var rt) ? rt.GetInt32() : rows.Count;
        return $"Top {rows.Count} (requested {requested}):\n\n{table}";
    }

    public static string FormatInvoiceList(JsonElement root, string arrayProperty = "invoices")
    {
        if (!root.TryGetProperty(arrayProperty, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return "No rows returned.";

        var rows = arr.EnumerateArray().ToList();
        if (rows.Count == 0)
            return "No matching invoices.";

        return string.Join("\n", rows.Select(r =>
        {
            var rank = r.TryGetProperty("rank", out var rk) ? rk.GetInt32() : 0;
            var inv = r.TryGetProperty("invoiceNumber", out var i) ? i.GetString()
                : r.TryGetProperty("reference", out var rf) ? rf.GetString() : "";
            var disc = r.TryGetProperty("discountValue", out var d) && d.ValueKind == JsonValueKind.Number
                ? $" discount {d.GetDecimal():N2}" : "";
            return $"  {rank}. {inv}{disc}";
        }));
    }
}
