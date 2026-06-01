namespace WizConnector.Service.Sage;

internal static class ArGlReconcileHandler
{
    public const string QuerySerial = "SAGE-AR-GL-RECON-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var asOf = ReconcileSqlHelper.ParseAsOf(parameters);
        var top = GlSqlHelper.ParseTop(parameters, 10);
        var (arTotal, _, contributors, _) = ArSubledgerHelper.SumOpenArWithContributors(top);
        var glDebtors = ReconcileSqlHelper.SumControlBalance(connectionString, ReconcileSqlHelper.DebtorsFilter, asOf);
        var variance = arTotal - glDebtors;
        var reconciled = Math.Abs(variance) < 1m;

        var topList = contributors.Select((c, i) => (object)new
        {
            rank = i + 1,
            contributorType = "customer",
            code = c.Code,
            amount = c.Amount,
            lineCount = c.LineCount
        }).ToList();

        return ReconcileEnvelope.Build(
            QuerySerial,
            "AR aging vs debtors control",
            arTotal,
            glDebtors,
            topList,
            reconciled
                ? "AR open-item subledger aligns with debtors control GL (within tolerance)."
                : $"AR variance: open AR {arTotal:N2} vs debtors GL {glDebtors:N2} (diff {variance:N2}).",
            reconciled,
            new { asOfDate = asOf.ToString("yyyy-MM-dd"), note = "Subledger: CustomerTransaction open invoice/order lines. GL: PostGL on receivable/debtor control accounts." });
    }
}
