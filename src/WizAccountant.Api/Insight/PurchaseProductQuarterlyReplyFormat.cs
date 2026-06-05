using System.Globalization;
using System.Text;
using System.Text.Json;

namespace WizAccountant.Api.Insight;

internal static class PurchaseProductQuarterlyReplyFormat
{
    public static bool TryFormat(string operation, JsonElement root, out string reply)
    {
        reply = "";
        if (!string.Equals(operation, PurchaseProductQuarterlyChatMatcher.Operation, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(operation, DynamicAnalyticalQueryBuilder.PurchaseProductQuarterlyOperation, StringComparison.OrdinalIgnoreCase))
            return false;

        var sb = new StringBuilder();
        sb.AppendLine("Query run:");
        sb.AppendLine(PurchaseProductQuarterlyChatMatcher.Operation);
        sb.AppendLine();
        sb.AppendLine("Result:");
        if (root.TryGetProperty("productCodes", out var codes) && codes.ValueKind == JsonValueKind.Array)
            sb.AppendLine($"Items included: {string.Join(", ", codes.EnumerateArray().Select(c => c.GetString() ?? ""))}");
        else if (root.TryGetProperty("itemsIncluded", out var items) && items.ValueKind == JsonValueKind.Array)
            sb.AppendLine($"Items included: {string.Join(", ", items.EnumerateArray().Select(c => c.GetString() ?? ""))}");
        if (root.TryGetProperty("year", out var year))
            sb.AppendLine($"Year: {year}");
        if (root.TryGetProperty("savedSqlReferenceTitle", out var refTitle) && refTitle.ValueKind == JsonValueKind.String)
            sb.AppendLine($"Saved SQL reference: {refTitle.GetString()}");

        var breakdownProp = root.TryGetProperty("periodBreakdown", out var periodRows) ? periodRows :
            root.TryGetProperty("quarterlyBreakdown", out var quarterRows) ? quarterRows : default;
        if (breakdownProp.ValueKind == JsonValueKind.Array)
        {
            sb.AppendLine();
            sb.AppendLine("Period         Quantity          Value");
            foreach (var row in breakdownProp.EnumerateArray())
            {
                var period = Prop(row, "periodName");
                if (string.IsNullOrEmpty(period))
                    period = Prop(row, "quarterName");
                var qty = TryProp(row, "totalQuantity", out var q) ? FormatNum(q) : "";
                var val = TryProp(row, "totalValue", out var v) ? FormatMoney(v) : "";
                sb.AppendLine($"{period,-14} {qty,16} {val,16}");
            }
        }

        if (root.TryGetProperty("totalQuantity", out var tq))
            sb.AppendLine().AppendLine($"Grand total quantity: {FormatNum(tq)}");
        if (root.TryGetProperty("totalValue", out var tv))
            sb.AppendLine($"Grand total value: {FormatMoney(tv)}");
        if (root.TryGetProperty("dataAsOfUtc", out var asOf))
            sb.AppendLine().AppendLine($"Data as of: {asOf}");
        else if (root.TryGetProperty("finding", out var finding))
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
}
