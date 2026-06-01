using System.Data;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

internal static class ApSubledgerHelper
{
    public static (decimal total, int lineCount, List<SupplierContributor> contributors, int unallocatedLines)
        SumOpenApWithContributors(int top = 15)
    {
        var table = SupplierTransaction.List("Outstanding <> 0");
        if (table is null)
            return (0, 0, [], 0);

        var buckets = new Dictionary<string, SupplierContributor>(StringComparer.OrdinalIgnoreCase);
        decimal total = 0;
        var lines = 0;
        var unallocated = 0;

        foreach (DataRow row in table.Rows)
        {
            lines++;
            if (!ApSupplierRankingHelper.IsOpenInvoiceLine(row))
                continue;

            var outstanding = ApSupplierRankingHelper.ResolveOutstanding(row);
            if (outstanding is null or <= 0) continue;

            total += outstanding.Value;
            var account = ApSupplierRankingHelper.ResolveSupplierCode(row);
            if (string.IsNullOrWhiteSpace(account))
            {
                unallocated++;
                continue;
            }

            if (!buckets.TryGetValue(account, out var bucket))
                buckets[account] = bucket = new SupplierContributor(account);
            bucket.Amount += outstanding.Value;
            bucket.LineCount++;
        }

        var ranked = buckets.Values
            .OrderByDescending(c => c.Amount)
            .Take(top)
            .ToList();

        return (total, lines, ranked, unallocated);
    }

    internal sealed class SupplierContributor(string code)
    {
        public string Code { get; } = code;
        public decimal Amount { get; set; }
        public int LineCount { get; set; }
    }
}
