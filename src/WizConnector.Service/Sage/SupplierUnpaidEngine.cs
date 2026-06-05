using System.Data;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

/// <summary>Shared AP open-balance aggregation for supplier unpaid handlers (SAGE-PATCH-011).</summary>
internal static class SupplierUnpaidEngine
{
    internal sealed record Row(string Code, string Name, int InvoiceCount, decimal TotalOutstanding);

    internal sealed record Snapshot(
        int TotalUnpaidSuppliers,
        decimal TotalOutstandingPayable,
        int TotalOpenLines,
        int UnallocatedLines,
        int SkippedNonInvoiceLines,
        IReadOnlyList<Row> Suppliers);

    public static Snapshot Load()
    {
        var names = ApSupplierRankingHelper.LoadSupplierNameLookup();
        var table = SupplierTransaction.List("Outstanding <> 0");
        if (table is null)
            return new Snapshot(0, 0, 0, 0, 0, []);

        var buckets = new Dictionary<string, SupplierUnpaidBucket>(StringComparer.OrdinalIgnoreCase);
        var totalLines = 0;
        var unallocated = 0;
        var skippedNonInvoice = 0;

        foreach (DataRow row in table.Rows)
        {
            totalLines++;
            if (!ApSupplierRankingHelper.IsOpenInvoiceLine(row))
            {
                skippedNonInvoice++;
                continue;
            }

            var code = ApSupplierRankingHelper.ResolveSupplierCode(row);
            if (string.IsNullOrWhiteSpace(code))
            {
                unallocated++;
                continue;
            }

            var outstanding = ApSupplierRankingHelper.ResolveOutstanding(row);
            if (outstanding is null or <= 0)
                continue;

            if (!buckets.TryGetValue(code, out var bucket))
                buckets[code] = bucket = new SupplierUnpaidBucket(code, ApSupplierRankingHelper.ResolveSupplierName(code, names));

            bucket.InvoiceCount++;
            bucket.TotalOutstanding += outstanding.Value;
        }

        var suppliers = buckets.Values
            .Select(b => new Row(b.Code, b.Name, b.InvoiceCount, b.TotalOutstanding))
            .ToList();

        return new Snapshot(
            suppliers.Count,
            suppliers.Sum(s => s.TotalOutstanding),
            totalLines,
            unallocated,
            skippedNonInvoice,
            suppliers);
    }
}

internal sealed class SupplierUnpaidBucket(string code, string name)
{
    public string Code { get; } = code;
    public string Name { get; } = name;
    public int InvoiceCount { get; set; }
    public decimal TotalOutstanding { get; set; }
}
