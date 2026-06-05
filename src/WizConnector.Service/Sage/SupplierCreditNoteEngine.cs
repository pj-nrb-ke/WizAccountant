using WizAccountant.Contracts;

namespace WizConnector.Service.Sage;

/// <summary>Supplier credit notes from InvNum RTS DocType 3 (SAGE-DISCOVERY-001).</summary>
internal static class SupplierCreditNoteEngine
{
    public const string QuerySerial = "SAGE-AP-SUPPLIER-CREDIT-NOTE-001";

    private static readonly string CountSql = $"""
        SELECT
            COUNT(DISTINCT H.AutoIndex) AS CreditNoteCount,
            SUM(ISNULL(H.InvTotIncl, 0)) AS TotalValue
        FROM InvNum H
        WHERE CAST(H.InvDate AS DATE) >= @pDateFrom
          AND CAST(H.InvDate AS DATE) <= @pDateTo
          AND {InvNumSqlHelper.DocStateAnalyticsExclusionFilter}
          AND {InvNumSqlHelper.SupplierRtsDocTypeFilter}
        """;

    private static readonly string ListSql = $"""
        SELECT TOP (@pTop)
            H.AutoIndex,
            H.InvNumber,
            H.InvDate,
            H.InvTotIncl,
            H.DocState,
            V.Account AS SupplierCode,
            ISNULL(V.Name, V.Account) AS SupplierName
        FROM InvNum H
        LEFT JOIN Vendor V ON V.DCLink = H.AccountID
        WHERE CAST(H.InvDate AS DATE) >= @pDateFrom
          AND CAST(H.InvDate AS DATE) <= @pDateTo
          AND {InvNumSqlHelper.DocStateAnalyticsExclusionFilter}
          AND {InvNumSqlHelper.SupplierRtsDocTypeFilter}
        ORDER BY H.InvDate DESC, H.InvTotIncl DESC
        """;

    private static readonly string TopSupplierSql = $"""
        SELECT TOP (@pTop)
            V.Account AS SupplierCode,
            ISNULL(V.Name, V.Account) AS SupplierName,
            COUNT(DISTINCT H.AutoIndex) AS CreditNoteCount,
            SUM(ISNULL(H.InvTotIncl, 0)) AS TotalValue
        FROM InvNum H
        INNER JOIN Vendor V ON V.DCLink = H.AccountID
        WHERE CAST(H.InvDate AS DATE) >= @pDateFrom
          AND CAST(H.InvDate AS DATE) <= @pDateTo
          AND {InvNumSqlHelper.DocStateAnalyticsExclusionFilter}
          AND {InvNumSqlHelper.SupplierRtsDocTypeFilter}
        GROUP BY V.Account, V.Name
        ORDER BY SUM(ISNULL(H.InvTotIncl, 0)) DESC
        """;

    private static readonly string MonthlySql = $"""
        SELECT
            YEAR(H.InvDate) AS TxYear,
            MONTH(H.InvDate) AS MonthNo,
            DATENAME(MONTH, H.InvDate) AS MonthName,
            COUNT(DISTINCT H.AutoIndex) AS CreditNoteCount,
            SUM(ISNULL(H.InvTotIncl, 0)) AS TotalValue
        FROM InvNum H
        WHERE CAST(H.InvDate AS DATE) >= @pDateFrom
          AND CAST(H.InvDate AS DATE) <= @pDateTo
          AND {InvNumSqlHelper.DocStateAnalyticsExclusionFilter}
          AND {InvNumSqlHelper.SupplierRtsDocTypeFilter}
        GROUP BY YEAR(H.InvDate), MONTH(H.InvDate), DATENAME(MONTH, H.InvDate)
        ORDER BY TxYear, MonthNo
        """;

    public sealed record LoadResult(
        InsightPeriodResolution Period,
        int Count,
        decimal TotalValue,
        IReadOnlyList<Dictionary<string, object?>> Documents,
        IReadOnlyList<Dictionary<string, object?>> TopSuppliers,
        IReadOnlyList<Dictionary<string, object?>> Monthly);

    public static LoadResult Load(
        string connectionString,
        Dictionary<string, string> parameters,
        bool includeList,
        bool includeTopSuppliers,
        bool includeMonthly,
        int listTop,
        int supplierTop)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Sage company database connection is not configured.");

        var period = InsightDateRangeParser.ResolvePeriod(parameters);
        var (from, to) = (period.DateFrom, period.DateTo);

        var countRows = GlSqlHelper.ExecuteQuery(connectionString, CountSql, cmd =>
            InvNumSqlHelper.AddDateParameters(cmd, from, to));
        var count = countRows.Count > 0 ? Convert.ToInt32(GlSqlHelper.ToDecimal(countRows[0], "CreditNoteCount")) : 0;
        var total = countRows.Count > 0 ? GlSqlHelper.ToDecimal(countRows[0], "TotalValue") : 0m;

        var documents = includeList
            ? GlSqlHelper.ExecuteQuery(connectionString, ListSql, cmd =>
            {
                cmd.Parameters.AddWithValue("@pTop", listTop);
                InvNumSqlHelper.AddDateParameters(cmd, from, to);
            })
            : [];

        var topSuppliers = includeTopSuppliers
            ? GlSqlHelper.ExecuteQuery(connectionString, TopSupplierSql, cmd =>
            {
                cmd.Parameters.AddWithValue("@pTop", supplierTop);
                InvNumSqlHelper.AddDateParameters(cmd, from, to);
            })
            : [];

        var monthly = includeMonthly
            ? GlSqlHelper.ExecuteQuery(connectionString, MonthlySql, cmd =>
                InvNumSqlHelper.AddDateParameters(cmd, from, to))
            : [];

        return new LoadResult(period, count, total, documents, topSuppliers, monthly);
    }
}
