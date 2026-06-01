using System.Data;
using System.Text.Json;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-AP-PARTIAL-001 — partially paid open supplier invoice lines.</summary>
internal static class PurchaseInvoicePartiallyPaidHandler
{
    public const string QuerySerial = "SAGE-AP-PARTIAL-001";

    public static string Execute(Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 25);
        var table = SupplierTransaction.List("Outstanding <> 0");
        if (table is null)
            return JsonSerializer.Serialize(new { querySerial = QuerySerial, requestedTop = top, invoices = Array.Empty<object>(), dataAsOfUtc = DateTimeOffset.UtcNow });

        var rows = new List<(string Account, string Ref, decimal Debit, decimal Out, string Date)>();
        foreach (DataRow row in table.Rows)
        {
            if (!ApSupplierRankingHelper.IsOpenInvoiceLine(row)) continue;
            var outstanding = ApSupplierRankingHelper.ResolveOutstanding(row);
            var debit = SageListHelpers.ParseRowAmount(row, "Debit", "fDebit") ?? 0m;
            if (outstanding is null or <= 0 || debit <= 0 || outstanding >= debit * 0.999m) continue;
            rows.Add((
                ApSupplierRankingHelper.ResolveSupplierCode(row) ?? "",
                SageListHelpers.Col(row, "Reference") ?? "",
                debit,
                outstanding.Value,
                SageListHelpers.Col(row, "TxDate", "Date") ?? ""));
        }

        var ranked = rows.OrderByDescending(r => r.Out).Take(top).Select((r, i) => new
        {
            rank = i + 1,
            supplierCode = r.Account,
            reference = r.Ref,
            originalAmount = r.Debit,
            outstanding = r.Out,
            paidAmount = r.Debit - r.Out,
            txDate = r.Date
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = top,
            invoices = ranked,
            note = "Partially paid supplier invoice lines (outstanding < original debit).",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
