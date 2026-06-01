using System.Text.Json;
using WizAccountant.Api.Insight;

namespace WizAccountant.Insight.Intents.Tests;

public class ApInvWhHandlerTests
{
    public static IEnumerable<object[]> Cases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ap-inv-wh-handlers.json");
        var json = File.ReadAllText(path);
        var cases = JsonSerializer.Deserialize<List<ArSalesHandlerCase>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        return cases.Select(c => new object[] { c });
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Handler_is_allowlisted(ArSalesHandlerCase c)
    {
        if (string.IsNullOrEmpty(c.Operation))
            return;
        Assert.True(InsightReadOnlyTools.IsAllowed(c.Operation), $"[{c.Id}] {c.Operation}");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Registry_contains_handler(ArSalesHandlerCase c)
    {
        if (string.IsNullOrEmpty(c.Operation))
            return;
        var entry = HandlerRegistry.Instance.GetByOperation(c.Operation);
        Assert.NotNull(entry);
        Assert.True(entry!.Implemented, $"[{c.Id}] {c.Operation}");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void PlanToolCall_routes_correctly(ArSalesHandlerCase c)
    {
        if (string.IsNullOrEmpty(c.Operation))
            return;

        var classification = SageIntentEngine.Classify(c.Query);
        var (op, parameters, _) = InvokePlanToolCall(c.Query, classification);
        Assert.Equal(c.Operation, op);

        if (c.MaxRows.HasValue && parameters.TryGetValue("top", out var topStr))
            Assert.True(int.Parse(topStr) <= c.MaxRows.Value, $"[{c.Id}] top too large");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Count_queries_suppress_bulk_list(ArSalesHandlerCase c)
    {
        if (!c.CountOnly)
            return;
        Assert.True(QueryAggregationMode.ShouldSuppressGrid(c.Query, "supplier.list", null));
        Assert.True(QueryAggregationMode.RejectMisroutedListing(c.Query, "supplier.list"));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Must_not_misroute(ArSalesHandlerCase c)
    {
        foreach (var blocked in c.MustNotRoute ?? Array.Empty<string>())
        {
            var classification = SageIntentEngine.Classify(c.Query);
            var (op, _, _) = InvokePlanToolCall(c.Query, classification);
            Assert.NotEqual(blocked, op);
        }
    }

    [Fact]
    public void All_twenty_four_new_operations_registered()
    {
        var ops = new[]
        {
            "supplier.credit.balances", "ap.invoice.overdue.count", "supplier.invoice.unpaid.olderthan",
            "supplier.outstanding.top", "purchaseinvoice.partially.paid", "purchaseinvoice.duplicate",
            "supplier.payments.top", "purchaseinvoice.count", "purchaseinvoice.top",
            "purchaseinvoice.discount.count", "purchaseinvoice.discount.top", "supplier.purchases.top",
            "inventory.slow.moving.top", "inventory.nonmoving", "inventory.negative.qty",
            "inventory.negative.valuation", "inventory.below.reorder", "inventory.overstocked",
            "inventory.value.top", "inventory.movement.top", "warehouse.value.summary",
            "warehouse.negative.qty", "warehouse.nonmoving", "warehouse.transfer.summary", "warehouse.discrepancy"
        };
        foreach (var op in ops)
        {
            Assert.True(InsightReadOnlyTools.IsAllowed(op));
            Assert.NotNull(HandlerRegistry.Instance.GetByOperation(op));
        }
    }

    private static (string? operation, Dictionary<string, string> parameters, List<string> tools) InvokePlanToolCall(
        string message,
        SageIntentEngine.Classification classification)
    {
        var m = message.ToLowerInvariant();
        var tools = new List<string>();
        var parameters = new Dictionary<string, string> { ["top"] = "500" };
        RankingQueryPolicy.ApplyRowLimits(message, classification, parameters);

        if (ChatIntentMatcher.TryInventoryBsNegativeLedgers(m, parameters, tools, out var negGl))
            return (negGl, parameters, tools);

        if (ApPurchaseInvChatMatcher.TryRoute(message, m, parameters, tools, out var apOp))
            return (apOp, parameters, tools);

        if (InvWarehouseChatMatcher.TryRoute(message, m, parameters, tools, out var invOp))
            return (invOp, parameters, tools);

        if (ArSalesChatMatcher.TryRoute(message, m, parameters, tools, out var arOp))
            return (arOp, parameters, tools);

        return (null, parameters, tools);
    }
}
