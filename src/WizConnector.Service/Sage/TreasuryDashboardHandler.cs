using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class TreasuryDashboardHandler
{
    public const string QuerySerial = "SAGE-TREASURY-DASH-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var (ar, arLines, arTop) = TreasuryForecastHelper.SumOpenArWithTop();
        var (ap, apLines, apTop) = TreasuryForecastHelper.SumOpenApWithTop();
        var bank = TreasuryForecastHelper.SumBankBalance(connectionString);
        var horizon = GlSqlHelper.ParseHorizonDays(parameters, 30);
        var inflows = ar * 0.65m;
        var outflows = ap * 0.55m;
        var projected = bank + inflows - outflows;
        var liquidityRatio = ap > 0 ? bank / ap : 0m;

        var likelyCause = ar > bank * 2m
            ? "Collections lagging — AR outstanding exceeds 2× bank balance"
            : ap > bank
                ? "Payables pressure — AP outstanding exceeds bank balance"
                : projected < 0m
                    ? "Projected cash shortfall within forecast horizon"
                    : "Cash position appears stable";

        var arContributors = arTop.Select((c, i) => (object)new
        {
            rank = i + 1,
            account = c.Account,
            type = "inflow_blocker",
            outstanding = c.Outstanding,
            description = $"Customer {c.Account} — AR {c.Outstanding:N2} outstanding"
        }).ToList();

        var apContributors = apTop.Select((c, i) => (object)new
        {
            rank = i + 1,
            account = c.Account,
            type = "outflow_pressure",
            outstanding = c.Outstanding,
            description = $"Supplier {c.Account} — AP {c.Outstanding:N2} outstanding"
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            cashPosition = bank,
            expectedInflows = inflows,
            expectedOutflows = outflows,
            projectedClosingCash = projected,
            forecastHorizonDays = horizon,
            overdueReceivables = ar,
            overduePayables = ap,
            openArLines = arLines,
            openApLines = apLines,
            liquidityRatio,
            likelyCause,
            topContributors = arContributors.Concat(apContributors).ToList(),
            cashDrivers = new { topArBlockers = arContributors, topApPressure = apContributors },
            countOnly = false,
            aggregationMode = true,
            finding = $"Treasury: bank {bank:N2}, AR open {ar:N2}, AP open {ap:N2}, projected {projected:N2}, liquidity {liquidityRatio:N2}. {likelyCause}.",
            note = "Treasury summary — not generic dashboard.summary KPIs.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
