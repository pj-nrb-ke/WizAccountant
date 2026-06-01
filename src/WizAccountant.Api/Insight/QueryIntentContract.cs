namespace WizAccountant.Api.Insight;

/// <summary>Parsed business intent structure for routing, capability checks, and query logging (Layer 2).</summary>
internal sealed class QueryIntentContract
{
    public string RawQuery { get; init; } = "";
    public string? Domain { get; init; }
    public string? BusinessProcess { get; init; }
    public string? PrimaryIntent { get; init; }
    public IReadOnlyList<string> Groupings { get; init; } = [];
    public IReadOnlyList<string> Metrics { get; init; } = [];
    public string? DateFilter { get; init; }
    public IReadOnlyList<string> OutputShape { get; init; } = [];
    public bool WantsRanking { get; init; }
    public bool WantsAggregation { get; init; }

    public static QueryIntentContract Parse(
        string message,
        SageIntentEngine.Classification classification,
        BusinessProcessClassifier.Classification? businessProcess = null)
    {
        var m = message.ToLowerInvariant();
        var groupings = new List<string>();
        var metrics = new List<string>();
        var output = new List<string>();

        if (m.Contains("product") || m.Contains("item") || m.Contains("stock item"))
            groupings.Add("product");
        if (m.Contains("customer") && !m.Contains("product"))
            groupings.Add("customer");
        if (m.Contains("supplier"))
            groupings.Add("supplier");
        if (m.Contains("warehouse"))
            groupings.Add("warehouse");
        if (m.Contains("month") || m.Contains("per month") || m.Contains("monthly"))
            groupings.Add("month");

        if (m.Contains("quantity") || m.Contains("qty"))
            metrics.Add("quantity");
        if (m.Contains("value") || m.Contains("amount"))
            metrics.Add("value");
        if (m.Contains("vat") || m.Contains("tax"))
            metrics.Add("vat");
        if (m.Contains("balance") || m.Contains("outstanding"))
            metrics.Add("balance");

        if (groupings.Count > 0 || metrics.Count > 0)
            output.Add("tabular");
        if (m.Contains("why") || m.Contains("explain"))
            output.Add("explainability");
        if (QueryAggregationMode.IsAggregationQuery(m))
            output.Add("aggregation");

        string? dateFilter = null;
        if (m.Contains("from jan") || m.Contains("starting from jan") || m.Contains("january 20"))
            dateFilter = "from-month-year";
        else if (ChatIntentMatcher.ExtractYearFromMessage(message).HasValue)
            dateFilter = "year-range";

        return new QueryIntentContract
        {
            RawQuery = message,
            Domain = classification.Domain.ToString(),
            BusinessProcess = businessProcess?.Process.ToString(),
            PrimaryIntent = classification.PrimaryIntent.ToString(),
            Groupings = groupings,
            Metrics = metrics,
            DateFilter = dateFilter,
            OutputShape = output,
            WantsRanking = classification.PrimaryIntent == SageIntentEngine.IntentType.Ranking ||
                           m.Contains("top") || m.Contains("most"),
            WantsAggregation = QueryAggregationMode.IsAggregationQuery(m)
        };
    }
}
