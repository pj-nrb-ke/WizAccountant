using System.Data;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

/// <summary>Shared AR open-item ranking for Insight SQL handlers.</summary>
internal static class ArCustomerRankingHelper
{
    public static Dictionary<string, string> LoadCustomerNameLookup()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in SageListHelpers.MapRows(Customer.List("DCLink > 0"), r => r))
        {
            var code = SageListHelpers.Col(row, "Account");
            if (string.IsNullOrWhiteSpace(code)) continue;
            var name = SageListHelpers.Col(row, "Name", "Description");
            dict[code] = string.IsNullOrWhiteSpace(name) ? code : name;
        }

        return dict;
    }

    public static string ResolveCustomerName(string code, Dictionary<string, string> names)
    {
        if (names.TryGetValue(code, out var name) && !string.IsNullOrWhiteSpace(name))
            return name;
        try
        {
            var customer = new Customer(code);
            return customer.Description ?? code;
        }
        catch
        {
            return code;
        }
    }

    public static DateTime? ParseTxDate(DataRow row)
    {
        var raw = SageListHelpers.Col(row, "TxDate", "Date", "TransactionDate");
        return DateTime.TryParse(raw, out var dt) ? dt.Date : null;
    }

    public static decimal? ResolveOutstanding(DataRow row)
    {
        var direct = SageListHelpers.ParseRowAmount(row, "Outstanding", "fOutstanding", "OutstandingForeign");
        if (direct is not null) return direct;
        var debit = SageListHelpers.ParseRowAmount(row, "Debit", "fDebit");
        var credit = SageListHelpers.ParseRowAmount(row, "Credit", "fCredit");
        if (debit is null && credit is null) return null;
        return (debit ?? 0m) - (credit ?? 0m);
    }

    public static bool IsExcludedCashCustomer(string account) =>
        account.Equals("CASH", StringComparison.OrdinalIgnoreCase);
}
