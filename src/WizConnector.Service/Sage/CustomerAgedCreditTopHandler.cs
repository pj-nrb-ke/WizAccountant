using System.Data;
using System.Text.Json;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-AR-AGED-CREDIT-TOP-001 — top N customers by oldest open credit (negative outstanding) AR lines.</summary>
internal static class CustomerAgedCreditTopHandler
{
    public const string QuerySerial = "SAGE-AR-AGED-CREDIT-TOP-001";

    public static string Execute(Dictionary<string, string> parameters)
    {
        var limit = InvNumSqlHelper.ParseTop(parameters, 5);
        var today = DateTime.Today;
        var names = ArCustomerRankingHelper.LoadCustomerNameLookup();
        var dclinkMap = SageCustomerRowResolver.LoadDclinkToAccountMap();
        var table = CustomerTransaction.List("Outstanding <> 0");
        if (table is null)
            return JsonSerializer.Serialize(Empty(limit));

        var buckets = new Dictionary<string, CreditAgedBucket>(StringComparer.OrdinalIgnoreCase);
        foreach (DataRow row in table.Rows)
        {
            if (!SageCustomerRowResolver.IsOpenInvoiceOrOrderLine(row))
                continue;

            var account = SageCustomerRowResolver.ResolveCustomerCode(row, dclinkMap);
            if (string.IsNullOrWhiteSpace(account) || ArCustomerRankingHelper.IsExcludedCashCustomer(account))
                continue;

            var outstanding = ArCustomerRankingHelper.ResolveOutstanding(row);
            if (outstanding is null or >= -0.01m)
                continue;

            var txDate = ArCustomerRankingHelper.ParseTxDate(row);
            if (txDate is null)
                continue;

            if (!buckets.TryGetValue(account, out var bucket))
            {
                buckets[account] = bucket = new CreditAgedBucket(
                    account, ArCustomerRankingHelper.ResolveCustomerName(account, names));
            }

            bucket.CreditTotal += Math.Abs(outstanding.Value);
            bucket.LineCount++;
            if (bucket.OldestCreditDate is null || txDate < bucket.OldestCreditDate)
                bucket.OldestCreditDate = txDate;
        }

        foreach (var b in buckets.Values)
        {
            if (b.OldestCreditDate is not null)
                b.DaysOutstanding = (today - b.OldestCreditDate.Value).Days;
        }

        var ranked = buckets.Values
            .Where(b => b.OldestCreditDate is not null)
            .OrderBy(b => b.OldestCreditDate)
            .Take(limit)
            .Select((b, i) => new
            {
                rank = i + 1,
                code = b.Code,
                name = b.Name,
                creditBalance = -b.CreditTotal,
                oldestCreditDate = b.OldestCreditDate!.Value.ToString("yyyy-MM-dd"),
                daysOutstanding = b.DaysOutstanding,
                openLineCount = b.LineCount
            })
            .ToList();

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = limit,
            topCustomers = ranked,
            countOnly = false,
            note = "Top customers by oldest open credit balance lines (negative outstanding on invoice lines).",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }

    private static object Empty(int limit) => new
    {
        querySerial = QuerySerial,
        requestedTop = limit,
        topCustomers = Array.Empty<object>(),
        dataAsOfUtc = DateTimeOffset.UtcNow
    };

    private sealed class CreditAgedBucket(string code, string name)
    {
        public string Code { get; } = code;
        public string Name { get; } = name;
        public decimal CreditTotal { get; set; }
        public DateTime? OldestCreditDate { get; set; }
        public int DaysOutstanding { get; set; }
        public int LineCount { get; set; }
    }
}
