using System.Data;
using System.Text.Json;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-AP-OVERDUE-COUNT-001 — count open supplier invoice lines (aggregation only).</summary>
internal static class ApOverdueInvoiceCountHandler
{
    public const string QuerySerial = "SAGE-AP-OVERDUE-COUNT-001";

    public static string Execute(Dictionary<string, string> parameters)
    {
        var today = DateTime.Today;
        var table = SupplierTransaction.List("Outstanding <> 0");
        if (table is null)
            return Empty();

        var total = 0;
        foreach (DataRow row in table.Rows)
        {
            if (!ApSupplierRankingHelper.IsOpenInvoiceLine(row))
                continue;
            var outstanding = ApSupplierRankingHelper.ResolveOutstanding(row);
            if (outstanding is null or <= 0)
                continue;
            var txDate = ApSupplierRankingHelper.ParseTxDate(row);
            if (txDate is null || (today - txDate.Value).Days < 0)
                continue;
            total++;
        }

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            countOnly = true,
            aggregationMode = true,
            totalOverdueInvoices = total,
            finding = $"Open supplier invoice lines with outstanding balance: {total:N0}.",
            note = "COUNT on open AP lines — not a supplier master listing.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }

    private static string Empty() => JsonSerializer.Serialize(new
    {
        querySerial = QuerySerial,
        countOnly = true,
        aggregationMode = true,
        totalOverdueInvoices = 0,
        finding = "No open supplier invoice lines found.",
        dataAsOfUtc = DateTimeOffset.UtcNow
    });
}
