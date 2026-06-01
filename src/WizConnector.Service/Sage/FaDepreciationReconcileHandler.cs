namespace WizConnector.Service.Sage;

internal static class FaDepreciationReconcileHandler
{
    public const string QuerySerial = "SAGE-FA-DEP-RECON-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var (from, to) = GlSqlHelper.ParseDateRange(parameters);

        var faSql = """
            SELECT ISNULL(SUM(ISNULL(B.fAmount, 0)), 0)
            FROM _btblFAGLBatchAssetValues B
            INNER JOIN _btblFAAsset A ON B.iAssetID = A.idAssetNo
            WHERE CAST(B.dDepreciationDate AS DATE) >= @pDateFrom
              AND CAST(B.dDepreciationDate AS DATE) <= @pDateTo;
            """;

        decimal faBatch;
        try
        {
            faBatch = VatSqlHelper.RunScalar(connectionString, faSql, from, to);
        }
        catch
        {
            faBatch = 0;
        }

        var glSql = $"""
            SELECT ISNULL(SUM(ISNULL(PG.Debit, 0)), 0)
            FROM PostGL PG
            {ReconcileSqlHelper.AccountJoin}
            WHERE CAST(PG.TxDate AS DATE) >= @pDateFrom AND CAST(PG.TxDate AS DATE) <= @pDateTo
              AND {ReconcileSqlHelper.DepreciationExpenseFilter};
            """;
        var glDep = VatSqlHelper.RunScalar(connectionString, glSql, from, to);
        var variance = faBatch - glDep;
        var reconciled = Math.Abs(variance) < 1m;

        return ReconcileEnvelope.Build(
            QuerySerial,
            "FA depreciation batch vs GL expense",
            faBatch,
            glDep,
            [],
            reconciled
                ? "FA batch depreciation aligns with GL depreciation expense (within tolerance)."
                : $"FA variance: batch {faBatch:N2} vs GL {glDep:N2} (diff {variance:N2}).",
            reconciled,
            new { dateFrom = from.ToString("yyyy-MM-dd"), dateTo = to.ToString("yyyy-MM-dd") });
    }
}
