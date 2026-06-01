using System.Data;
using System.Text.Json;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

/// <summary>Top N suppliers by oldest open AP balance — digest AP #26 proxy.</summary>
internal static class SupplierAgedTopHandler
{
    public const string QuerySerial = "SAGE-AP-AGED-TOP-001";

    public static string Execute(Dictionary<string, string> parameters)
    {
        var limit = Math.Clamp(SageListHelpers.ParseIntParam(parameters, "top", 5), 1, 50);
        var names = LoadSupplierNameLookup();
        var today = DateTime.Today;

        var table = SupplierTransaction.List("Outstanding <> 0");
        if (table is null)
        {
            return JsonSerializer.Serialize(new
            {
                querySerial = QuerySerial,
                requestedTop = limit,
                topSuppliers = Array.Empty<object>(),
                note = "No open AP data returned from Sage.",
                dataAsOfUtc = DateTimeOffset.UtcNow
            });
        }

        var buckets = new Dictionary<string, SupplierAgedBucket>(StringComparer.OrdinalIgnoreCase);

        foreach (DataRow row in table.Rows)
        {
            var account = SageListHelpers.Col(row, "Account", "Supplier", "cAccount", "Code")?.Trim();
            if (string.IsNullOrWhiteSpace(account))
                continue;

            var outstanding = ResolveOutstanding(row);
            if (outstanding is null or <= 0)
                continue;

            var txDate = ParseTxDate(row);
            if (!buckets.TryGetValue(account, out var bucket))
            {
                buckets[account] = bucket = new SupplierAgedBucket(
                    account, ResolveSupplierName(account, names));
            }

            bucket.OpenLineCount++;
            bucket.TotalOutstanding += outstanding.Value;
            if (txDate is not null && (bucket.OldestInvoiceDate is null || txDate < bucket.OldestInvoiceDate))
                bucket.OldestInvoiceDate = txDate;
        }

        foreach (var bucket in buckets.Values)
        {
            if (bucket.OldestInvoiceDate is not null)
                bucket.DaysOutstanding = (today - bucket.OldestInvoiceDate.Value).Days;
        }

        var ranked = buckets.Values
            .Where(b => b.TotalOutstanding > 0 && b.OldestInvoiceDate is not null)
            .OrderBy(b => b.OldestInvoiceDate)
            .ThenByDescending(b => b.DaysOutstanding)
            .Take(limit)
            .Select((b, i) => new
            {
                rank = i + 1,
                code = b.Code,
                name = b.Name,
                totalOutstanding = b.TotalOutstanding,
                oldestInvoiceDate = b.OldestInvoiceDate!.Value.ToString("yyyy-MM-dd"),
                daysOutstanding = b.DaysOutstanding,
                openLineCount = b.OpenLineCount
            })
            .ToList();

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = limit,
            topSuppliers = ranked,
            suppliersWithDebitBalance = buckets.Count,
            note = "Ranked by oldest open supplier transaction date (Outstanding > 0).",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }

    private static DateTime? ParseTxDate(DataRow row)
    {
        var raw = SageListHelpers.Col(row, "TxDate", "Date");
        return string.IsNullOrWhiteSpace(raw) || !DateTime.TryParse(raw, out var dt) ? null : dt.Date;
    }

    private static decimal? ResolveOutstanding(DataRow row)
    {
        var direct = SageListHelpers.ParseRowAmount(row, "Outstanding", "fOutstanding", "OutstandingForeign");
        if (direct is not null) return direct;
        var debit = SageListHelpers.ParseRowAmount(row, "Debit", "fDebit");
        var credit = SageListHelpers.ParseRowAmount(row, "Credit", "fCredit");
        if (debit is null && credit is null) return null;
        return Math.Abs((debit ?? 0m) - (credit ?? 0m));
    }

    private static Dictionary<string, string> LoadSupplierNameLookup()
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

    private static string ResolveSupplierName(string code, Dictionary<string, string> names)
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

    private sealed class SupplierAgedBucket(string code, string name)
    {
        public string Code { get; } = code;
        public string Name { get; } = name;
        public decimal TotalOutstanding { get; set; }
        public DateTime? OldestInvoiceDate { get; set; }
        public int DaysOutstanding { get; set; }
        public int OpenLineCount { get; set; }
    }
}
