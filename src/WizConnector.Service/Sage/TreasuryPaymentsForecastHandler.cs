using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class TreasuryPaymentsForecastHandler
{
    public const string QuerySerial = "SAGE-TREASURY-PAY-001";

    public static string Execute(Dictionary<string, string> parameters)
    {
        var horizon = GlSqlHelper.ParseHorizonDays(parameters, 30);
        var (ap, lines) = TreasuryForecastHelper.SumOpenAp();
        var expected = ap * 0.55m;
        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            forecastHorizonDays = horizon,
            openApOutstanding = ap,
            expectedPayments = expected,
            openApLines = lines,
            finding = $"Expected supplier payments ({horizon}d horizon proxy): {expected:N2} from open AP {ap:N2}.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
