namespace WizConnector.Service.Sage;

internal static class ArVarianceContributorsHandler
{
    public const string QuerySerial = "SAGE-AR-VAR-CONT-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var top = GlSqlHelper.ParseTop(parameters, 15);
        var (arTotal, _, contributors, _) = ArSubledgerHelper.SumOpenArWithContributors(top);
        var asOf = ReconcileSqlHelper.ParseAsOf(parameters);
        var glDebtors = ReconcileSqlHelper.SumControlBalance(connectionString, ReconcileSqlHelper.DebtorsFilter, asOf);

        var topList = contributors.Select((c, i) => (object)new
        {
            rank = i + 1,
            customer = c.Code,
            openBalance = c.Amount,
            lineCount = c.LineCount,
            shareOfSubledger = arTotal > 0 ? Math.Round(c.Amount / arTotal * 100, 2) : 0m
        }).ToList();

        return ReconcileEnvelope.Build(
            QuerySerial,
            "AR mismatch contributors",
            arTotal,
            glDebtors,
            topList,
            $"Top {topList.Count} customers by open AR balance (subledger).",
            Math.Abs(arTotal - glDebtors) < 1m);
    }
}
