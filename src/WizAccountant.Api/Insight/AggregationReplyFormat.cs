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

    public static string FormatSalesCreditNoteCount(JsonElement root)
    {
        var count = root.TryGetProperty("creditNoteCount", out var c) ? c.GetInt32() : 0;
        var totalValue = root.TryGetProperty("totalValue", out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetDecimal()
            : 0m;
        var periodLabel = root.TryGetProperty("periodLabel", out var pl) ? pl.GetString() : null;
        var from = root.TryGetProperty("dateFrom", out var df) ? df.GetString() : null;
        var to = root.TryGetProperty("dateTo", out var dt) ? dt.GetString() : null;

        var lines = new List<string>
        {
            !string.IsNullOrEmpty(periodLabel)
                ? $"Total sales credit notes for {periodLabel}: {count:N0}"
                : $"Total sales credit notes: {count:N0}",
            $"Total value (incl): {totalValue:N2}",
            "",
            $"Total Count: {count:N0}"
        };

        if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to))
            lines.Add($"Period: {from} to {to}");

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
