using System.Data;
using System.Text.Json;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-AR-OVERDUE-BUCKETS-001 — count open customer invoice lines by aging bucket (aggregation only).</summary>
internal static class ArOverdueAgingBucketHandler
{
    public const string QuerySerial = "SAGE-AR-OVERDUE-BUCKETS-001";

    public static string Execute(Dictionary<string, string> parameters)
    {
        var today = DateTime.Today;
        var dclinkMap = SageCustomerRowResolver.LoadDclinkToAccountMap();
        var table = CustomerTransaction.List("Outstanding <> 0");
        if (table is null)
            return EmptyAggregation();

        var bucket0_30 = 0;
        var bucket31_60 = 0;
        var bucket61_90 = 0;
        var bucket90Plus = 0;
        var total = 0;

        foreach (DataRow row in table.Rows)
        {
            if (!SageCustomerRowResolver.IsOpenInvoiceOrOrderLine(row))
                continue;

            var outstanding = ResolveOutstanding(row);
            if (outstanding is null or <= 0)
                continue;

            var txDate = ParseTxDate(row);
            if (txDate is null)
                continue;

            var days = (today - txDate.Value).Days;
            if (days < 0) continue;

            total++;
            if (days <= 30) bucket0_30++;
            else if (days <= 60) bucket31_60++;
            else if (days <= 90) bucket61_90++;
            else bucket90Plus++;
        }

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            countOnly = true,
            aggregationMode = true,
            totalOverdueInvoices = total,
            buckets = new[]
            {
                new { bucket = "0-30 days", count = bucket0_30 },
                new { bucket = "31-60 days", count = bucket31_60 },
                new { bucket = "61-90 days", count = bucket61_90 },
                new { bucket = "90+ days", count = bucket90Plus }
            },
            finding = $"Overdue open invoice lines: {total:N0} (by age from transaction date).",
            note = "COUNT by aging bucket on open AR invoice lines — not a line listing.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }

    private static string EmptyAggregation() => JsonSerializer.Serialize(new
    {
        querySerial = QuerySerial,
        countOnly = true,
        aggregationMode = true,
        totalOverdueInvoices = 0,
        buckets = Array.Empty<object>(),
        finding = "No overdue open invoice lines found.",
        dataAsOfUtc = DateTimeOffset.UtcNow
    });

    private static DateTime? ParseTxDate(DataRow row)
    {
        var raw = SageListHelpers.Col(row, "TxDate", "Date", "TransactionDate");
        return DateTime.TryParse(raw, out var dt) ? dt.Date : null;
    }

    private static decimal? ResolveOutstanding(DataRow row)
    {
        var direct = SageListHelpers.ParseRowAmount(row, "Outstanding", "fOutstanding", "OutstandingForeign");
        if (direct is not null) return direct;
        var debit = SageListHelpers.ParseRowAmount(row, "Debit", "fDebit");
        var credit = SageListHelpers.ParseRowAmount(row, "Credit", "fCredit");
        if (debit is null && credit is null) return null;
        return Math.Abs((debit ?? 0m) - (credit ?? 0m));
    }
}
