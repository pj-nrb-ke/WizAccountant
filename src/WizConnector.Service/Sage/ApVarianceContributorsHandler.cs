namespace WizConnector.Service.Sage;

internal static class ApVarianceContributorsHandler
{
    public const string QuerySerial = "SAGE-AP-VAR-CONT-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var top = GlSqlHelper.ParseTop(parameters, 15);
        var (apTotal, _, contributors, _) = ApSubledgerHelper.SumOpenApWithContributors(top);
        var asOf = ReconcileSqlHelper.ParseAsOf(parameters);
        var glCreditors = ReconcileSqlHelper.SumControlBalance(connectionString, ReconcileSqlHelper.CreditorsFilter, asOf);

        var topList = contributors.Select((c, i) => (object)new
        {
            rank = i + 1,
            supplier = c.Code,
            openBalance = c.Amount,
            lineCount = c.LineCount,
            shareOfSubledger = apTotal > 0 ? Math.Round(c.Amount / apTotal * 100, 2) : 0m
        }).ToList();

        return ReconcileEnvelope.Build(
            QuerySerial,
            "AP mismatch contributors",
            apTotal,
            glCreditors,
            topList,
            $"Top {topList.Count} suppliers by open AP balance (subledger).",
            Math.Abs(apTotal - glCreditors) < 1m);
    }
}
