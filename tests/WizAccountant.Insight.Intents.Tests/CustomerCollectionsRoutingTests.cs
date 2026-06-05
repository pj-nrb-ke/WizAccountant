using System.Text.Json;
using WizAccountant.Api.Insight;
using WizAccountant.Contracts;

namespace WizAccountant.Insight.Intents.Tests;

public class CustomerCollectionsRoutingTests
{
    public static IEnumerable<object[]> Cases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "customer-collections-handlers.json");
        var json = File.ReadAllText(path);
        var cases = JsonSerializer.Deserialize<List<CustomerCollectionsCase>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        return cases.Select(c => new object[] { c });
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Plan_routes_to_expected_operation(CustomerCollectionsCase c)
    {
        var classification = SageIntentEngine.Classify(c.Query);
        var (op, parameters, _) = ChatRoutePlanner.Plan(c.Query, classification);

        Assert.Equal(c.Operation, op);
        Assert.True(InsightReadOnlyTools.IsAllowed(c.Operation!));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Must_not_route_to_customer_list(CustomerCollectionsCase c)
    {
        foreach (var blocked in c.MustNotRoute ?? [])
        {
            var classification = SageIntentEngine.Classify(c.Query);
            var (op, _, _) = ChatRoutePlanner.Plan(c.Query, classification);
            Assert.NotEqual(blocked, op);
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Q2_period_applied_for_collections(CustomerCollectionsCase c)
    {
        if (!c.Query.Contains("Q2", StringComparison.OrdinalIgnoreCase))
            return;
        if (c.Query.Contains(" and Q", StringComparison.OrdinalIgnoreCase) ||
            c.Query.Contains(" & Q", StringComparison.OrdinalIgnoreCase))
            return;

        var classification = SageIntentEngine.Classify(c.Query);
        var (op, parameters, _) = ChatRoutePlanner.Plan(c.Query, classification);
        var contract = QueryIntentContract.Parse(c.Query, classification);

        Assert.NotNull(op);
        Assert.True(InsightChatPeriodHelper.TryApplyForOperation(op, c.Query, parameters, contract, out _));
        Assert.True(parameters.TryGetValue("dateFrom", out var dateFrom));
        Assert.True(parameters.TryGetValue("dateTo", out var dateTo));
        Assert.EndsWith("-04-01", dateFrom);
        Assert.EndsWith("-06-30", dateTo);
        if (c.Query.Contains("2025", StringComparison.Ordinal))
        {
            Assert.Equal("2025-04-01", dateFrom);
            Assert.Equal("2025-06-30", dateTo);
        }
    }

    [Fact]
    public void Expected_collections_next_month_still_routes_to_treasury_forecast()
    {
        const string query = "Expected customer collections next month";
        var classification = SageIntentEngine.Classify(query);
        var (op, _, _) = ChatRoutePlanner.Plan(query, classification);
        Assert.Equal("treasury.collections.forecast", op);
    }

    [Fact]
    public void Helper_detects_collection_from_customers()
    {
        Assert.True(CustomerCollectionsHelper.IsCustomerCollectionsQuery(
            "what was the collection from customers in q2 2025"));
        Assert.False(CustomerCollectionsHelper.IsCustomerCollectionsQuery(
            "expected customer collections next month"));
    }

    [Fact]
    public void All_collection_operations_allowlisted_and_registered()
    {
        var ops = new[]
        {
            CustomerCollectionsHelper.SummaryOperation,
            CustomerCollectionsHelper.ByMonthOperation,
            CustomerCollectionsHelper.ByCustomerOperation,
            CustomerCollectionsHelper.TopOperation
        };

        foreach (var op in ops)
        {
            Assert.True(InsightReadOnlyTools.IsAllowed(op));
            Assert.NotNull(HandlerRegistry.Instance.GetByOperation(op));
        }
    }

    [Fact]
    public void Q2_and_Q4_collections_applies_segmented_period()
    {
        const string query = "what was the total collection from customers in Q2 and Q4 of 2025";
        var classification = SageIntentEngine.Classify(query);
        var (op, parameters, _) = ChatRoutePlanner.Plan(query, classification);
        var contract = QueryIntentContract.Parse(query, classification);

        Assert.Equal(CustomerCollectionsHelper.SummaryOperation, op);
        Assert.True(InsightChatPeriodHelper.TryApplyForOperation(op, query, parameters, contract, out var blockReason), blockReason);
        Assert.Equal("false", parameters.GetValueOrDefault("periodIsContiguous"));
        Assert.Contains("Q2 2025", parameters.GetValueOrDefault("segmentsJson") ?? "");
        Assert.Contains("Q4 2025", parameters.GetValueOrDefault("segmentsJson") ?? "");
    }

    [Fact]
    public void Collection_operations_registered_in_connector_phase2_switch()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "WizConnector.Service", "Sage", "SageSdkPhase2Handlers.cs"));
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = start;
            for (var i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++, dir = Path.GetDirectoryName(dir))
            {
                var candidate = Path.Combine(dir, "src", "WizConnector.Service", "Sage", "SageSdkPhase2Handlers.cs");
                if (File.Exists(candidate))
                {
                    path = candidate;
                    break;
                }
            }
        }

        Assert.True(File.Exists(path), path);
        var source = File.ReadAllText(path);
        foreach (var op in new[]
        {
            CustomerCollectionsHelper.SummaryOperation,
            CustomerCollectionsHelper.ByMonthOperation,
            CustomerCollectionsHelper.ByCustomerOperation,
            CustomerCollectionsHelper.TopOperation
        })
        {
            Assert.Contains($"\"{op}\"", source);
        }
    }

    public sealed class CustomerCollectionsCase
    {
        public string Id { get; set; } = "";
        public string Query { get; set; } = "";
        public string? Operation { get; set; }
        public string[]? MustNotRoute { get; set; }
    }
}
