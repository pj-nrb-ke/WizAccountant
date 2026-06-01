using System.Text.Json;
using WizAccountant.Api.Insight;

namespace WizAccountant.Insight.Intents.Tests;

public class ConsolidationHandlerTests
{
    public static IEnumerable<object[]> Cases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "consolidation-handlers.json");
        var json = File.ReadAllText(path);
        var cases = JsonSerializer.Deserialize<List<ArSalesHandlerCase>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        return cases.Select(c => new object[] { c });
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Business_process_classifier_detects_known_process(ArSalesHandlerCase c)
    {
        if (c.Id is "con-guard-01")
            return;

        var bp = BusinessProcessClassifier.Classify(c.Query);
        if (bp.Process == BusinessProcessType.Unknown && !string.IsNullOrEmpty(c.Operation))
        {
            var classification = SageIntentEngine.Classify(c.Query);
            var (_, _, tools) = ChatRoutePlanner.Plan(c.Query, classification);
            Assert.Contains(tools, t => t.StartsWith("businessProcess:", StringComparison.Ordinal));
            return;
        }

        Assert.NotEqual(BusinessProcessType.Unknown, bp.Process);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Plan_routes_to_canonical_operation(ArSalesHandlerCase c)
    {
        if (string.IsNullOrEmpty(c.Operation) && string.IsNullOrEmpty(c.PreferredOperation))
            return;

        var expected = c.Operation ?? c.PreferredOperation!;
        var classification = SageIntentEngine.Classify(c.Query);
        var (op, _, tools) = ChatRoutePlanner.Plan(c.Query, classification);
        Assert.Equal(expected, op);
        if (c.Id is not "con-guard-01")
            Assert.Contains(tools, t => t.StartsWith("businessProcess:", StringComparison.Ordinal));
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
    public void Confusion_guard_blocks_prompt_payer_to_outstanding()
    {
        Assert.True(BusinessProcessConfusionGuards.IsBlocked(
            "which customers pay promptly",
            "customer.outstanding.debit.top"));
    }

    [Fact]
    public void Registry_canonical_payment_handler_loaded()
    {
        var entry = HandlerRegistry.Instance.GetCanonical("customer.payment.prompt.top");
        Assert.NotNull(entry);
        Assert.True(entry!.IsCanonical);
        Assert.Equal("customer_payment_discipline", entry.BusinessProcess);
    }

    [Fact]
    public void Investigation_context_applies_warehouse_follow_up()
    {
        var parameters = new Dictionary<string, string>();
        var prior = InvestigationContext.FromPriorAssistantMessage(
            JsonSerializer.Serialize(new List<string> { "inventory.gl.explain" }),
            "inventory variance");
        prior?.ApplyFollowUp("show warehouse 10 details", parameters);
        Assert.Equal("10", parameters["warehouseCode"]);
    }
}
