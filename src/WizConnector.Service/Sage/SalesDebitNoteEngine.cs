using System.Data.SqlClient;
using WizAccountant.Contracts;

namespace WizConnector.Service.Sage;

/// <summary>Customer debit notes from PostAR DN TrCode (SAGE-DISCOVERY-001).</summary>
internal static class SalesDebitNoteEngine
{
    public const string QuerySerial = "SAGE-AR-DEBIT-NOTE-001";

    private static readonly string CountSql = $"""
        SELECT
            COUNT(DISTINCT P.AutoIdx) AS DebitNoteCount,
            SUM(ISNULL(P.Debit, 0)) AS TotalValue
        FROM PostAR P
        WHERE CAST(P.TxDate AS DATE) >= @pDateFrom
          AND CAST(P.TxDate AS DATE) <= @pDateTo
          AND ISNULL(P.Debit, 0) > 0
          AND {SageTrCodeSqlHelper.PostArDebitNoteFilter}
        """;

    private static readonly string ListSql = $"""
        SELECT TOP (@pTop)
            P.AutoIdx,
            P.Reference,
            P.Description,
            P.Debit,
            P.TxDate,
            C.Account AS CustomerCode,
            ISNULL(C.Name, C.Account) AS CustomerName
        FROM PostAR P
        LEFT JOIN Client C ON C.DCLink = P.AccountLink
        WHERE CAST(P.TxDate AS DATE) >= @pDateFrom
          AND CAST(P.TxDate AS DATE) <= @pDateTo
          AND ISNULL(P.Debit, 0) > 0
          AND {SageTrCodeSqlHelper.PostArDebitNoteFilter}
        ORDER BY P.TxDate DESC, P.Debit DESC
        """;

    private static readonly string TopCustomerSql = $"""
        SELECT TOP (@pTop)
            C.Account AS CustomerCode,
            ISNULL(C.Name, C.Account) AS CustomerName,
            COUNT(DISTINCT P.AutoIdx) AS DebitNoteCount,
            SUM(ISNULL(P.Debit, 0)) AS TotalValue
        FROM PostAR P
        INNER JOIN Client C ON C.DCLink = P.AccountLink
        WHERE CAST(P.TxDate AS DATE) >= @pDateFrom
          AND CAST(P.TxDate AS DATE) <= @pDateTo
          AND ISNULL(P.Debit, 0) > 0
          AND {SageTrCodeSqlHelper.PostArDebitNoteFilter}
        GROUP BY C.Account, C.Name
        ORDER BY SUM(ISNULL(P.Debit, 0)) DESC
        """;

    private static readonly string MonthlySql = $"""
        SELECT
            YEAR(P.TxDate) AS TxYear,
            MONTH(P.TxDate) AS MonthNo,
            DATENAME(MONTH, P.TxDate) AS MonthName,
            COUNT(DISTINCT P.AutoIdx) AS DebitNoteCount,
            SUM(ISNULL(P.Debit, 0)) AS TotalValue
        FROM PostAR P
        WHERE CAST(P.TxDate AS DATE) >= @pDateFrom
          AND CAST(P.TxDate AS DATE) <= @pDateTo
          AND ISNULL(P.Debit, 0) > 0
          AND {SageTrCodeSqlHelper.PostArDebitNoteFilter}
        GROUP BY YEAR(P.TxDate), MONTH(P.TxDate), DATENAME(MONTH, P.TxDate)
        ORDER BY TxYear, MonthNo
        """;

    public sealed record LoadResult(
        InsightPeriodResolution Period,
        int Count,
        decimal TotalValue,
        IReadOnlyList<Dictionary<string, object?>> Documents,
        IReadOnlyList<Dictionary<string, object?>> TopCustomers,
        IReadOnlyList<Dictionary<string, object?>> Monthly);

    public static LoadResult Load(
        string connectionString,
        Dictionary<string, string> parameters,
        bool includeList,
        bool includeTopCustomers,
        bool includeMonthly,
        int listTop,
        int customerTop)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Sage company database connection is not configured.");

        var period = InsightDateRangeParser.ResolvePeriod(parameters);
        var (from, to) = (period.DateFrom, period.DateTo);

        var countRows = GlSqlHelper.ExecuteQuery(connectionString, CountSql, cmd =>
            InvNumSqlHelper.AddDateParameters(cmd, from, to));
        var count = countRows.Count > 0 ? Convert.ToInt32(GlSqlHelper.ToDecimal(countRows[0], "DebitNoteCount")) : 0;
        var total = countRows.Count > 0 ? GlSqlHelper.ToDecimal(countRows[0], "TotalValue") : 0m;

        var documents = includeList
            ? GlSqlHelper.ExecuteQuery(connectionString, ListSql, cmd =>
            {
                cmd.Parameters.AddWithValue("@pTop", listTop);
                InvNumSqlHelper.AddDateParameters(cmd, from, to);
            })
            : [];

        var topCustomers = includeTopCustomers
            ? GlSqlHelper.ExecuteQuery(connectionString, TopCustomerSql, cmd =>
            {
                cmd.Parameters.AddWithValue("@pTop", customerTop);
                InvNumSqlHelper.AddDateParameters(cmd, from, to);
            })
            : [];

        var monthly = includeMonthly
            ? GlSqlHelper.ExecuteQuery(connectionString, MonthlySql, cmd =>
                InvNumSqlHelper.AddDateParameters(cmd, from, to))
            : [];

        return new LoadResult(period, count, total, documents, topCustomers, monthly);
    }
}
