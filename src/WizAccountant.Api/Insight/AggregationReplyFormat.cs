using System.Text.Json;

namespace WizAccountant.Api.Insight;

internal static class AggregationReplyFormat
{
    public static string FormatSalesInvoiceDiscountCount(JsonElement root)
    {
        var year = root.TryGetProperty("year", out var y) ? y.GetInt32() : 0;
        var count = root.TryGetProperty("invoiceCount", out var c) ? c.GetInt32() : 0;

        var lines = new List<string>
        {
            year > 0
                ? $"Total sales invoices with discounts in {year}: {count:N0}"
                : $"Total sales invoices with discounts: {count:N0}",
            "",
            $"Total Count: {count:N0}"
        };

        if (root.TryGetProperty("averageDiscountValue", out var avg) && avg.ValueKind == JsonValueKind.Number)
            lines.Add($"Average discount value (header): {avg.GetDecimal():N2}");

        if (root.TryGetProperty("highestDiscountInvoice", out var hi) && hi.ValueKind == JsonValueKind.String)
        {
            var inv = hi.GetString();
            if (!string.IsNullOrEmpty(inv))
                lines.Add($"Highest discount invoice (reference): {inv}");
        }

        if (root.TryGetProperty("finding", out var f) && f.ValueKind == JsonValueKind.String)
        {
            var finding = f.GetString();
            if (!string.IsNullOrEmpty(finding))
            {
                lines.Add("");
                lines.Add(finding);
            }
        }

        return string.Join(Environment.NewLine, lines);
    }
}
