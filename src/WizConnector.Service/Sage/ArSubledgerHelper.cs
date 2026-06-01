using System.Data;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

internal static class ArSubledgerHelper
{
    public static (decimal total, int lineCount, List<CustomerContributor> contributors, int unallocatedLines)
        SumOpenArWithContributors(int top = 15)
    {
        var table = CustomerTransaction.List("Outstanding <> 0");
        if (table is null)
            return (0, 0, [], 0);

        var dclinkMap = SageCustomerRowResolver.LoadDclinkToAccountMap();
        var buckets = new Dictionary<string, CustomerContributor>(StringComparer.OrdinalIgnoreCase);
        decimal total = 0;
        var lines = 0;
        var unallocated = 0;

        foreach (DataRow row in table.Rows)
        {
            lines++;
            if (!SageCustomerRowResolver.IsOpenInvoiceOrOrderLine(row))
                continue;

            var outstanding = ArCustomerRankingHelper.ResolveOutstanding(row);
            if (outstanding is null or <= 0) continue;

            total += outstanding.Value;
            var account = SageCustomerRowResolver.ResolveCustomerCode(row, dclinkMap);
            if (string.IsNullOrWhiteSpace(account))
            {
                unallocated++;
                continue;
            }

            if (!buckets.TryGetValue(account, out var bucket))
                buckets[account] = bucket = new CustomerContributor(account);
            bucket.Amount += outstanding.Value;
            bucket.LineCount++;
        }

        var ranked = buckets.Values
            .OrderByDescending(c => c.Amount)
            .Take(top)
            .ToList();

        return (total, lines, ranked, unallocated);
    }

    internal sealed class CustomerContributor(string code)
    {
        public string Code { get; } = code;
        public decimal Amount { get; set; }
        public int LineCount { get; set; }
    }
}
