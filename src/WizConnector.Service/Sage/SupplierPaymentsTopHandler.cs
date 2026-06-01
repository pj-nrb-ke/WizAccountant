using System.Data;
using System.Text.Json;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-AP-PAYMENTS-TOP-001 — top suppliers by payment value in current month.</summary>
internal static class SupplierPaymentsTopHandler
{
    public const string QuerySerial = "SAGE-AP-PAYMENTS-TOP-001";

    public static string Execute(Dictionary<string, string> parameters)
    {
        var limit = InvNumSqlHelper.ParseTop(parameters, 10);
        var (from, to) = ResolveMonthRange(parameters);
        var names = ApSupplierRankingHelper.LoadSupplierNameLookup();
        var criteria = $"TxDate >= '{from:yyyy-MM-dd}' AND TxDate <= '{to:yyyy-MM-dd}'";
        var table = SupplierTransaction.List(criteria);
        if (table is null)
            return JsonSerializer.Serialize(new { querySerial = QuerySerial, requestedTop = limit, topSuppliers = Array.Empty<object>(), dataAsOfUtc = DateTimeOffset.UtcNow });

        var buckets = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (DataRow row in table.Rows)
        {
            var credit = SageListHelpers.ParseRowAmount(row, "Credit", "fCredit") ?? 0m;
            if (credit <= 0) continue;
            var code = ApSupplierRankingHelper.ResolveSupplierCode(row);
            if (string.IsNullOrWhiteSpace(code)) continue;
            buckets[code] = buckets.GetValueOrDefault(code) + credit;
        }

        var ranked = buckets.OrderByDescending(kv => kv.Value).Take(limit).Select((kv, i) => new
        {
            rank = i + 1,
            code = kv.Key,
            name = ApSupplierRankingHelper.ResolveSupplierName(kv.Key, names),
            paymentTotal = kv.Value
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = limit,
            periodFrom = from.ToString("yyyy-MM-dd"),
            periodTo = to.ToString("yyyy-MM-dd"),
            topSuppliers = ranked,
            note = "Top suppliers by AP credit (payment) postings in period.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }

    private static (DateTime From, DateTime To) ResolveMonthRange(Dictionary<string, string> parameters)
    {
        if (parameters.TryGetValue("year", out var y) && int.TryParse(y, out var year))
        {
            var month = DateTime.Today.Month;
            if (parameters.TryGetValue("month", out var m) && int.TryParse(m, out var mo))
                month = mo;
            var from = new DateTime(year, month, 1);
            return (from, from.AddMonths(1).AddDays(-1));
        }

        var today = DateTime.Today;
        var start = new DateTime(today.Year, today.Month, 1);
        return (start, today);
    }
}
