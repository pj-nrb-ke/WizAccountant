using System.Text.Json;
using WizAccountant.Api.Insight;

namespace WizAccountant.Insight.Intents.Tests;

public class AmbiguousIntentTests
{
    public static IEnumerable<object[]> AmbiguousCases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ambiguous-intents.json");
        var json = File.ReadAllText(path);
        var cases = JsonSerializer.Deserialize<List<AmbiguousIntentCase>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

        return cases.Select(c => new object[] { c });
    }

    [Theory]
    [MemberData(nameof(AmbiguousCases))]
    public void Classify_primary_secondary_domain(AmbiguousIntentCase c)
    {
        var result = SageIntentEngine.Classify(c.Query);

        Assert.Equal(
            Enum.Parse<SageIntentEngine.IntentType>(c.ExpectedIntent, ignoreCase: true),
            result.PrimaryIntent);

        Assert.True(
            result.Confidence >= c.MinConfidence,
            $"[{c.Id}] confidence {result.Confidence:F2} < {c.MinConfidence}: {c.Query}");

        if (!string.IsNullOrEmpty(c.ExpectedDomain))
        {
            Assert.Equal(
                Enum.Parse<SageChatDomain.Layer>(c.ExpectedDomain, ignoreCase: true),
                result.Domain);
        }

        if (!string.IsNullOrEmpty(c.ExpectedSecondaryIntent))
        {
            Assert.Equal(
                Enum.Parse<SageIntentEngine.IntentType>(c.ExpectedSecondaryIntent, ignoreCase: true),
                result.SecondaryIntent);
        }
    }

    [Theory]
    [MemberData(nameof(AmbiguousCases))]
    public void Resolve_returns_handler_or_mega_digest(AmbiguousIntentCase c)
    {
        var resolution = SageIntentResolver.Resolve(c.Query);

        Assert.NotNull(resolution.Classification);
        Assert.Equal(
            Enum.Parse<SageIntentEngine.IntentType>(c.ExpectedIntent, ignoreCase: true),
            resolution.Classification.PrimaryIntent);

        Assert.False(string.IsNullOrWhiteSpace(resolution.Summary));

        if (!string.IsNullOrEmpty(c.PreferredOperation))
        {
            Assert.Equal(SageIntentResolver.RouteKind.ImplementedHandler, resolution.Route);
            Assert.Equal(c.PreferredOperation, resolution.Operation);
            Assert.False(string.IsNullOrWhiteSpace(resolution.HandlerId));
        }
        else if (!string.IsNullOrEmpty(c.RouteKind))
        {
            var expected = Enum.Parse<SageIntentResolver.RouteKind>(c.RouteKind, ignoreCase: true);
            Assert.Equal(expected, resolution.Route);
        }
        else
        {
            Assert.True(
                resolution.Route is SageIntentResolver.RouteKind.ImplementedHandler
                    or SageIntentResolver.RouteKind.MegaDigestFallback,
                $"[{c.Id}] expected handler or mega digest, got {resolution.Route}");
        }
    }

    [Theory]
    [MemberData(nameof(AmbiguousCases))]
    public void Resolution_includes_domain_signals(AmbiguousIntentCase c)
    {
        var result = SageIntentEngine.Classify(c.Query);
        if (!string.IsNullOrEmpty(c.ExpectedDomain))
            Assert.NotEmpty(result.DomainSignals);
        Assert.True(result.DomainConfidence >= 0);
    }

    [Fact]
    public void Ambiguous_query_reports_secondary_when_close()
    {
        var result = SageIntentEngine.Classify("How much total outstanding vs which customer owes the most?");
        Assert.True(result.IsAmbiguous || result.SecondaryIntent.HasValue ||
                      result.PrimaryIntent == SageIntentEngine.IntentType.Aggregation ||
                      result.PrimaryIntent == SageIntentEngine.IntentType.Ranking);
    }
}
