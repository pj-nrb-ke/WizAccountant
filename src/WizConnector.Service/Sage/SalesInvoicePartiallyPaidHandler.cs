using System.Data;
using System.Text.Json;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-SALES-INV-PARTIAL-001 — open AR invoice lines with partial payment (outstanding &lt; original).</summary>
internal static class SalesInvoicePartiallyPaidHandler
{
    public const string QuerySerial = "SAGE-SALES-INV-PARTIAL-001";

    public static string Execute(Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 25);
        var dclinkMap = SageCustomerRowResolver.LoadDclinkToAccountMap();
        var table = CustomerTransaction.List("Outstanding <> 0");
        if (table is null)
            return JsonSerializer.Serialize(Empty(top));

        var rows = new List<PartialRow>();
        foreach (DataRow row in table.Rows)
        {
            if (!SageCustomerRowResolver.IsOpenInvoiceOrOrderLine(row))
                continue;

            var outstanding = ResolveOutstanding(row);
            var debit = SageListHelpers.ParseRowAmount(row, "Debit", "fDebit") ?? 0m;
            if (outstanding is null or <= 0 || debit <= 0)
                continue;

            if (outstanding >= debit * 0.999m)
                continue;

            var account = SageCustomerRowResolver.ResolveCustomerCode(row, dclinkMap) ?? "";
            rows.Add(new PartialRow(
                account,
                SageListHelpers.Col(row, "Reference") ?? "",
                SageListHelpers.Col(row, "Description") ?? "",
                debit,
                outstanding.Value,
                SageListHelpers.Col(row, "TxDate", "Date") ?? ""));
        }

        var ranked = rows
            .OrderByDescending(r => r.Outstanding)
            .Take(top)
            .Select((r, i) => new
            {
                rank = i + 1,
                customerCode = r.Account,
                reference = r.Reference,
                description = r.Description,
                originalAmount = r.Debit,
                outstanding = r.Outstanding,
                paidAmount = r.Debit - r.Outstanding,
                txDate = r.TxDate
            })
            .ToList();

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = top,
            invoices = ranked,
            countOnly = false,
            note = "Partially paid: open invoice lines where outstanding is less than original debit.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }

    private static object Empty(int top) => new
    {
        querySerial = QuerySerial,
        requestedTop = top,
        invoices = Array.Empty<object>(),
        note = "No partially paid open invoice lines found.",
        dataAsOfUtc = DateTimeOffset.UtcNow
    };

    private static decimal? ResolveOutstanding(DataRow row)
    {
        var direct = SageListHelpers.ParseRowAmount(row, "Outstanding", "fOutstanding", "OutstandingForeign");
        if (direct is not null) return direct;
        var debit = SageListHelpers.ParseRowAmount(row, "Debit", "fDebit");
        var credit = SageListHelpers.ParseRowAmount(row, "Credit", "fCredit");
        if (debit is null && credit is null) return null;
        return Math.Abs((debit ?? 0m) - (credit ?? 0m));
    }

    private sealed record PartialRow(string Account, string Reference, string Description, decimal Debit, decimal Outstanding, string TxDate);
}
