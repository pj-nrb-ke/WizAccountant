using System.Data;
using System.Text.Json;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-AP-UNPAID-OLDER-001 — supplier invoices older than N days (limited TOP).</summary>
internal static class SupplierInvoiceUnpaidOlderThanHandler
{
    public const string QuerySerial = "SAGE-AP-UNPAID-OLDER-001";

    public static string Execute(Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 25);
        var minDays = SageListHelpers.ParseIntParam(parameters, "minDaysOutstanding", 30);
        var today = DateTime.Today;
        var table = SupplierTransaction.List("Outstanding <> 0");
        if (table is null)
            return JsonSerializer.Serialize(Empty(top, minDays));

        var rows = new List<(string Account, string Reference, decimal Outstanding, DateTime TxDate, int Days)>();
        foreach (DataRow row in table.Rows)
        {
            if (!ApSupplierRankingHelper.IsOpenInvoiceLine(row))
                continue;
            var outstanding = ApSupplierRankingHelper.ResolveOutstanding(row);
            if (outstanding is null or <= 0) continue;
            var txDate = ApSupplierRankingHelper.ParseTxDate(row);
            if (txDate is null) continue;
            var days = (today - txDate.Value).Days;
            if (days < minDays) continue;
            rows.Add((
                ApSupplierRankingHelper.ResolveSupplierCode(row) ?? "",
                SageListHelpers.Col(row, "Reference") ?? "",
                outstanding.Value,
                txDate.Value,
                days));
        }

        var ranked = rows.OrderByDescending(r => r.Days).Take(top).Select((r, i) => new
        {
            rank = i + 1,
            supplierCode = r.Account,
            reference = r.Reference,
            outstanding = r.Outstanding,
            invoiceDate = r.TxDate.ToString("yyyy-MM-dd"),
            daysOutstanding = r.Days
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = top,
            minDaysOutstanding = minDays,
            invoices = ranked,
            totalMatching = rows.Count,
            countOnly = false,
            note = $"Open supplier invoice lines at least {minDays} days old (top {top}).",
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
}
