using WizAccountant.Api.Insight;
using WizAccountant.Contracts;

namespace WizAccountant.Insight.Intents.Tests;

public class DynamicAnalyticalQueryBuilderTests
{
    private static (string? Op, Dictionary<string, string> Params) Plan(string query)
    {
        var classification = SageIntentEngine.Classify(query);
        var contract = QueryIntentContract.Parse(query, classification);
        var parameters = new Dictionary<string, string>();
        var tools = new List<string>();
        DynamicAnalyticalQueryBuilder.TryPlan(query, contract, parameters, tools, out var op);
        return (op, parameters);
    }

    [Theory]
    [InlineData("how much CPO was bought in Q1 Q2 Q3 Q4 of 2025")]
    [InlineData("how much DRCPO01 was purchased by quarter in 2025")]
    [InlineData("purchase quantity of DRCPO01 and DRCPO02 by quarter")]
    [InlineData("crude palm oil bought per quarter in 2025")]
    [InlineData("item purchase quantity and value by month for DRCPO01 in 2025")]
    public void Routes_item_purchase_by_period_queries(string query)
    {
        var (op, parameters) = Plan(query);

        Assert.Equal(DynamicAnalyticalQueryBuilder.PurchaseItemPeriodSummaryOperation, op);
        Assert.True(InsightReadOnlyTools.IsAllowed(op!));
        if (query.Contains("month", StringComparison.OrdinalIgnoreCase))
            Assert.Equal("month", parameters.GetValueOrDefault("groupBy"));
        else
            Assert.Equal("quarter", parameters.GetValueOrDefault("groupBy"));
    }

    [Fact]
    public void Cpo_user_query_extracts_year_and_does_not_hit_mega_digest_only()
    {
        const string query =
            "how much CPO (Crude Palm Oil) was bought in Q1, Q2, Q3 & Q4 of 2025. I need total per quarter. Code = DRCPO01 and DRCPO02";
        var classification = SageIntentEngine.Classify(query);
        var contract = QueryIntentContract.Parse(query, classification);

        Assert.True(DynamicAnalyticalQueryBuilder.CanAnswer(query, contract));

        var (op, parameters, _) = ChatRoutePlanner.Plan(query, classification);
        Assert.Equal(DynamicAnalyticalQueryBuilder.PurchaseItemPeriodSummaryOperation, op);
        Assert.Equal("2025", parameters.GetValueOrDefault("year"));
        Assert.Contains("DRCPO01", parameters.GetValueOrDefault("productCodes") ?? "");
    }

    [Fact]
    public void Does_not_show_no_handler_when_dynamic_can_answer()
    {
        const string query = "how much CPO was bought in Q1 Q2 Q3 Q4 of 2025";
        var classification = SageIntentEngine.Classify(query);

        Assert.False(MegaDigestFallbackMatcher.TryBuildReply(
            query, classification, out var reply, out _));
        Assert.DoesNotContain("No dedicated SQL handler matched", reply);
    }

    [Theory]
    [InlineData("how much CPO was bought in Q1 Q2 Q3 Q4 of 2025", "customer.collections.summary")]
    [InlineData("how much CPO was bought in Q1 Q2 Q3 Q4 of 2025", "salesinvoice.discount.count")]
    [InlineData("how much CPO was bought in Q1 Q2 Q3 Q4 of 2025", "customer.list")]
    public void Does_not_misroute_purchase_period_to_unrelated_handlers(string query, string wrongOp)
    {
        var classification = SageIntentEngine.Classify(query);
        var (op, _, _) = ChatRoutePlanner.Plan(query, classification);
        Assert.NotEqual(wrongOp, op);
    }
}
