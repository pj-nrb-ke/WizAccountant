using System.Data.SqlClient;

namespace WizConnector.Service.Sage;

internal static class CustomerPaymentBehaviorEngine
{
    public const string QuerySerial = "SAGE-AR-PAYMENT-BEHAVIOR-001";

    private static readonly string MetricsSql = $"""
        WITH InvoiceBase AS (
            SELECT
                C.Account AS CustomerCode,
                ISNULL(C.Name, C.Account) AS CustomerName,
                H.AutoIndex AS InvKey,
                CAST(H.InvDate AS DATE) AS InvoiceDate,
                CAST(COALESCE(H.DueDate, DATEADD(DAY, 30, H.InvDate)) AS DATE) AS DueDate,
                ISNULL(H.InvTotIncl, 0) AS InvoiceAmount,
                ISNULL(H.Outstanding, ISNULL(H.InvTotOutstanding, 0)) AS Outstanding
            FROM InvNum H
            INNER JOIN Client C ON C.DCLink = H.AccountID
            WHERE CAST(H.InvDate AS DATE) >= @pDateFrom
              AND CAST(H.InvDate AS DATE) <= @pDateTo
              AND {InvNumSqlHelper.DocStateAnalyticsExclusionFilter}
              AND {InvNumSqlHelper.SalesDocTypeFilter}
        ),
        Payments AS (
            SELECT P.InvNumKey, MAX(CAST(P.TxDate AS DATE)) AS PaymentDate
            FROM PostAR P
            WHERE ISNULL(P.Credit, 0) > 0
              AND P.InvNumKey IS NOT NULL
              AND P.InvNumKey > 0
            GROUP BY P.InvNumKey
        ),
        Scored AS (
            SELECT
                b.CustomerCode,
                b.CustomerName,
                b.Outstanding,
                CASE WHEN b.Outstanding <= 0.01 AND pay.PaymentDate IS NOT NULL THEN 1 ELSE 0 END AS IsPaid,
                CASE WHEN b.Outstanding <= 0.01 AND pay.PaymentDate IS NOT NULL AND pay.PaymentDate <= b.DueDate THEN 1 ELSE 0 END AS PaidWithinTerms,
                CASE WHEN b.Outstanding <= 0.01 AND pay.PaymentDate IS NOT NULL
                     THEN DATEDIFF(DAY, b.InvoiceDate, pay.PaymentDate) END AS DaysToPay,
                CASE WHEN b.Outstanding <= 0.01 AND pay.PaymentDate IS NOT NULL
                     THEN DATEDIFF(DAY, b.DueDate, pay.PaymentDate) END AS DaysLate
            FROM InvoiceBase b
            LEFT JOIN Payments pay ON pay.InvNumKey = b.InvKey
        )
        SELECT
            CustomerCode,
            CustomerName,
            COUNT(*) AS InvoicesAnalyzed,
            SUM(IsPaid) AS PaidInvoices,
            SUM(CASE WHEN Outstanding > 0.01 THEN Outstanding ELSE 0 END) AS CurrentOverdue,
            AVG(CAST(DaysToPay AS FLOAT)) AS AvgPaymentDays,
            AVG(CAST(DaysLate AS FLOAT)) AS AvgDaysLate,
            CASE WHEN SUM(IsPaid) > 0
                 THEN CAST(SUM(PaidWithinTerms) AS FLOAT) / CAST(SUM(IsPaid) AS FLOAT)
                 ELSE 0 END AS PromptPaymentRatio
        FROM Scored
        GROUP BY CustomerCode, CustomerName
        HAVING COUNT(*) >= @pMinInvoices
        """;

    public static (bool Success, string? Error, List<CustomerPaymentMetrics> Metrics) LoadMetrics(
        string connectionString,
        Dictionary<string, string> parameters,
        string? customerCodeFilter = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return (false, "Sage company database connection is not configured.", []);

        var (from, to) = InvNumSqlHelper.ParseDateRange(parameters, parameters.GetValueOrDefault("message"));
        var minInvoices = Math.Clamp(SageListHelpers.ParseIntParam(parameters, "minInvoices", 3), 1, 20);

        try
        {
            var sql = MetricsSql;
            if (!string.IsNullOrWhiteSpace(customerCodeFilter))
                sql += "\nAND CustomerCode = @pCustomerCode";

            var rows = GlSqlHelper.ExecuteQuery(connectionString, sql, cmd =>
            {
                InvNumSqlHelper.AddDateParameters(cmd, from, to);
                cmd.Parameters.AddWithValue("@pMinInvoices", minInvoices);
                if (!string.IsNullOrWhiteSpace(customerCodeFilter))
                    cmd.Parameters.AddWithValue("@pCustomerCode", customerCodeFilter.Trim());
            });

            var metrics = rows.Select(MapRow).ToList();
            return (true, null, metrics);
        }
        catch (SqlException ex)
        {
            return (false,
                "Payment allocation analysis requires InvNum due dates and PostAR payment dates. " +
                $"SQL error: {ex.Message}",
                []);
        }
    }

    private static CustomerPaymentMetrics MapRow(Dictionary<string, object?> r)
    {
        var ratio = (decimal)GlSqlHelper.ToDecimal(r, "PromptPaymentRatio");
        var avgLate = GlSqlHelper.ToDecimal(r, "AvgDaysLate");
        var avgPay = GlSqlHelper.ToDecimal(r, "AvgPaymentDays");
        var overdue = GlSqlHelper.ToDecimal(r, "CurrentOverdue");
        var paid = (int)GlSqlHelper.ToDecimal(r, "PaidInvoices");
        var analyzed = (int)GlSqlHelper.ToDecimal(r, "InvoicesAnalyzed");

        return new CustomerPaymentMetrics(
            r["CustomerCode"]?.ToString() ?? "",
            r["CustomerName"]?.ToString() ?? "",
            analyzed,
            paid,
            ratio,
            avgPay,
            avgLate,
            overdue,
            ComputeScore(ratio, avgLate, overdue, paid));
    }

    internal static int ComputeScore(decimal promptRatio, decimal avgDaysLate, decimal currentOverdue, int paidInvoices)
    {
        var promptPart = Math.Clamp(promptRatio * 50m, 0m, 50m);
        var latePart = Math.Clamp(20m - Math.Max(0m, avgDaysLate), 0m, 20m);
        var overduePart = currentOverdue <= 0.01m ? 15m : Math.Clamp(15m - (currentOverdue / 100_000m), 0m, 15m);
        var volumePart = Math.Clamp(paidInvoices / 5m * 15m, 0m, 15m);
        return (int)Math.Round(promptPart + latePart + overduePart + volumePart);
    }

    internal sealed record CustomerPaymentMetrics(
        string Code,
        string Name,
        int InvoicesAnalyzed,
        int PaidInvoices,
        decimal PromptPaymentRatio,
        decimal AvgPaymentDays,
        decimal AvgDaysLate,
        decimal CurrentOverdue,
        int PaymentDisciplineScore);
}
