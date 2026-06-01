using System.Text.Json;
using WizAccountant.Api.Insight;

namespace WizAccountant.Insight.Intents.Tests;

public class IntentClassificationTests
{
    public static IEnumerable<object[]> GoldenCases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "golden-intents.json");
        var json = File.ReadAllText(path);
        var cases = JsonSerializer.Deserialize<List<GoldenIntentCase>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

        return cases.Select(c => new object[] { c });
    }

    [Theory]
    [MemberData(nameof(GoldenCases))]
    public void ClassifyIntent_matches_golden_expectations(GoldenIntentCase golden)
    {
        var result = SageIntentEngine.Classify(golden.Query);

        Assert.Equal(
            Enum.Parse<SageIntentEngine.IntentType>(golden.ExpectedIntent, ignoreCase: true),
            result.PrimaryIntent);

        Assert.True(
            result.Confidence >= golden.MinConfidence,
            $"[{golden.Id}] confidence {result.Confidence:F2} < {golden.MinConfidence} for: {golden.Query}");

        if (!string.IsNullOrEmpty(golden.ExpectedResponseShape))
        {
            Assert.Equal(
                Enum.Parse<SageIntentEngine.ResponseShape>(golden.ExpectedResponseShape, ignoreCase: true),
                result.ExpectedResponse);
        }

        if (!string.IsNullOrEmpty(golden.ExpectedDomain))
        {
            Assert.Equal(
                Enum.Parse<SageChatDomain.Layer>(golden.ExpectedDomain, ignoreCase: true),
                result.Domain);
        }
    }

    [Theory]
    [MemberData(nameof(GoldenCases))]
    public void Aggregation_never_allows_forbidden_listing_ops(GoldenIntentCase golden)
    {
        if (!golden.SuppressGrid && golden.ForbiddenOperations is null)
            return;

        if (golden.ForbiddenOperations is null)
            return;

        foreach (var op in golden.ForbiddenOperations)
        {
            var rejected = QueryAggregationMode.RejectMisroutedListing(golden.Query, op);
            if (golden.SuppressGrid || golden.ExpectedIntent.Equals("Aggregation", StringComparison.OrdinalIgnoreCase))
                Assert.True(rejected, $"[{golden.Id}] should block {op}");
        }

        if (golden.SuppressGrid)
        {
            Assert.True(QueryAggregationMode.ShouldSuppressGrid(golden.Query, "customer.openitems", null));
        }
    }

    [Theory]
    [MemberData(nameof(GoldenCases))]
    public void Ranking_respects_row_limits(GoldenIntentCase golden)
    {
        if (!golden.MaxRows.HasValue && golden.ExpectedIntent != "Ranking")
            return;

        var classification = SageIntentEngine.Classify(golden.Query);
        if (classification.PrimaryIntent != SageIntentEngine.IntentType.Ranking)
            return;

        var parameters = new Dictionary<string, string> { ["top"] = "500" };
        RankingQueryPolicy.ApplyRowLimits(golden.Query, classification, parameters);

        Assert.True(parameters.TryGetValue("top", out var topStr));
        var top = int.Parse(topStr);
        Assert.InRange(top, 1, RankingQueryPolicy.MaxTop);

        if (golden.MaxRows.HasValue)
            Assert.True(top <= golden.MaxRows.Value, $"[{golden.Id}] top {top} > expected max {golden.MaxRows}");
    }
}
