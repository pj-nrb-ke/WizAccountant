using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class TreasuryCollectionsForecastHandler
{
    public const string QuerySerial = "SAGE-TREASURY-COLLECT-001";

    public static string Execute(Dictionary<string, string> parameters)
    {
        var horizon = GlSqlHelper.ParseHorizonDays(parameters, 30);
        var (ar, lines) = TreasuryForecastHelper.SumOpenAr();
        var expected = ar * 0.65m;
        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            forecastHorizonDays = horizon,
            openArOutstanding = ar,
            expectedCollections = expected,
            openArLines = lines,
            finding = $"Expected customer collections ({horizon}d horizon proxy): {expected:N2} from open AR {ar:N2}.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
