using System.Data;
using System.Text.Json;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

/// <summary>
/// Top N customers by oldest open AR debit (aged debtors). DOCS/Sage_AI_Agent_Top5_Aged_Debtors_Patch.md — SAGE-AR-AGED-TOP-001.
/// </summary>
internal static class CustomerAgedTopHandler
{
    public const string QuerySerial = "SAGE-AR-AGED-TOP-001";

    public static string Execute(Dictionary<string, string> parameters)
    {
        var limit = Math.Clamp(SageListHelpers.ParseIntParam(parameters, "top", 5), 1, 50);
        var names = LoadCustomerNameLookup();
        var dclinkMap = SageCustomerRowResolver.LoadDclinkToAccountMap();
        var today = DateTime.Today;

        var table = CustomerTransaction.List("Outstanding <> 0");
        if (table is null)
        {
            return JsonSerializer.Serialize(EmptyResult(limit));
        }

        var buckets = new Dictionary<string, CustomerAgedBucket>(StringComparer.OrdinalIgnoreCase);
        var totalLines = 0;
        var unallocated = 0;
        var skippedNonInvoice = 0;

        foreach (DataRow row in table.Rows)
        {
            totalLines++;
            if (!SageCustomerRowResolver.IsOpenInvoiceOrOrderLine(row))
            {
                skippedNonInvoice++;
                continue;
            }

            var account = SageCustomerRowResolver.ResolveCustomerCode(row, dclinkMap);
            if (string.IsNullOrWhiteSpace(account) || IsExcludedCashCustomer(account))
            {
                if (string.IsNullOrWhiteSpace(account))
                    unallocated++;
                continue;
            }

            var outstanding = ResolveOutstanding(row);
            if (outstanding is null or <= 0)
                continue;

            var txDate = ParseTxDate(row);
            if (!buckets.TryGetValue(account, out var bucket))
            {
                buckets[account] = bucket = new CustomerAgedBucket(
                    account, ResolveCustomerName(account, names));
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
            intent = "customer_ar_aged_top",
            requestedTop = limit,
            topCustomers = ranked,
            totalOpenLines = totalLines,
            customersWithDebitBalance = buckets.Count,
            unallocatedLines = unallocated,
            skippedNonInvoiceLines = skippedNonInvoice,
            note = "Ranked by oldest open invoice/order date per customer (Outstanding > 0). Excludes payments, zero balances, and CASH account.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }

    private static object EmptyResult(int limit) => new
    {
        querySerial = QuerySerial,
        requestedTop = limit,
        topCustomers = Array.Empty<object>(),
        totalOpenLines = 0,
        customersWithDebitBalance = 0,
        note = "No open AR data returned from Sage.",
        dataAsOfUtc = DateTimeOffset.UtcNow
    };

    private static bool IsExcludedCashCustomer(string account) =>
        account.Equals("CASH", StringComparison.OrdinalIgnoreCase);

    private static DateTime? ParseTxDate(DataRow row)
    {
        var raw = SageListHelpers.Col(row, "TxDate", "Date", "TransactionDate");
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        if (DateTime.TryParse(raw, out var dt))
            return dt.Date;
        return null;
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

    private static Dictionary<string, string> LoadCustomerNameLookup()
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

    private static string ResolveCustomerName(string code, Dictionary<string, string> names)
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

    private sealed class CustomerAgedBucket(string code, string name)
    {
        public string Code { get; } = code;
        public string Name { get; } = name;
        public decimal TotalOutstanding { get; set; }
        public DateTime? OldestInvoiceDate { get; set; }
        public int DaysOutstanding { get; set; }
        public int OpenLineCount { get; set; }
    }
}
