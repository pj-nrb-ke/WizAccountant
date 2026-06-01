using System.Text.Json;
using WizAccountant.Api.Insight;

namespace WizAccountant.Insight.Intents.Tests;

public class ReconciliationHandlerTests
{
    public static IEnumerable<object[]> Cases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "reconciliation-handlers.json");
        var json = File.ReadAllText(path);
        var cases = JsonSerializer.Deserialize<List<ArSalesHandlerCase>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        return cases.Select(c => new object[] { c });
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Handler_is_allowlisted(ArSalesHandlerCase c)
    {
        if (string.IsNullOrEmpty(c.Operation) && string.IsNullOrEmpty(c.PreferredOperation))
            return;
        var op = c.Operation ?? c.PreferredOperation!;
        Assert.True(InsightReadOnlyTools.IsAllowed(op), $"[{c.Id}] {op}");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Registry_contains_handler(ArSalesHandlerCase c)
    {
        if (string.IsNullOrEmpty(c.Operation)) return;
        var entry = HandlerRegistry.Instance.GetByOperation(c.Operation);
        Assert.NotNull(entry);
        Assert.True(entry!.Implemented, $"[{c.Id}] {c.Operation}");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void PlanToolCall_routes_correctly(ArSalesHandlerCase c)
    {
        if (string.IsNullOrEmpty(c.Operation)) return;
        var classification = SageIntentEngine.Classify(c.Query);
        var (op, _, _) = InvokePlanToolCall(c.Query, classification);
        Assert.Equal(c.Operation, op);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Preferred_operation_routes(ArSalesHandlerCase c)
    {
        if (string.IsNullOrEmpty(c.PreferredOperation)) return;
        var classification = SageIntentEngine.Classify(c.Query);
        var (op, _, _) = InvokePlanToolCall(c.Query, classification);
        Assert.Equal(c.PreferredOperation, op);
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
    public void All_reconciliation_handlers_registered()
    {
        var ops = new[]
        {
            "inventory.gl.reconcile", "inventory.warehouse.reconcile", "inventory.stockgroup.reconcile",
            "inventory.gl.explain", "inventory.item.drilldown",
            "ar.gl.reconcile", "ar.variance.contributors", "ar.unallocated",
            "ap.gl.reconcile", "ap.variance.contributors", "ap.unallocated",
            "vat.reconcile", "vat.variance.contributors", "vat.missing",
            "bank.reconcile.variance", "bank.deposits.outstanding", "bank.cheques.unpresented", "bank.unmatched",
            "fa.depreciation.reconcile", "fa.variance.contributors"
        };
        foreach (var op in ops)
        {
            Assert.True(InsightReadOnlyTools.IsAllowed(op));
            Assert.NotNull(HandlerRegistry.Instance.GetByOperation(op));
        }
    }

    private static (string? operation, Dictionary<string, string> parameters, List<string> tools) InvokePlanToolCall(
        string message, SageIntentEngine.Classification classification)
    {
        var m = message.ToLowerInvariant();
        var tools = new List<string>();
        var parameters = new Dictionary<string, string> { ["top"] = "500" };
        RankingQueryPolicy.ApplyRowLimits(message, classification, parameters);

        if (ChatIntentMatcher.TryInventoryBsNegativeLedgers(m, parameters, tools, out var negGl))
            return (negGl, parameters, tools);

        if (ChatIntentMatcher.TryUnpaidSalesInvoices(m, parameters, tools, out var openOp))
            return (openOp, parameters, tools);

        if (ReconciliationChatMatcher.TryRoute(message, m, parameters, tools, out var reconOp))
            return (reconOp, parameters, tools);

        if (ArSalesChatMatcher.TryRoute(message, m, parameters, tools, out var arOp))
            return (arOp, parameters, tools);

        if (ApPurchaseInvChatMatcher.TryRoute(message, m, parameters, tools, out var apOp))
            return (apOp, parameters, tools);

        if (InvWarehouseChatMatcher.TryRoute(message, m, parameters, tools, out var invOp))
            return (invOp, parameters, tools);

        if (GlFinanceChatMatcher.TryRoute(message, m, parameters, tools, out var finOp))
            return (finOp, parameters, tools);

        if (ChatIntentMatcher.TryCustomerUnpaidSummary(m, parameters, tools, out var summaryOp))
            return (summaryOp, parameters, tools);

        return (null, parameters, tools);
    }
}
