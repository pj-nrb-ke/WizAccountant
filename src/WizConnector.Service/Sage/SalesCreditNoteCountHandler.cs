using System.Data.SqlClient;
using System.Text.Json;
using WizAccountant.Contracts;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-AR-CREDIT-NOTE-COUNT-001 — count sales credit notes posted in period (InvNum DocType 1).</summary>
internal static class SalesCreditNoteCountHandler
{
    public const string QuerySerial = "SAGE-AR-CREDIT-NOTE-COUNT-001";

    private static readonly string CountSql = $"""
        SELECT
            COUNT(DISTINCT H.AutoIndex) AS CreditNoteCount,
            SUM(ISNULL(H.InvTotIncl, 0)) AS TotalValue
        FROM InvNum H
        WHERE CAST(H.InvDate AS DATE) >= @pDateFrom
          AND CAST(H.InvDate AS DATE) <= @pDateTo
          AND {InvNumSqlHelper.DocStateAnalyticsExclusionFilter}
          AND {InvNumSqlHelper.SalesCreditNoteDocTypeFilter};
        """;

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrWhiteSpace(companyConnectionString))
            throw new InvalidOperationException("Sage company database connection is not configured.");

        var period = InsightDateRangeParser.ResolvePeriod(parameters);
        var (from, to) = (period.DateFrom, period.DateTo);

        using var conn = new SqlConnection(companyConnectionString);
        conn.Open();
        using var cmd = new SqlCommand(CountSql, conn) { CommandTimeout = 120 };
        InvNumSqlHelper.AddDateParameters(cmd, from, to);
        using var reader = cmd.ExecuteReader();
        var count = 0;
        decimal totalValue = 0;
        if (reader.Read())
        {
            count = reader["CreditNoteCount"] is DBNull ? 0 : Convert.ToInt32(reader["CreditNoteCount"]);
            totalValue = reader["TotalValue"] is DBNull ? 0 : Convert.ToDecimal(reader["TotalValue"]);
        }

        var periodLabel = period.Segments.Count == 1 && !string.IsNullOrWhiteSpace(period.Segments[0].Label)
            ? period.Segments[0].Label
            : $"{from:yyyy-MM-dd} to {to:yyyy-MM-dd}";

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            dateFrom = from.ToString("yyyy-MM-dd"),
            dateTo = to.ToString("yyyy-MM-dd"),
            periodType = period.PeriodType,
            periodLabel,
            creditNoteCount = count,
            totalValue,
            countOnly = true,
            aggregationMode = true,
            finding = count == 0
                ? $"No sales credit notes found for {periodLabel}."
                : $"Total sales credit notes for {periodLabel}: {count:N0} (value {totalValue:N2}).",
            note = "COUNT on InvNum sales credit notes (DocType 1) — not customer credit balances or PostAR listing.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
