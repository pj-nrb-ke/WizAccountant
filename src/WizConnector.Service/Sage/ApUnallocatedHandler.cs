using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class ApUnallocatedHandler
{
    public const string QuerySerial = "SAGE-AP-UNALLOC-001";

    public static string Execute(Dictionary<string, string> parameters)
    {
        var top = GlSqlHelper.ParseTop(parameters, 25);
        var (_, lineCount, _, unallocated) = ApSubledgerHelper.SumOpenApWithContributors(1);

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            reconciliationType = "Unallocated AP transactions",
            unallocatedLineCount = unallocated,
            totalOpenLines = lineCount,
            finding = unallocated > 0
                ? $"{unallocated} open AP line(s) could not be tied to a supplier account."
                : "No unallocated AP lines detected in open items.",
            requestedTop = top,
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
