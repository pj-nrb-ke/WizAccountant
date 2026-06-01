using System.Data;
using System.Text.Json;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-AP-OUTSTANDING-TOP-001 — top N suppliers by total outstanding payable.</summary>
internal static class SupplierOutstandingTopHandler
{
    public const string QuerySerial = "SAGE-AP-OUTSTANDING-TOP-001";

    public static string Execute(Dictionary<string, string> parameters)
    {
        var limit = InvNumSqlHelper.ParseTop(parameters, 10);
        var names = ApSupplierRankingHelper.LoadSupplierNameLookup();
        var table = SupplierTransaction.List("Outstanding <> 0");
        if (table is null)
            return JsonSerializer.Serialize(new { querySerial = QuerySerial, requestedTop = limit, topSuppliers = Array.Empty<object>(), dataAsOfUtc = DateTimeOffset.UtcNow });

        var buckets = new Dictionary<string, Bucket>(StringComparer.OrdinalIgnoreCase);
        foreach (DataRow row in table.Rows)
        {
            if (!ApSupplierRankingHelper.IsOpenInvoiceLine(row))
                continue;
            var code = ApSupplierRankingHelper.ResolveSupplierCode(row);
            if (string.IsNullOrWhiteSpace(code)) continue;
            var outstanding = ApSupplierRankingHelper.ResolveOutstanding(row);
            if (outstanding is null or <= 0) continue;
            if (!buckets.TryGetValue(code, out var b))
                buckets[code] = b = new Bucket(code, ApSupplierRankingHelper.ResolveSupplierName(code, names));
            b.Total += outstanding.Value;
            b.Lines++;
        }

        var ranked = buckets.Values.OrderByDescending(x => x.Total).Take(limit).Select((b, i) => new
        {
            rank = i + 1,
            code = b.Code,
            name = b.Name,
            totalOutstanding = b.Total,
            openLineCount = b.Lines
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = limit,
            topSuppliers = ranked,
            note = "Top suppliers by total open AP outstanding (not oldest-date ranking).",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }

    private sealed class Bucket(string code, string name)
    {
        public string Code { get; } = code;
        public string Name { get; } = name;
        public decimal Total { get; set; }
        public int Lines { get; set; }
    }
}
