using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class TreasuryDashboardHandler
{
    public const string QuerySerial = "SAGE-TREASURY-DASH-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var (ar, arLines) = TreasuryForecastHelper.SumOpenAr();
        var (ap, apLines) = TreasuryForecastHelper.SumOpenAp();
        var bank = TreasuryForecastHelper.SumBankBalance(connectionString);
        var horizon = GlSqlHelper.ParseHorizonDays(parameters, 30);
        var inflows = ar * 0.65m;
        var outflows = ap * 0.55m;
        var projected = bank + inflows - outflows;
        var liquidityRatio = ap > 0 ? bank / ap : 0m;

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
            countOnly = false,
            aggregationMode = true,
            finding = $"Treasury: bank {bank:N2}, AR open {ar:N2}, AP open {ap:N2}, projected {projected:N2}, liquidity {liquidityRatio:N2}.",
            note = "Treasury summary — not generic dashboard.summary KPIs.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
