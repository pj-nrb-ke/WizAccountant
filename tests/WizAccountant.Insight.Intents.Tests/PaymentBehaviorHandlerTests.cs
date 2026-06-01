using System.Text.Json;
using WizAccountant.Api.Insight;

namespace WizAccountant.Insight.Intents.Tests;

public class PaymentBehaviorHandlerTests
{
    public static IEnumerable<object[]> Cases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "payment-behavior-handlers.json");
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

    private static (string? operation, Dictionary<string, string> parameters, List<string> tools) InvokePlanToolCall(
        string message, SageIntentEngine.Classification classification)
    {
        var m = message.ToLowerInvariant();
        var tools = new List<string>();
        var parameters = new Dictionary<string, string> { ["top"] = "500" };
        RankingQueryPolicy.ApplyRowLimits(message, classification, parameters);

        if (ArPaymentBehaviorChatMatcher.TryRoute(message, m, parameters, tools, out var payOp))
            return (payOp, parameters, tools);

        if (ArSalesChatMatcher.TryRoute(message, m, parameters, tools, out var arOp))
            return (arOp, parameters, tools);

        if (m.Contains("who owes") && m.Contains("most"))
        {
            var tools2 = new List<string> { "customer.outstanding.debit.top" };
            return ("customer.outstanding.debit.top", parameters, tools2);
        }

        if (ChatIntentMatcher.TryCustomerUnpaidSummary(m, parameters, tools, out var summaryOp))
            return (summaryOp, parameters, tools);

        if (ChatIntentMatcher.TryCustomerAgedTop(m, parameters, tools, out var agedOp))
            return (agedOp, parameters, tools);

        return (null, parameters, tools);
    }
}
