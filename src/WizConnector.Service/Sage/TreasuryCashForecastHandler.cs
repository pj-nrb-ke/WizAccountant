using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class TreasuryCashForecastHandler
{
    public const string QuerySerial = "SAGE-TREASURY-FORECAST-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var horizon = GlSqlHelper.ParseHorizonDays(parameters, 30);
        var (ar, arLines) = TreasuryForecastHelper.SumOpenAr();
        var (ap, apLines) = TreasuryForecastHelper.SumOpenAp();
        var bank = TreasuryForecastHelper.SumBankBalance(connectionString);

        var collectionRate = 0.65m;
        var paymentRate = 0.55m;
        var expectedInflows = ar * collectionRate;
        var expectedOutflows = ap * paymentRate;
        var projectedClosing = bank + expectedInflows - expectedOutflows;

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            forecastHorizonDays = horizon,
            currentBankBalance = bank,
            openArOutstanding = ar,
            openApOutstanding = ap,
            expectedInflows,
            expectedOutflows,
            projectedClosingCash = projectedClosing,
            collectionAssumption = collectionRate,
            paymentAssumption = paymentRate,
            openArLines = arLines,
            openApLines = apLines,
            finding = $"Forecast {horizon}d: inflows {expectedInflows:N2}, outflows {expectedOutflows:N2}, projected cash {projectedClosing:N2}.",
            note = "Predictive forecast from bank balance + aged AR/AP assumptions — not current balance only.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
