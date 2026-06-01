using System.Data;
using System.Text.Json;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-AR-UNPAID-OLDER-001 — unpaid invoice lines older than N days (limited TOP, not full dump).</summary>
internal static class CustomerInvoiceUnpaidOlderThanHandler
{
    public const string QuerySerial = "SAGE-AR-UNPAID-OLDER-001";

    public static string Execute(Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 25);
        var minDays = SageListHelpers.ParseIntParam(parameters, "minDaysOutstanding", 30);
        if (parameters.TryGetValue("minDays", out var md) && int.TryParse(md, out var d))
            minDays = Math.Max(1, d);

        var today = DateTime.Today;
        var dclinkMap = SageCustomerRowResolver.LoadDclinkToAccountMap();
        var table = CustomerTransaction.List("Outstanding <> 0");
        if (table is null)
            return JsonSerializer.Serialize(Empty(top, minDays));

        var rows = new List<LineRow>();
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
            if (days < minDays)
                continue;

            rows.Add(new LineRow(
                SageCustomerRowResolver.ResolveCustomerCode(row, dclinkMap) ?? "",
                SageListHelpers.Col(row, "Reference") ?? "",
                outstanding.Value,
                txDate.Value,
                days));
        }

        var ranked = rows
            .OrderByDescending(r => r.DaysOutstanding)
            .Take(top)
            .Select((r, i) => new
            {
                rank = i + 1,
                customerCode = r.Account,
                reference = r.Reference,
                outstanding = r.Outstanding,
                invoiceDate = r.TxDate.ToString("yyyy-MM-dd"),
                daysOutstanding = r.DaysOutstanding
            })
            .ToList();

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = top,
            minDaysOutstanding = minDays,
            invoices = ranked,
            totalMatching = rows.Count,
            countOnly = false,
            note = $"Open unpaid invoice lines at least {minDays} days old (limited to top {top}).",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }

    private static object Empty(int top, int minDays) => new
    {
        querySerial = QuerySerial,
        requestedTop = top,
        minDaysOutstanding = minDays,
        invoices = Array.Empty<object>(),
        totalMatching = 0,
        dataAsOfUtc = DateTimeOffset.UtcNow
    };

    private static DateTime? ParseTxDate(DataRow row)
    {
        var raw = SageListHelpers.Col(row, "TxDate", "Date");
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

    private sealed record LineRow(string Account, string Reference, decimal Outstanding, DateTime TxDate, int DaysOutstanding);
}
