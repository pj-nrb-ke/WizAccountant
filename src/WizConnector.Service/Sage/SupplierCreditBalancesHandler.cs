using System.Text.Json;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-AP-CREDIT-BAL-001 — suppliers with credit (overpaid) AP balances.</summary>
internal static class SupplierCreditBalancesHandler
{
    public const string QuerySerial = "SAGE-AP-CREDIT-BAL-001";

    public static string Execute(Dictionary<string, string> parameters)
    {
        var limit = InvNumSqlHelper.ParseTop(parameters, 50);
        var rows = new List<(string Code, string Name, decimal Balance)>();

        foreach (var row in SageListHelpers.MapRows(Supplier.List("DCLink > 0"), r => r))
        {
            var code = SageListHelpers.Col(row, "Account");
            if (string.IsNullOrWhiteSpace(code)) continue;
            var balance = SageListHelpers.ParseRowAmount(row, "DCBalance", "Balance", "fAccBal", "AccountBalance");
            if (balance is null or >= -0.01m) continue;
            rows.Add((code, SageListHelpers.Col(row, "Name", "Description") ?? code, balance.Value));
        }

        var ranked = rows.OrderBy(r => r.Balance).Take(limit).Select((r, i) => new
        {
            rank = i + 1,
            code = r.Code,
            name = r.Name,
            balance = r.Balance
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = limit,
            suppliers = ranked,
            hasCreditBalances = ranked.Count > 0,
            note = "Suppliers where Sage supplier master balance is credit (negative / overpaid).",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
