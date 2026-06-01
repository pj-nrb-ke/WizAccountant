using System.Text.Json;
using WizAccountant.Api.Insight;

namespace WizAccountant.Insight.Intents.Tests;

public class GlFinanceHandlerTests
{
    public static IEnumerable<object[]> Cases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "gl-vat-treasury-handlers.json");
        var json = File.ReadAllText(path);
        var cases = JsonSerializer.Deserialize<List<ArSalesHandlerCase>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        return cases.Select(c => new object[] { c });
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Handler_is_allowlisted(ArSalesHandlerCase c)
    {
        if (string.IsNullOrEmpty(c.Operation)) return;
        Assert.True(InsightReadOnlyTools.IsAllowed(c.Operation), $"[{c.Id}] {c.Operation}");
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
        var (op, parameters, _) = InvokePlanToolCall(c.Query, classification);
        Assert.Equal(c.Operation, op);
        if (c.MaxRows.HasValue && parameters.TryGetValue("top", out var topStr))
            Assert.True(int.Parse(topStr) <= c.MaxRows.Value);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Preferred_operation_routes(ArSalesHandlerCase c)
    {
        if (string.IsNullOrEmpty(c.PreferredOperation))
            return;
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
    public void All_finance_handlers_registered()
    {
        var ops = new[]
        {
            "gl.expense.top", "gl.expense.trend", "gl.expense.variance", "gl.journal.manual",
            "gl.journal.users.top", "gl.transaction.backdated", "gl.balance.unusual", "gl.journal.round",
            "gl.journal.periodend", "gl.journal.duplicate", "gl.ratios", "gl.trialbalance.anomaly",
            "vat.summary", "vat.output", "vat.input", "vat.payable.estimate", "vat.trend", "vat.anomalies",
            "vat.zero.rated", "vat.by.account.top", "vat.reconcile", "vat.missing",
            "bank.cashbook", "bank.unusual", "bank.daily.cash",
            "treasury.dashboard", "treasury.cash.forecast", "treasury.collections.forecast",
            "treasury.payments.forecast", "treasury.netcashflow.forecast", "inventory.adjustment.top"
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

        if (GlFinanceChatMatcher.TryRoute(message, m, parameters, tools, out var finOp))
            return (finOp, parameters, tools);

        if (ApPurchaseInvChatMatcher.TryRoute(message, m, parameters, tools, out var apOp))
            return (apOp, parameters, tools);

        if (InvWarehouseChatMatcher.TryRoute(message, m, parameters, tools, out var invOp))
            return (invOp, parameters, tools);

        if (ArSalesChatMatcher.TryRoute(message, m, parameters, tools, out var arOp))
            return (arOp, parameters, tools);

        return (null, parameters, tools);
    }
}
