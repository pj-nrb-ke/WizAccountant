using System.Text.Json;
using WizAccountant.Api.Insight;

namespace WizAccountant.Insight.Intents.Tests;

public class ArSalesHandlerTests
{
    public static IEnumerable<object[]> Cases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ar-sales-handlers.json");
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
        Assert.True(InsightReadOnlyTools.IsAllowed(c.Operation), $"[{c.Id}] {c.Operation} not allowlisted");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Registry_contains_handler(ArSalesHandlerCase c)
    {
        if (string.IsNullOrEmpty(c.Operation))
            return;
        var entry = HandlerRegistry.Instance.GetByOperation(c.Operation);
        Assert.NotNull(entry);
        Assert.True(entry!.Implemented, $"[{c.Id}] {c.Operation} not implemented in registry");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void PlanToolCall_routes_to_expected_operation(ArSalesHandlerCase c)
    {
        if (string.IsNullOrEmpty(c.Operation))
            return;

        var classification = SageIntentEngine.Classify(c.Query);
        var (op, parameters, _) = InvokePlanToolCall(c.Query, classification);

        Assert.Equal(c.Operation, op);

        if (c.MaxRows.HasValue && parameters.TryGetValue("top", out var topStr))
        {
            var top = int.Parse(topStr);
            Assert.True(top <= c.MaxRows.Value, $"[{c.Id}] top {top} > {c.MaxRows}");
        }

        if (!string.IsNullOrEmpty(c.Intent))
        {
            Assert.Equal(
                Enum.Parse<SageIntentEngine.IntentType>(c.Intent, ignoreCase: true),
                classification.PrimaryIntent);
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Count_queries_block_open_item_dump(ArSalesHandlerCase c)
    {
        foreach (var forbidden in c.ForbiddenOperations ?? Array.Empty<string>())
        {
            Assert.True(
                QueryAggregationMode.RejectMisroutedListing(c.Query, forbidden),
                $"[{c.Id}] should block {forbidden}");
        }

        if (c.CountOnly)
            Assert.True(QueryAggregationMode.ShouldSuppressGrid(c.Query, "customer.openitems", null));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Must_not_route_to_bulk_list(ArSalesHandlerCase c)
    {
        foreach (var blocked in c.MustNotRoute ?? Array.Empty<string>())
        {
            var classification = SageIntentEngine.Classify(c.Query);
            var (op, _, _) = InvokePlanToolCall(c.Query, classification);
            Assert.NotEqual(blocked, op);
        }
    }

    [Fact]
    public void All_ten_handler_operations_registered_in_connector_switch()
    {
        var expected = new[]
        {
            "salesinvoice.discount.count",
            "salesinvoice.discount.top",
            "customer.aged.top",
            "customer.aged.credit.top",
            "ar.invoice.overdue.buckets",
            "customer.over.creditlimit",
            "salesinvoice.partially.paid",
            "customer.invoice.unpaid.olderthan",
            "customer.outstanding.debit.top",
            "customer.sales.top"
        };

        foreach (var op in expected)
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

        if (ArSalesChatMatcher.TryRoute(message, m, parameters, tools, out var arOp))
            return (arOp, parameters, tools);

        if (ChatIntentMatcher.TryCustomerAgedTop(m, parameters, tools, out var aged))
            return (aged, parameters, tools);

        return (null, parameters, tools);
    }
}
