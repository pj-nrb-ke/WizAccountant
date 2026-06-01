using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>Standard reconciliation JSON envelope (SAGE-TRAIN-006).</summary>
internal static class ReconcileEnvelope
{
    public static string Build(
        string querySerial,
        string reconciliationType,
        decimal subledgerTotal,
        decimal glTotal,
        IEnumerable<object>? topContributors,
        string finding,
        bool reconciled,
        object? extra = null)
    {
        var difference = subledgerTotal - glTotal;
        var payload = new Dictionary<string, object?>
        {
            ["querySerial"] = querySerial,
            ["reconciliationType"] = reconciliationType,
            ["subledgerTotal"] = subledgerTotal,
            ["glTotal"] = glTotal,
            ["difference"] = difference,
            ["reconciled"] = reconciled,
            ["matches"] = reconciled,
            ["finding"] = finding,
            ["topContributors"] = topContributors?.ToList() ?? [],
            ["dataAsOfUtc"] = DateTimeOffset.UtcNow
        };
        if (extra is not null)
            payload["detail"] = extra;
        return JsonSerializer.Serialize(payload);
    }
}
