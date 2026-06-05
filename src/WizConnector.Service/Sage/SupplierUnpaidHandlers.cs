using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-PATCH-011 — supplier unpaid AP handlers (count / list / top).</summary>
internal static class SupplierUnpaidHandlers
{
    public const string CountQuerySerial = "SAGE-AP-SUPPLIER-UNPAID-COUNT-001";
    public const string ListQuerySerial = "SAGE-AP-SUPPLIER-UNPAID-LIST-001";
    public const string TopQuerySerial = "SAGE-AP-SUPPLIER-UNPAID-TOP-001";
    public const string SummaryQuerySerial = "SAGE-AP-UNPAID-SUMMARY-001";

    public const string CountOperation = "supplier.unpaid.count";
    public const string ListOperation = "supplier.unpaid.list";
    public const string TopOperation = "supplier.unpaid.top";
    public const string SummaryOperation = "supplier.unpaid.summary";

    public static string ExecuteCount(Dictionary<string, string> parameters) =>
        SerializeCount(SupplierUnpaidEngine.Load());

    public static string ExecuteList(Dictionary<string, string> parameters)
    {
        var snapshot = SupplierUnpaidEngine.Load();
        var limit = Math.Clamp(InvNumSqlHelper.ParseTop(parameters, 500), 1, 500);
        var suppliers = snapshot.Suppliers
            .OrderByDescending(s => s.TotalOutstanding)
            .ThenByDescending(s => s.InvoiceCount)
            .Take(limit)
            .Select(MapRow)
            .ToList();
        return SerializeList(snapshot, suppliers, limit);
    }

    public static string ExecuteTop(Dictionary<string, string> parameters)
    {
        var snapshot = SupplierUnpaidEngine.Load();
        var limit = Math.Clamp(InvNumSqlHelper.ParseTop(parameters, 10), 1, 50);
        var ranked = snapshot.Suppliers
            .OrderByDescending(s => s.TotalOutstanding)
            .ThenByDescending(s => s.InvoiceCount)
            .Take(limit)
            .Select((s, i) => new
            {
                rank = i + 1,
                code = s.Code,
                name = s.Name,
                invoiceCount = s.InvoiceCount,
                totalOutstanding = s.TotalOutstanding
            })
            .ToList();

        return JsonSerializer.Serialize(new
        {
            querySerial = TopQuerySerial,
            operation = TopOperation,
            requestedTop = limit,
            asOfDate = DateTime.Today.ToString("yyyy-MM-dd"),
            totalUnpaidSuppliers = snapshot.TotalUnpaidSuppliers,
            totalOutstandingPayable = snapshot.TotalOutstandingPayable,
            topSuppliers = ranked,
            finding = ranked.Count == 0
                ? "No suppliers with unpaid AP balances found."
                : $"Top {ranked.Count} supplier(s) by outstanding AP balance as of today.",
            note = "Ranked by total open AP outstanding from SupplierTransaction (Outstanding > 0).",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }

    public static string ExecuteSummary(Dictionary<string, string> parameters)
    {
        var countOnly = parameters.TryGetValue("countOnly", out var co) &&
                        co.Equals("true", StringComparison.OrdinalIgnoreCase);
        return countOnly ? ExecuteCount(parameters) : ExecuteList(parameters);
    }

    private static string SerializeCount(SupplierUnpaidEngine.Snapshot snapshot)
    {
        var asOf = DateTime.Today.ToString("yyyy-MM-dd");
        return JsonSerializer.Serialize(new
        {
            querySerial = CountQuerySerial,
            operation = CountOperation,
            countOnly = true,
            aggregationMode = true,
            asOfDate = asOf,
            totalUnpaidSuppliers = snapshot.TotalUnpaidSuppliers,
            totalOutstandingPayable = snapshot.TotalOutstandingPayable,
            suppliersWithUnpaidInvoices = snapshot.TotalUnpaidSuppliers,
            totalOpenLines = snapshot.TotalOpenLines,
            unallocatedLines = snapshot.UnallocatedLines,
            skippedNonInvoiceLines = snapshot.SkippedNonInvoiceLines,
            finding = snapshot.TotalUnpaidSuppliers == 0
                ? "No suppliers with unpaid AP balances as of today."
                : $"Total unpaid suppliers as of today: {snapshot.TotalUnpaidSuppliers:N0}.",
            note = "Distinct suppliers with open AP invoice lines (Outstanding > 0). Excludes payment lines.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }

    private static string SerializeList(
        SupplierUnpaidEngine.Snapshot snapshot,
        IReadOnlyList<object> suppliers,
        int limit)
    {
        var asOf = DateTime.Today.ToString("yyyy-MM-dd");
        return JsonSerializer.Serialize(new
        {
            querySerial = ListQuerySerial,
            operation = ListOperation,
            asOfDate = asOf,
            totalUnpaidSuppliers = snapshot.TotalUnpaidSuppliers,
            totalOutstandingPayable = snapshot.TotalOutstandingPayable,
            shown = suppliers.Count,
            limit,
            suppliers,
            totalOpenLines = snapshot.TotalOpenLines,
            unallocatedLines = snapshot.UnallocatedLines,
            finding = snapshot.TotalUnpaidSuppliers == 0
                ? "No suppliers with unpaid AP balances as of today."
                : $"{snapshot.TotalUnpaidSuppliers:N0} supplier(s) with unpaid AP balances as of today.",
            note = "Suppliers grouped from SupplierTransaction open lines (Outstanding > 0).",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }

    private static object MapRow(SupplierUnpaidEngine.Row row) => new
    {
        code = row.Code,
        name = row.Name,
        invoiceCount = row.InvoiceCount,
        totalOutstanding = row.TotalOutstanding
    };
}
