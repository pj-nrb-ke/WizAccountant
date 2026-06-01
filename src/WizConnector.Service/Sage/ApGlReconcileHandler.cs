namespace WizConnector.Service.Sage;

internal static class ApGlReconcileHandler
{
    public const string QuerySerial = "SAGE-AP-GL-RECON-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var asOf = ReconcileSqlHelper.ParseAsOf(parameters);
        var top = GlSqlHelper.ParseTop(parameters, 10);
        var (apTotal, _, contributors, _) = ApSubledgerHelper.SumOpenApWithContributors(top);
        var glCreditors = ReconcileSqlHelper.SumControlBalance(connectionString, ReconcileSqlHelper.CreditorsFilter, asOf);
        var variance = apTotal - glCreditors;
        var reconciled = Math.Abs(variance) < 1m;

        var topList = contributors.Select((c, i) => (object)new
        {
            rank = i + 1,
            contributorType = "supplier",
            code = c.Code,
            amount = c.Amount,
            lineCount = c.LineCount
        }).ToList();

        return ReconcileEnvelope.Build(
            QuerySerial,
            "AP aging vs creditors control",
            apTotal,
            glCreditors,
            topList,
            reconciled
                ? "AP open-item subledger aligns with creditors control GL (within tolerance)."
                : $"AP variance: open AP {apTotal:N2} vs creditors GL {glCreditors:N2} (diff {variance:N2}).",
            reconciled,
            new { asOfDate = asOf.ToString("yyyy-MM-dd"), note = "Subledger: SupplierTransaction open lines. GL: PostGL on payable/creditor control accounts." });
    }
}
