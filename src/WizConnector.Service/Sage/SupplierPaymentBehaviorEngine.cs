using System.Data.SqlClient;

namespace WizConnector.Service.Sage;

/// <summary>
/// AP payment discipline engine — measures how promptly WE pay our suppliers.
/// Uses InvNum (DocType 5, purchase invoices) + Vendor table.
/// Metrics: % invoices fully paid, avg days overdue, overdue exposure.
/// Note: exact payment dates unavailable without PostAP InvNumKey linkage;
/// "paid" = Outstanding &lt;= 0.01.
/// </summary>
internal static class SupplierPaymentBehaviorEngine
{
    public const string QuerySerial = "SAGE-AP-PAYMENT-BEHAVIOR-001";

    private const string MetricsSql = """
        WITH InvoiceBase AS (
            SELECT
                V.Account  AS SupplierCode,
                ISNULL(V.Name, V.Account) AS SupplierName,
                CAST(H.InvDate AS DATE) AS InvoiceDate,
                CAST(COALESCE(H.DueDate, DATEADD(DAY, 30, H.InvDate)) AS DATE) AS DueDate,
                ISNULL(H.InvTotIncl, 0) AS InvoiceAmount,
                ISNULL(H.Outstanding, ISNULL(H.InvTotOutstanding, 0)) AS Outstanding
            FROM InvNum H
            INNER JOIN Vendor V ON V.DCLink = H.AccountID
            WHERE CAST(H.InvDate AS DATE) >= @pDateFrom
              AND CAST(H.InvDate AS DATE) <= @pDateTo
              AND ISNULL(H.DocState, 0) NOT IN (2, 5, 6, 7)
              AND (H.DocType = 5)
        ),
        Scored AS (
            SELECT
                SupplierCode,
                SupplierName,
                Outstanding,
                CASE WHEN Outstanding <= 0.01 THEN 1 ELSE 0 END AS IsPaid,
                CASE WHEN Outstanding > 0.01
                      AND DueDate < CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END AS IsOverdue,
                CASE WHEN Outstanding > 0.01
                      AND DueDate < CAST(GETDATE() AS DATE)
                     THEN DATEDIFF(DAY, DueDate, CAST(GETDATE() AS DATE)) END AS DaysOverdue
            FROM InvoiceBase
        )
        SELECT
            SupplierCode,
            SupplierName,
            COUNT(*)                                                                AS InvoicesAnalyzed,
            SUM(IsPaid)                                                             AS PaidInvoices,
            SUM(IsOverdue)                                                          AS OverdueInvoices,
            SUM(CASE WHEN Outstanding > 0.01 THEN Outstanding ELSE 0 END)          AS CurrentOverdue,
            ISNULL(AVG(CAST(DaysOverdue AS FLOAT)), 0)                             AS AvgDaysOverdue,
            CASE WHEN SUM(IsPaid) + SUM(IsOverdue) > 0
                 THEN CAST(SUM(IsPaid) AS FLOAT) / NULLIF(COUNT(*), 0)
                 ELSE 0 END                                                         AS PaidRatio
        FROM Scored
        GROUP BY SupplierCode, SupplierName
        HAVING COUNT(*) >= @pMinInvoices
        """;

    public static (bool Success, string? Error, List<SupplierPaymentMetrics> Metrics) LoadMetrics(
        string connectionString,
        Dictionary<string, string> parameters,
        string? supplierCodeFilter = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return (false, "Sage company database connection is not configured.", []);

        var (from, to) = InvNumSqlHelper.ParseDateRange(parameters, parameters.GetValueOrDefault("message"));
        var minInvoices = Math.Clamp(SageListHelpers.ParseIntParam(parameters, "minInvoices", 2), 1, 20);

        try
        {
            var sql = MetricsSql;
            if (!string.IsNullOrWhiteSpace(supplierCodeFilter))
                sql += "\nAND SupplierCode = @pSupplierCode";

            var rows = GlSqlHelper.ExecuteQuery(connectionString, sql, cmd =>
            {
                InvNumSqlHelper.AddDateParameters(cmd, from, to);
                cmd.Parameters.AddWithValue("@pMinInvoices", minInvoices);
                if (!string.IsNullOrWhiteSpace(supplierCodeFilter))
                    cmd.Parameters.AddWithValue("@pSupplierCode", supplierCodeFilter.Trim());
            });

            return (true, null, rows.Select(MapRow).ToList());
        }
        catch (SqlException ex)
        {
            return (false,
                "Supplier payment discipline requires InvNum purchase invoices (DocType 5) and Vendor table. " +
                $"SQL error: {ex.Message}",
                []);
        }
    }

    private static SupplierPaymentMetrics MapRow(Dictionary<string, object?> r)
    {
        var paidRatio    = (decimal)GlSqlHelper.ToDecimal(r, "PaidRatio");
        var avgOverdue   = GlSqlHelper.ToDecimal(r, "AvgDaysOverdue");
        var overdue      = GlSqlHelper.ToDecimal(r, "CurrentOverdue");
        var paid         = (int)GlSqlHelper.ToDecimal(r, "PaidInvoices");
        var analyzed     = (int)GlSqlHelper.ToDecimal(r, "InvoicesAnalyzed");
        var overdueCount = (int)GlSqlHelper.ToDecimal(r, "OverdueInvoices");

        return new SupplierPaymentMetrics(
            r["SupplierCode"]?.ToString() ?? "",
            r["SupplierName"]?.ToString() ?? "",
            analyzed,
            paid,
            overdueCount,
            paidRatio,
            avgOverdue,
            overdue,
            ComputeScore(paidRatio, avgOverdue, overdue, paid));
    }

    /// <summary>
    /// Higher score = we pay this supplier promptly and have low overdue exposure.
    /// Scale 0–100.
    /// </summary>
    internal static int ComputeScore(
        decimal paidRatio, decimal avgDaysOverdue, decimal currentOverdue, int paidInvoices)
    {
        var promptPart  = Math.Clamp(paidRatio * 50m, 0m, 50m);
        var overduePart = Math.Clamp(20m - Math.Max(0m, avgDaysOverdue), 0m, 20m);
        var expPart     = currentOverdue <= 0.01m ? 15m : Math.Clamp(15m - (currentOverdue / 100_000m), 0m, 15m);
        var volPart     = Math.Clamp(paidInvoices / 5m * 15m, 0m, 15m);
        return (int)Math.Round(promptPart + overduePart + expPart + volPart);
    }

    internal sealed record SupplierPaymentMetrics(
        string Code,
        string Name,
        int InvoicesAnalyzed,
        int PaidInvoices,
        int OverdueInvoices,
        decimal PaidRatio,
        decimal AvgDaysOverdue,
        decimal CurrentOverdue,
        int PaymentDisciplineScore);
}
