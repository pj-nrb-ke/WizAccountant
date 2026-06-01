using WizAccountant.Api.Insight;

namespace WizAccountant.Insight.Intents.Tests;

public class MegaDigestMatcherTests
{
    [Fact]
    public void Fallback_returns_recognized_intent_not_empty()
    {
        var query = "Customers with invoices overdue more than 180 days";
        var classification = SageIntentEngine.Classify(query);

        var ok = MegaDigestFallbackMatcher.TryBuildReply(query, classification, out var reply, out var citations);

        Assert.True(ok);
        Assert.Contains("Recognized", reply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Try: \"show dashboard\"", reply);
        Assert.NotEmpty(citations);
    }

    [Fact]
    public void Intent_only_fallback_when_domain_unknown_phrase()
    {
        var query = "How many credit notes were issued last month?";
        var classification = SageIntentEngine.Classify(query);

        var ok = MegaDigestFallbackMatcher.TryBuildReply(query, classification, out var reply, out _);

        Assert.True(ok);
        Assert.Contains("Aggregation", reply, StringComparison.OrdinalIgnoreCase);
    }
}
