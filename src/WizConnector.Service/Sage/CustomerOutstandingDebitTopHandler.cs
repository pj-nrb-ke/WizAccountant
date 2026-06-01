using System.Data;
using System.Text.Json;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-AR-OUTSTANDING-DEBIT-TOP-001 — top N customers by total outstanding debit.</summary>
internal static class CustomerOutstandingDebitTopHandler
{
    public const string QuerySerial = "SAGE-AR-OUTSTANDING-DEBIT-TOP-001";

    public static string Execute(Dictionary<string, string> parameters)
    {
        var limit = InvNumSqlHelper.ParseTop(parameters, 10);
        var names = ArCustomerRankingHelper.LoadCustomerNameLookup();
        var dclinkMap = SageCustomerRowResolver.LoadDclinkToAccountMap();
        var table = CustomerTransaction.List("Outstanding <> 0");
        if (table is null)
            return JsonSerializer.Serialize(Empty(limit));

        var buckets = new Dictionary<string, DebitBucket>(StringComparer.OrdinalIgnoreCase);
        foreach (DataRow row in table.Rows)
        {
            if (!SageCustomerRowResolver.IsOpenInvoiceOrOrderLine(row))
                continue;

            var account = SageCustomerRowResolver.ResolveCustomerCode(row, dclinkMap);
            if (string.IsNullOrWhiteSpace(account) || ArCustomerRankingHelper.IsExcludedCashCustomer(account))
                continue;

            var outstanding = ArCustomerRankingHelper.ResolveOutstanding(row);
            if (outstanding is null or <= 0)
                continue;

            if (!buckets.TryGetValue(account, out var bucket))
            {
                buckets[account] = bucket = new DebitBucket(
                    account, ArCustomerRankingHelper.ResolveCustomerName(account, names));
            }

            bucket.LineCount++;
            bucket.TotalOutstanding += outstanding.Value;
        }

        var ranked = buckets.Values
            .OrderByDescending(b => b.TotalOutstanding)
            .Take(limit)
            .Select((b, i) => new
            {
                rank = i + 1,
                code = b.Code,
                name = b.Name,
                totalOutstanding = b.TotalOutstanding,
                openLineCount = b.LineCount
            })
            .ToList();

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = limit,
            topCustomers = ranked,
            countOnly = false,
            note = "Top customers by total open outstanding debit (not oldest-date ranking).",
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

    private sealed class DebitBucket(string code, string name)
    {
        public string Code { get; } = code;
        public string Name { get; } = name;
        public decimal TotalOutstanding { get; set; }
        public int LineCount { get; set; }
    }
}
