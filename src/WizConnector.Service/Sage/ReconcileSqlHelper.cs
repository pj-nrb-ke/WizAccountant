using System.Data.SqlClient;

namespace WizConnector.Service.Sage;

/// <summary>Shared GL control-account SQL for subledger vs GL reconciliation (SAGE-TRAIN-006).</summary>
internal static class ReconcileSqlHelper
{
    public const string AccountJoin = """
        INNER JOIN Accounts A ON PG.AccountLink = A.AccountLink
        LEFT JOIN _etblGLAccountTypes AT ON A.iAccountType = AT.idGLAccountType
        """;

    public const string DebtorsFilter = """
        (
            LOWER(ISNULL(AT.cDescription, '')) LIKE '%receivable%'
            OR LOWER(ISNULL(AT.cDescription, '')) LIKE '%debtor%'
            OR LOWER(ISNULL(A.Description, '')) LIKE '%debtor%'
            OR LOWER(ISNULL(A.Description, '')) LIKE '%receivable%'
        )
        AND LOWER(ISNULL(AT.cDescription, '')) NOT LIKE '%creditor%'
        AND LOWER(ISNULL(A.Description, '')) NOT LIKE '%creditor%'
        """;

    public const string CreditorsFilter = """
        (
            LOWER(ISNULL(AT.cDescription, '')) LIKE '%payable%'
            OR LOWER(ISNULL(AT.cDescription, '')) LIKE '%creditor%'
            OR LOWER(ISNULL(A.Description, '')) LIKE '%creditor%'
            OR LOWER(ISNULL(A.Description, '')) LIKE '%payable%'
        )
        AND LOWER(ISNULL(AT.cDescription, '')) NOT LIKE '%debtor%'
        AND LOWER(ISNULL(A.Description, '')) NOT LIKE '%receivable%'
        """;

    public const string DepreciationExpenseFilter = """
        (
            LOWER(ISNULL(AT.cDescription, '')) LIKE '%depreciation%'
            OR LOWER(ISNULL(A.Description, '')) LIKE '%depreciation%'
        )
        AND LOWER(ISNULL(AT.cDescription, '')) NOT LIKE '%accumulated%'
        AND LOWER(ISNULL(A.Description, '')) NOT LIKE '%accumulated%'
        """;

    public static DateTime ParseAsOf(Dictionary<string, string> parameters) =>
        parameters.TryGetValue("asOfDate", out var raw) && DateTime.TryParse(raw, out var d)
            ? d.Date
            : DateTime.Today;

    public static decimal SumControlBalance(string connectionString, string accountFilter, DateTime asOf)
    {
        var sql = $"""
            SELECT ISNULL(SUM({GlSqlHelper.NetValueExpr}), 0)
            FROM PostGL PG
            {AccountJoin}
            WHERE CAST(PG.TxDate AS DATE) <= @pAsOf
              AND {accountFilter};
            """;
        using var conn = new SqlConnection(connectionString);
        conn.Open();
        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 180 };
        cmd.Parameters.AddWithValue("@pAsOf", asOf);
        return Convert.ToDecimal(cmd.ExecuteScalar() ?? 0m);
    }

    public static List<Dictionary<string, object?>> TopAccountsByBalance(
        string connectionString,
        string accountFilter,
        DateTime asOf,
        int top)
    {
        var sql = $"""
            SELECT TOP (@pTop)
                A.Account AS AccountCode,
                A.Description AS AccountName,
                ISNULL(SUM({GlSqlHelper.NetValueExpr}), 0) AS Balance
            FROM PostGL PG
            {AccountJoin}
            WHERE CAST(PG.TxDate AS DATE) <= @pAsOf
              AND {accountFilter}
            GROUP BY A.Account, A.Description
            ORDER BY ABS(ISNULL(SUM({GlSqlHelper.NetValueExpr}), 0)) DESC;
            """;
        return GlSqlHelper.ExecuteQuery(connectionString, sql, cmd =>
        {
            cmd.Parameters.AddWithValue("@pTop", top);
            cmd.Parameters.AddWithValue("@pAsOf", asOf);
        });
    }
}
