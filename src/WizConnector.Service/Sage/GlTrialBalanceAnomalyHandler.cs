using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-GL-TB-ANOMALY-001 — accounts with large single-month movement vs trailing average.</summary>
internal static class GlTrialBalanceAnomalyHandler
{
    public const string QuerySerial = "SAGE-GL-TB-ANOMALY-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var top = GlSqlHelper.ParseTop(parameters, 15);
        var sql = $"""
            WITH Monthly AS (
                SELECT A.Account, A.Description,
                    YEAR(PG.TxDate) AS Yr, MONTH(PG.TxDate) AS Mo,
                    SUM(ABS({GlSqlHelper.NetValueExpr})) AS Movement
                FROM PostGL PG
                INNER JOIN Accounts A ON PG.AccountLink = A.AccountLink
                WHERE PG.TxDate >= DATEADD(MONTH, -6, GETDATE())
                GROUP BY A.Account, A.Description, YEAR(PG.TxDate), MONTH(PG.TxDate)
            ),
            Latest AS (
                SELECT Account, Description, Movement FROM Monthly M
                WHERE Yr = YEAR(GETDATE()) AND Mo = MONTH(GETDATE())
            ),
            AvgPrior AS (
                SELECT Account, AVG(Movement) AS AvgMove FROM Monthly
                WHERE NOT (Yr = YEAR(GETDATE()) AND Mo = MONTH(GETDATE()))
                GROUP BY Account
            )
            SELECT TOP (@pTop)
                L.Account AS AccountCode, L.Description AS AccountName,
                L.Movement AS CurrentMonthMovement,
                ISNULL(A.AvgMove, 0) AS AvgPriorMovement
            FROM Latest L
            LEFT JOIN AvgPrior A ON A.Account = L.Account
            WHERE L.Movement > ISNULL(A.AvgMove, 0) * 2 AND L.Movement > 1000
            ORDER BY L.Movement DESC;
            """;

        var rows = GlSqlHelper.ExecuteQuery(connectionString, sql, cmd => cmd.Parameters.AddWithValue("@pTop", top));

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            accounts = GlExpenseTopHandler.MapRanked(rows, "CurrentMonthMovement"),
            note = "Trial balance movement anomalies (current month vs 6-month average).",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
