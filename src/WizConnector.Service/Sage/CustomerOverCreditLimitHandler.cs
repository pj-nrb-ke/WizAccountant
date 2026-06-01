using System.Text.Json;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-AR-CREDIT-LIMIT-001 — customers where balance exceeds credit limit.</summary>
internal static class CustomerOverCreditLimitHandler
{
    public const string QuerySerial = "SAGE-AR-CREDIT-LIMIT-001";

    public static string Execute(Dictionary<string, string> parameters)
    {
        var limit = InvNumSqlHelper.ParseTop(parameters, 25);
        var rows = new List<OverLimitRow>();

        foreach (var row in SageListHelpers.MapRows(Customer.List("DCLink > 0"), r => r))
        {
            var code = SageListHelpers.Col(row, "Account");
            if (string.IsNullOrWhiteSpace(code)) continue;

            var balance = SageListHelpers.ParseRowAmount(row, "DCBalance", "Balance", "fAccBal", "AccountBalance");
            var creditLimit = SageListHelpers.ParseRowAmount(row, "CreditLimit", "fCreditLimit", "Credit_Limit");
            if (balance is null || creditLimit is null || creditLimit <= 0)
                continue;

            if (balance <= creditLimit)
                continue;

            var name = SageListHelpers.Col(row, "Name", "Description") ?? code;
            rows.Add(new OverLimitRow(code, name, balance.Value, creditLimit.Value));
        }

        var ranked = rows
            .OrderByDescending(r => r.OverBy)
            .Take(limit)
            .Select((r, i) => new
            {
                rank = i + 1,
                code = r.Code,
                name = r.Name,
                balance = r.Balance,
                creditLimit = r.CreditLimit,
                overBy = r.OverBy
            })
            .ToList();

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = limit,
            customers = ranked,
            countOnly = false,
            note = "Customers where Sage account balance exceeds credit limit.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }

    private sealed record OverLimitRow(string Code, string Name, decimal Balance, decimal CreditLimit)
    {
        public decimal OverBy => Balance - CreditLimit;
    }
}
