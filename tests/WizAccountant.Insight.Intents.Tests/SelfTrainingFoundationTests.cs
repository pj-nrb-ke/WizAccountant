using WizAccountant.Api.Insight;

namespace WizAccountant.Insight.Intents.Tests;

public class SelfTrainingFoundationTests
{
    [Fact]
    public void Product_monthly_contract_blocks_customer_listing()
    {
        var q = "which product get ordered most per month by quantity and value from Jan 2026";
        var classification = SageIntentEngine.Classify(q);
        var bp = BusinessProcessClassifier.Classify(q);
        var contract = QueryIntentContract.Parse(q, classification, bp);

        Assert.False(CompatibilityGate.IsCompatible(contract, "customer.unpaid.summary", out var reason));
        Assert.Contains("product", reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Product_monthly_contract_suggests_canonical_handler()
    {
        var q = "product-wise monthly quantity and value from January 2026";
        var contract = QueryIntentContract.Parse(q, SageIntentEngine.Classify(q), null);
        Assert.Equal(ProductOrderAnalysisChatMatcher.Operation, CompatibilityGate.SuggestCanonicalOperation(contract));
    }

    [Fact]
    public void Handler_capability_declares_monthly_product_metrics()
    {
        var cap = HandlerCapabilityRegistry.Get(ProductOrderAnalysisChatMatcher.Operation);
        Assert.NotNull(cap);
        Assert.True(cap!.SupportsMonthlyBreakdown);
        Assert.Contains("quantity", cap.SupportsMetrics);
    }
}
