using System.Data;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

/// <summary>Shared AP open-item ranking for Insight handlers.</summary>
internal static class ApSupplierRankingHelper
{
    public static Dictionary<string, string> LoadSupplierNameLookup()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in SageListHelpers.MapRows(Supplier.List("DCLink > 0"), r => r))
        {
            var code = SageListHelpers.Col(row, "Account");
            if (string.IsNullOrWhiteSpace(code)) continue;
            var name = SageListHelpers.Col(row, "Name", "Description");
            dict[code] = string.IsNullOrWhiteSpace(name) ? code : name;
        }

        return dict;
    }

    public static string ResolveSupplierName(string code, Dictionary<string, string> names)
    {
        if (names.TryGetValue(code, out var name) && !string.IsNullOrWhiteSpace(name))
            return name;
        try
        {
            var supplier = new Supplier(code);
            return supplier.Description ?? code;
        }
        catch
        {
            return code;
        }
    }

    public static string? ResolveSupplierCode(DataRow row) =>
        SageListHelpers.Col(row, "Account", "Supplier", "cAccount", "Code")?.Trim();

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
        return Math.Abs((debit ?? 0m) - (credit ?? 0m));
    }

    public static bool IsOpenInvoiceLine(DataRow row)
    {
        var desc = (SageListHelpers.Col(row, "Description") ?? "").ToLowerInvariant();
        var reference = (SageListHelpers.Col(row, "Reference") ?? "").ToLowerInvariant();
        if (desc.Contains("payment") || desc.Contains("receipt") || reference.Contains("pmt"))
            return false;
        return true;
    }
}
