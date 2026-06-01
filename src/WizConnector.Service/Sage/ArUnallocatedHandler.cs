using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class ArUnallocatedHandler
{
    public const string QuerySerial = "SAGE-AR-UNALLOC-001";

    public static string Execute(Dictionary<string, string> parameters)
    {
        var top = GlSqlHelper.ParseTop(parameters, 25);
        var (_, lineCount, _, unallocated) = ArSubledgerHelper.SumOpenArWithContributors(1);

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            reconciliationType = "Unallocated AR transactions",
            unallocatedLineCount = unallocated,
            totalOpenLines = lineCount,
            finding = unallocated > 0
                ? $"{unallocated} open AR line(s) could not be tied to a customer account."
                : "No unallocated AR lines detected in open items.",
            requestedTop = top,
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
