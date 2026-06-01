using System.Text.Json;
using WizAccountant.Api.Insight;

namespace WizAccountant.Insight.Intents.Tests;

public class ProductMonthlyOrdersHandlerTests
{
    public static IEnumerable<object[]> Cases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "product-monthly-orders-handlers.json");
        var json = File.ReadAllText(path);
        var cases = JsonSerializer.Deserialize<List<ArSalesHandlerCase>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        return cases.Select(c => new object[] { c });
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Handler_is_allowlisted(ArSalesHandlerCase c)
    {
        Assert.True(InsightReadOnlyTools.IsAllowed(c.Operation!), $"[{c.Id}]");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Plan_routes_to_product_monthly_analysis(ArSalesHandlerCase c)
    {
        var classification = SageIntentEngine.Classify(c.Query);
        var (op, _, _) = ChatRoutePlanner.Plan(c.Query, classification);
        Assert.Equal(c.Operation, op);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Must_not_misroute(ArSalesHandlerCase c)
    {
        foreach (var blocked in c.MustNotRoute ?? Array.Empty<string>())
        {
            var classification = SageIntentEngine.Classify(c.Query);
            var (op, _, _) = ChatRoutePlanner.Plan(c.Query, classification);
            Assert.NotEqual(blocked, op);
        }
    }

    [Fact]
    public void Matcher_recognizes_user_exact_query()
    {
        var q = "which product get ordered most. Give me analysis per product per month by Quantity and Value starting from Jan 2026";
        Assert.True(ProductOrderAnalysisChatMatcher.IsProductMonthlyOrderQuery(q.ToLowerInvariant()));
    }
}
