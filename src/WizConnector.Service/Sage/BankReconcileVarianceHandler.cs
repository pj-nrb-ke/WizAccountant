namespace WizConnector.Service.Sage;

internal static class BankReconcileVarianceHandler
{
    public const string QuerySerial = "SAGE-BANK-RECON-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var (from, to) = GlSqlHelper.ParseDateRange(parameters);
        var bookBalance = TreasuryForecastHelper.SumBankBalance(connectionString);

        var movementSql = $"""
            SELECT ISNULL(SUM({GlSqlHelper.NetValueExpr}), 0)
            FROM PostGL PG
            {GlSqlHelper.BankJoin}
            WHERE CAST(PG.TxDate AS DATE) >= @pDateFrom AND CAST(PG.TxDate AS DATE) <= @pDateTo
              AND {GlSqlHelper.BankFilter};
            """;
        var periodMovement = VatSqlHelper.RunScalar(connectionString, movementSql, from, to);

        var unmatchedSql = $"""
            SELECT ISNULL(SUM(ABS({GlSqlHelper.NetValueExpr})), 0)
            FROM PostGL PG
            {GlSqlHelper.BankJoin}
            WHERE CAST(PG.TxDate AS DATE) >= @pDateFrom AND CAST(PG.TxDate AS DATE) <= @pDateTo
              AND {GlSqlHelper.BankFilter}
              AND (
                LOWER(ISNULL(PG.Description,'')) LIKE '%unalloc%'
                OR LOWER(ISNULL(PG.Description,'')) LIKE '%unmatched%'
                OR LOWER(ISNULL(PG.Reference,'')) LIKE '%unalloc%'
              );
            """;
        var unmatched = VatSqlHelper.RunScalar(connectionString, unmatchedSql, from, to);
        var reconciledState = bookBalance - unmatched;
        var variance = periodMovement - reconciledState;

        return ReconcileEnvelope.Build(
            QuerySerial,
            "Bank book vs reconciliation adjustments",
            bookBalance,
            reconciledState,
            new[]
            {
                (object)new { rank = 1, contributorType = "unmatchedItems", amount = unmatched },
                (object)new { rank = 2, contributorType = "periodMovement", amount = periodMovement }
            },
            Math.Abs(variance) < 1m
                ? "Bank GL book balance aligns with reconciliation adjustment heuristic."
                : $"Bank reconciliation variance {variance:N2} — review unmatched entries and period movement.",
            Math.Abs(variance) < 1m,
            new { dateFrom = from.ToString("yyyy-MM-dd"), dateTo = to.ToString("yyyy-MM-dd"), note = "Book: bank GL balance. Adjusted: book less unmatched-description items in period." });
    }
}
