using System.Data.SqlClient;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WizConnector.Service.Sage;

/// <summary>
/// Count distinct sales/tax invoices in a calendar year that have line or header discount (InvNum layer).
/// SAGE-SALES-INV-DISC-COUNT-001 — not CustomerTransaction open items.
/// </summary>
internal static class SalesInvoiceDiscountCountHandler
{
    public const string QuerySerial = "SAGE-SALES-INV-DISC-COUNT-001";

    private static readonly string CountSql = $"""
        DECLARE @From DATE = @pDateFrom;
        DECLARE @To DATE = @pDateTo;

        SELECT COUNT(DISTINCT H.AutoIndex) AS InvoiceCount
        FROM InvNum H
        WHERE CAST(H.InvDate AS DATE) >= @From
          AND CAST(H.InvDate AS DATE) <= @To
          AND {InvNumSqlHelper.DocStateAnalyticsExclusionFilter}
          AND {InvNumSqlHelper.SalesDocTypeFilter}
          AND (
                ISNULL(H.InvDisc, 0) <> 0
                OR ISNULL(H.InvDiscAmnt, 0) <> 0
                OR ISNULL(H.InvDiscAmntEx, 0) <> 0
                OR ISNULL(H.DiscValue, 0) > 0
                OR ISNULL(H.DiscPercentage, 0) > 0
                OR EXISTS (
                    SELECT 1
                    FROM _btblInvoiceLines L
                    WHERE L.iInvoiceID = H.AutoIndex
                      AND (
                            ISNULL(L.fLineDiscount, 0) <> 0
                            OR ISNULL(L.fLineDiscountAmnt, 0) <> 0
                            OR ISNULL(L.fLineDiscountAmntEx, 0) <> 0
                          )
                )
              );
        """;

    private static readonly string AvgDiscSql = $"""
        SELECT AVG(CAST(ISNULL(H.InvDiscAmnt, 0) AS DECIMAL(18, 2)))
        FROM InvNum H
        WHERE CAST(H.InvDate AS DATE) >= @pDateFrom AND CAST(H.InvDate AS DATE) <= @pDateTo
          AND {InvNumSqlHelper.DocStateAnalyticsExclusionFilter}
          AND {InvNumSqlHelper.SalesDocTypeFilter}
          AND (ISNULL(H.InvDiscAmnt, 0) <> 0 OR ISNULL(H.DiscValue, 0) > 0);
        """;

    private static readonly string TopDiscSql = $"""
        SELECT TOP 1 H.InvNumber
        FROM InvNum H
        WHERE CAST(H.InvDate AS DATE) >= @pDateFrom AND CAST(H.InvDate AS DATE) <= @pDateTo
          AND {InvNumSqlHelper.DocStateAnalyticsExclusionFilter}
          AND {InvNumSqlHelper.SalesDocTypeFilter}
          AND (ISNULL(H.InvDiscAmnt, 0) <> 0 OR ISNULL(H.DiscValue, 0) > 0)
        ORDER BY ISNULL(H.InvDiscAmnt, 0) DESC;
        """;

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrWhiteSpace(companyConnectionString))
            throw new InvalidOperationException("Sage company database connection is not configured.");

        var year = ParseYear(parameters);
        var dateFrom = new DateTime(year, 1, 1);
        var dateTo = new DateTime(year, 12, 31);

        try
        {
            var count = RunCount(companyConnectionString, dateFrom, dateTo);
            var stats = TryStats(companyConnectionString, dateFrom, dateTo);

            return JsonSerializer.Serialize(new
            {
                querySerial = QuerySerial,
                year,
                dateFrom = dateFrom.ToString("yyyy-MM-dd"),
                dateTo = dateTo.ToString("yyyy-MM-dd"),
                invoiceCount = count,
                countOnly = true,
                aggregationMode = true,
                averageDiscountValue = stats?.AvgDiscount,
                highestDiscountInvoice = stats?.TopInvoiceNumber,
                finding = count == 0
                    ? $"No sales invoices in {year} were found with discounts on the invoice header or lines."
                    : $"Total sales invoices with discounts in {year}: {count}.",
                note = "Aggregation mode: COUNT on InvNum (DocType 4) with discount fields — not CustomerTransaction.List.",
                dataAsOfUtc = DateTimeOffset.UtcNow
            });
        }
        catch (SqlException ex) when (ex.Message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Sage invoice discount count SQL failed — missing InvNum or _btblInvoiceLines ({ex.Message}). " +
                "Verify Evolution schema on this company database.",
                ex);
        }
    }

    private static int RunCount(string connectionString, DateTime dateFrom, DateTime dateTo)
    {
        using var conn = new SqlConnection(connectionString);
        conn.Open();
        using var cmd = new SqlCommand(CountSql, conn) { CommandTimeout = 120 };
        cmd.Parameters.AddWithValue("@pDateFrom", dateFrom);
        cmd.Parameters.AddWithValue("@pDateTo", dateTo);
        var result = cmd.ExecuteScalar();
        return result is null or DBNull ? 0 : Convert.ToInt32(result);
    }

    private static int ParseYear(Dictionary<string, string> parameters)
    {
        if (parameters.TryGetValue("year", out var y) && int.TryParse(y, out var year) && year is >= 1990 and <= 2100)
            return year;

        if (parameters.TryGetValue("message", out var msg))
        {
            var fromMsg = ExtractYearFromText(msg);
            if (fromMsg.HasValue)
                return fromMsg.Value;
        }

        return DateTime.Today.Year;
    }

    private static StatsRow? TryStats(string connectionString, DateTime dateFrom, DateTime dateTo)
    {
        try
        {
            using var conn = new SqlConnection(connectionString);
            conn.Open();

            decimal? avg = null;
            using (var avgCmd = new SqlCommand(AvgDiscSql, conn) { CommandTimeout = 60 })
            {
                avgCmd.Parameters.AddWithValue("@pDateFrom", dateFrom);
                avgCmd.Parameters.AddWithValue("@pDateTo", dateTo);
                var avgResult = avgCmd.ExecuteScalar();
                if (avgResult is not null and not DBNull)
                    avg = Convert.ToDecimal(avgResult);
            }

            string? topInv = null;
            using (var topCmd = new SqlCommand(TopDiscSql, conn) { CommandTimeout = 60 })
            {
                topCmd.Parameters.AddWithValue("@pDateFrom", dateFrom);
                topCmd.Parameters.AddWithValue("@pDateTo", dateTo);
                topInv = topCmd.ExecuteScalar()?.ToString();
            }

            if (avg is null && string.IsNullOrEmpty(topInv))
                return null;

            return new StatsRow(avg ?? 0, topInv ?? "");
        }
        catch
        {
            return null;
        }
    }

    private sealed record StatsRow(decimal AvgDiscount, string TopInvoiceNumber);

    public static int? ExtractYearFromText(string text)
    {
        var match = Regex.Match(text, @"\b(20\d{2})\b");
        return match.Success && int.TryParse(match.Groups[1].Value, out var y) ? y : null;
    }
}
