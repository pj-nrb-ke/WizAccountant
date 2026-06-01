namespace WizConnector.Service.Sage;

internal static class FaVarianceContributorsHandler
{
    public const string QuerySerial = "SAGE-FA-VAR-CONT-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var top = GlSqlHelper.ParseTop(parameters, 15);
        var (from, to) = GlSqlHelper.ParseDateRange(parameters);

        var sql = """
            SELECT TOP (@pTop)
                A.cAssetCode AS AssetCode,
                A.cAssetDescription AS AssetDescription,
                ISNULL(SUM(ISNULL(B.fAmount, 0)), 0) AS BatchDepreciation
            FROM _btblFAGLBatchAssetValues B
            INNER JOIN _btblFAAsset A ON B.iAssetID = A.idAssetNo
            WHERE CAST(B.dDepreciationDate AS DATE) >= @pDateFrom
              AND CAST(B.dDepreciationDate AS DATE) <= @pDateTo
            GROUP BY A.cAssetCode, A.cAssetDescription
            ORDER BY ABS(ISNULL(SUM(ISNULL(B.fAmount, 0)), 0)) DESC;
            """;

        List<Dictionary<string, object?>> rows;
        try
        {
            rows = GlSqlHelper.ExecuteQuery(connectionString, sql, cmd =>
            {
                cmd.Parameters.AddWithValue("@pTop", top);
                InvNumSqlHelper.AddDateParameters(cmd, from, to);
            });
        }
        catch
        {
            rows = [];
        }

        var faTotal = rows.Sum(r => GlSqlHelper.ToDecimal(r, "BatchDepreciation"));
        var glSql = $"""
            SELECT ISNULL(SUM(ISNULL(PG.Debit, 0)), 0)
            FROM PostGL PG
            {ReconcileSqlHelper.AccountJoin}
            WHERE CAST(PG.TxDate AS DATE) >= @pDateFrom AND CAST(PG.TxDate AS DATE) <= @pDateTo
              AND {ReconcileSqlHelper.DepreciationExpenseFilter};
            """;
        var glDep = VatSqlHelper.RunScalar(connectionString, glSql, from, to);

        var topList = rows.Select((r, i) => (object)new
        {
            rank = i + 1,
            assetCode = r["AssetCode"]?.ToString(),
            description = r["AssetDescription"]?.ToString(),
            batchDepreciation = GlSqlHelper.ToDecimal(r, "BatchDepreciation")
        }).ToList();

        return ReconcileEnvelope.Build(
            QuerySerial,
            "FA asset depreciation contributors",
            faTotal,
            glDep,
            topList,
            $"Top {topList.Count} assets by FA batch depreciation in period.",
            Math.Abs(faTotal - glDep) < 1m);
    }
}
