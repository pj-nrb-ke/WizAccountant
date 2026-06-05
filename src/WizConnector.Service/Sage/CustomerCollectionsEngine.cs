using System.Data.SqlClient;
using WizAccountant.Contracts;

namespace WizConnector.Service.Sage;

/// <summary>PostAR credit (customer receipt) aggregates for a period (SAGE-PATCH-010).</summary>
internal static class CustomerCollectionsEngine
{
    public const string QuerySerial = "SAGE-AR-COLLECTIONS-001";

    private const string MonthlySql = """
        SELECT
            YEAR(a.TxDate) AS CollectionYear,
            MONTH(a.TxDate) AS MonthNo,
            DATENAME(MONTH, a.TxDate) AS CollectionMonth,
            SUM(ISNULL(a.Credit, 0)) AS CollectionAmount
        FROM PostAR a
        WHERE CAST(a.TxDate AS DATE) >= @pDateFrom
          AND CAST(a.TxDate AS DATE) <= @pDateTo
          AND ISNULL(a.Credit, 0) > 0
        GROUP BY YEAR(a.TxDate), MONTH(a.TxDate), DATENAME(MONTH, a.TxDate)
        ORDER BY CollectionYear, MonthNo
        """;

    private const string TotalSql = """
        SELECT SUM(ISNULL(a.Credit, 0)) AS TotalCollections
        FROM PostAR a
        WHERE CAST(a.TxDate AS DATE) >= @pDateFrom
          AND CAST(a.TxDate AS DATE) <= @pDateTo
          AND ISNULL(a.Credit, 0) > 0
        """;

    private const string ByCustomerSql = """
        SELECT
            C.Account AS CustomerCode,
            ISNULL(C.Name, C.Account) AS CustomerName,
            SUM(ISNULL(a.Credit, 0)) AS CollectionAmount
        FROM PostAR a
        INNER JOIN Client C ON C.DCLink = a.AccountLink
        WHERE CAST(a.TxDate AS DATE) >= @pDateFrom
          AND CAST(a.TxDate AS DATE) <= @pDateTo
          AND ISNULL(a.Credit, 0) > 0
        GROUP BY C.Account, C.Name
        ORDER BY SUM(ISNULL(a.Credit, 0)) DESC
        """;

    public sealed record LoadResult(
        bool Success,
        string? Error,
        InsightPeriodResolution Period,
        decimal TotalCollections,
        IReadOnlyList<SegmentTotalRow> SegmentTotals,
        IReadOnlyList<MonthlyRow> Monthly,
        IReadOnlyList<CustomerRow> Customers);

    public static LoadResult Load(
        string connectionString,
        Dictionary<string, string> parameters,
        bool includeMonthly,
        bool includeCustomers,
        int customerTop)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return new LoadResult(false, "Sage company database connection is not configured.", DefaultPeriod(parameters), 0, [], [], []);

        var period = InsightDateRangeParser.ResolvePeriod(parameters);
        var segments = !period.IsContiguous && period.Segments.Count > 0
            ? period.Segments
            : [new InsightPeriodSegment { From = period.DateFrom, To = period.DateTo, Label = period.OriginalText }];

        try
        {
            var segmentTotals = new List<SegmentTotalRow>();
            var monthly = new List<MonthlyRow>();
            var customerBuckets = new Dictionary<string, (string Name, decimal Amount)>(StringComparer.OrdinalIgnoreCase);
            decimal grandTotal = 0;

            foreach (var segment in segments)
            {
                var segTotalRows = GlSqlHelper.ExecuteQuery(connectionString, TotalSql, cmd =>
                    InvNumSqlHelper.AddDateParameters(cmd, segment.From, segment.To));
                var segTotal = segTotalRows.Count > 0
                    ? GlSqlHelper.ToDecimal(segTotalRows[0], "TotalCollections")
                    : 0m;
                grandTotal += segTotal;
                segmentTotals.Add(new SegmentTotalRow(
                    segment.Label,
                    segment.From,
                    segment.To,
                    segTotal));

                if (includeMonthly)
                {
                    var monthRows = GlSqlHelper.ExecuteQuery(connectionString, MonthlySql, cmd =>
                        InvNumSqlHelper.AddDateParameters(cmd, segment.From, segment.To));
                    monthly.AddRange(monthRows.Select(r => new MonthlyRow(
                        Convert.ToInt32(GlSqlHelper.ToDecimal(r, "CollectionYear")),
                        Convert.ToInt32(GlSqlHelper.ToDecimal(r, "MonthNo")),
                        r["CollectionMonth"]?.ToString() ?? "",
                        GlSqlHelper.ToDecimal(r, "CollectionAmount"),
                        segments.Count > 1 ? segment.Label : null)));
                }

                if (includeCustomers)
                {
                    var custRows = GlSqlHelper.ExecuteQuery(connectionString, ByCustomerSql, cmd =>
                        InvNumSqlHelper.AddDateParameters(cmd, segment.From, segment.To));
                    foreach (var r in custRows)
                    {
                        var code = r["CustomerCode"]?.ToString() ?? "";
                        if (string.IsNullOrWhiteSpace(code)) continue;
                        var name = r["CustomerName"]?.ToString() ?? code;
                        var amt = GlSqlHelper.ToDecimal(r, "CollectionAmount");
                        if (customerBuckets.TryGetValue(code, out var existing))
                            customerBuckets[code] = (existing.Name, existing.Amount + amt);
                        else
                            customerBuckets[code] = (name, amt);
                    }
                }
            }

            var customers = customerBuckets
                .OrderByDescending(kv => kv.Value.Amount)
                .Take(customerTop > 0 ? customerTop : int.MaxValue)
                .Select((kv, i) => new CustomerRow(i + 1, kv.Key, kv.Value.Name, kv.Value.Amount))
                .ToList();

            return new LoadResult(true, null, period, grandTotal, segmentTotals, monthly, customers);
        }
        catch (SqlException ex)
        {
            return new LoadResult(false,
                "Customer collections require PostAR receipt (credit) postings. SQL error: " + ex.Message,
                period, 0, [], [], []);
        }
    }

    private static InsightPeriodResolution DefaultPeriod(Dictionary<string, string> parameters)
    {
        var (from, to) = InvNumSqlHelper.ParseDateRange(parameters, parameters.GetValueOrDefault("message"));
        return new InsightPeriodResolution
        {
            DateFrom = from,
            DateTo = to,
            IsContiguous = true,
            Segments = [new InsightPeriodSegment { From = from, To = to }]
        };
    }

    internal sealed record SegmentTotalRow(string Label, DateTime From, DateTime To, decimal Amount);

    internal sealed record MonthlyRow(int Year, int MonthNo, string MonthName, decimal Amount, string? SegmentLabel);

    internal sealed record CustomerRow(int Rank, string Code, string Name, decimal Amount);
}
