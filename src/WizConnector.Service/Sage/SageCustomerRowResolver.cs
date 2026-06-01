using System.Data;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

/// <summary>Resolves customer account codes from AR list rows (handover: AR uses Client/DCLink, not always Account text).</summary>
internal static class SageCustomerRowResolver
{
    public static Dictionary<int, string> LoadDclinkToAccountMap()
    {
        var dict = new Dictionary<int, string>();
        foreach (var row in SageListHelpers.MapRows(Customer.List("DCLink > 0"), r => r))
        {
            var account = SageListHelpers.Col(row, "Account");
            if (string.IsNullOrWhiteSpace(account)) continue;
            if (int.TryParse(SageListHelpers.Col(row, "DCLink", "ID"), out var link))
                dict[link] = account;
        }

        return dict;
    }

    public static string? ResolveCustomerCode(DataRow row, IReadOnlyDictionary<int, string> dclinkMap)
    {
        var code = SageListHelpers.Col(row, "Account", "Customer", "cAccount", "Code");
        if (!string.IsNullOrWhiteSpace(code))
            return code.Trim();

        foreach (var col in new[] { "DCLink", "AccountLink", "Client", "CustomerLink", "iAccountID" })
        {
            var raw = SageListHelpers.Col(row, col);
            if (string.IsNullOrWhiteSpace(raw)) continue;
            if (int.TryParse(raw, out var link) && dclinkMap.TryGetValue(link, out var account))
                return account;
        }

        return null;
    }

    /// <summary>Open invoice/sales-order style AR lines (excludes payments per handover PostGL Id patterns).</summary>
    public static bool IsOpenInvoiceOrOrderLine(DataRow row)
    {
        var id = SageListHelpers.Col(row, "Id", "TrCodeID") ?? "";
        var description = SageListHelpers.Col(row, "Description", "TxType", "Type") ?? "";
        var reference = SageListHelpers.Col(row, "Reference") ?? "";
        return IsOpenInvoiceOrOrderLine(id, description, reference);
    }

    public static bool IsOpenInvoiceOrOrderLine(string? id, string description, string reference)
    {
        if (IsPaymentLine(description, reference))
            return false;

        var idU = id?.Trim().ToUpperInvariant() ?? "";
        if (idU is "OINV" or "INV" or "OS" or "SO")
            return true;

        var text = $"{description} {reference}".ToLowerInvariant();
        if (text.Contains("payment") || text.Contains("received") || text.Contains("receipt"))
            return false;

        return text.Contains("invoice") || text.Contains("sales order") ||
               (text.Contains("order") && !text.Contains("purchase"));
    }

    public static bool IsPaymentLine(string description, string reference)
    {
        var text = $"{description} {reference}".ToLowerInvariant();
        return text.Contains("payment") || text.Contains("receipt") || text.Contains("received");
    }
}
