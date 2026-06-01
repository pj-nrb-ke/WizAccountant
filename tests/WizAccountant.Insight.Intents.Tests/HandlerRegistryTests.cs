using WizAccountant.Api.Insight;

namespace WizAccountant.Insight.Intents.Tests;

public class HandlerRegistryTests
{
    [Fact]
    public void Registry_loads_implemented_handlers()
    {
        var registry = HandlerRegistry.Instance;
        Assert.False(string.IsNullOrEmpty(registry.Version));
        Assert.True(registry.Handlers.Count >= 8);
        Assert.Contains(registry.Handlers, h => h.Id == "AR-COUNT-DISCOUNTED-INVOICES" && h.Implemented);
    }

    [Fact]
    public void Discount_count_query_maps_to_registry_handler()
    {
        var query = "How many sales invoices in 2026 have discounts?";
        var classification = SageIntentEngine.Classify(query);
        var entry = HandlerRegistry.Instance.FindBest(query, classification);

        Assert.NotNull(entry);
        Assert.Equal("salesinvoice.discount.count", entry!.Operation);
    }
}
