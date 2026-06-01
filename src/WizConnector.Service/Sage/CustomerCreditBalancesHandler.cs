using System.Data;
using System.Text.Json;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

/// <summary>Customers with credit (negative) AR balances — digest AR #2.</summary>
internal static class CustomerCreditBalancesHandler
{
    public const string QuerySerial = "SAGE-AR-CREDIT-BAL-001";

    public static string Execute(Dictionary<string, string> parameters)
    {
        var limit = Math.Clamp(SageListHelpers.ParseIntParam(parameters, "top", 50), 1, 100);
        var rows = new List<CreditRow>();

        foreach (var row in SageListHelpers.MapRows(Customer.List("DCLink > 0"), r => r))
        {
            var code = SageListHelpers.Col(row, "Account");
            if (string.IsNullOrWhiteSpace(code)) continue;

            var balance = SageListHelpers.ParseRowAmount(row, "DCBalance", "Balance", "fAccBal", "AccountBalance");
            if (balance is null or >= -0.01m) continue;

            var name = SageListHelpers.Col(row, "Name", "Description") ?? code;
            rows.Add(new CreditRow(code, name, balance.Value));
        }

        var ranked = rows
            .OrderBy(r => r.Balance)
            .Take(limit)
            .Select((r, i) => new
            {
                rank = i + 1,
                code = r.Code,
                name = r.Name,
                balance = r.Balance
            })
            .ToList();

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = limit,
            hasCreditBalances = ranked.Count > 0,
            customers = ranked,
            totalCreditBalance = ranked.Sum(r => r.balance),
            note = "Customers where Sage customer balance is credit (negative). Not the same as open invoice lines.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }

    private sealed record CreditRow(string Code, string Name, decimal Balance);
}
