using System.Globalization;
using System.Text;
using System.Text.Json;

namespace WizAccountant.Api.Insight;

internal static class ProductMonthlyOrdersReplyFormat
{
    public static bool TryFormat(string operation, JsonElement root, out string reply)
    {
        reply = "";
        if (!string.Equals(operation, ProductOrderAnalysisChatMatcher.Operation, StringComparison.OrdinalIgnoreCase))
            return false;

        var sb = new StringBuilder();
        sb.AppendLine("Product Monthly Order Analysis");
        if (root.TryGetProperty("dateFrom", out var df) && root.TryGetProperty("dateTo", out var dt))
            sb.AppendLine($"Period: {df.GetString()} to {dt.GetString()}");
        if (root.TryGetProperty("savedSqlReferenceTitle", out var refTitle) && refTitle.ValueKind == JsonValueKind.String)
            sb.AppendLine($"Saved SQL reference: {refTitle.GetString()}");
        if (root.TryGetProperty("evidenceNote", out var note))
            sb.AppendLine(note.GetString());

        if (root.TryGetProperty("topProductByQuantity", out var top) && top.ValueKind == JsonValueKind.Object)
        {
            sb.AppendLine();
            sb.AppendLine("Top product (overall):");
            sb.AppendLine($"  {Prop(top, "productCode")} — {Prop(top, "productName")}");
            if (TryProp(top, "totalQuantity", out var tq))
                sb.AppendLine($"  Total quantity: {FormatNum(tq)}");
            if (TryProp(top, "totalValue", out var tv))
                sb.AppendLine($"  Total value: {FormatMoney(tv)}");
        }

        if (root.TryGetProperty("monthlyBreakdown", out var rows) && rows.ValueKind == JsonValueKind.Array && rows.GetArrayLength() > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Month          Product        Name                      Quantity        Value");
            foreach (var row in rows.EnumerateArray().Take(80))
            {
                var month = Prop(row, "month");
                var code = Prop(row, "productCode");
                var name = Prop(row, "productName");
                var qty = TryProp(row, "quantity", out var q) ? FormatNum(q) : "";
                var val = TryProp(row, "value", out var v) ? FormatMoney(v) : "";
                sb.AppendLine($"{month,-14} {code,-14} {Truncate(name, 24),-24} {qty,14} {val,16}");
            }
        }

        if (root.TryGetProperty("finding", out var finding))
            sb.AppendLine().AppendLine(finding.GetString());

        reply = sb.ToString().TrimEnd();
        return true;
    }

    private static string FormatNum(JsonElement el) =>
        el.TryGetDecimal(out var d) ? d.ToString("N2", CultureInfo.InvariantCulture) : el.ToString();

    private static string FormatMoney(JsonElement el) =>
        el.TryGetDecimal(out var d) ? d.ToString("N2", CultureInfo.InvariantCulture) : el.ToString();

    private static string Prop(JsonElement el, string camelName)
    {
        if (TryProp(el, camelName, out var v))
            return v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : FormatNum(v);
        return "";
    }

    private static bool TryProp(JsonElement el, string camelName, out JsonElement value)
    {
        if (el.TryGetProperty(camelName, out value))
            return true;
        var pascal = char.ToUpperInvariant(camelName[0]) + camelName[1..];
        return el.TryGetProperty(pascal, out value);
    }

    private static string Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s[..max];
}
