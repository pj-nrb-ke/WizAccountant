using WizAccountant.Api;
using WizAccountant.Api.Insight;
using WizAccountant.Contracts;

namespace WizAccountant.Insight.Intents.Tests;

public class InsightSavedSqlPromptMatcherTests
{
    private const string UserPrompt =
        "which products are most frequently bought. I need Quantity sold per month between Q3 and Q4 of 2025";

    [Fact]
    public void FindMatch_links_frequently_bought_title_to_user_prompt()
    {
        var saved = new InsightSavedSqlQueryRecord
        {
            QueryId = Guid.NewGuid(),
            Title = "Frequently bought products in a quarter",
            AiPrompt = UserPrompt,
            Sql = """
                SELECT YEAR(h.InvDate) AS SalesYear, s.Code AS ProductCode,
                SUM(l.fQtyChange) AS TotalQtySold
                FROM _btblInvoiceLines l
                INNER JOIN InvNum h ON h.AutoIndex = l.iInvoiceID
                INNER JOIN StkItem s ON s.StockLink = l.iStockCodeID
                GROUP BY YEAR(h.InvDate), s.Code
                """
        };

        var match = InsightSavedSqlPromptMatcher.FindMatch(UserPrompt, [saved]);
        Assert.NotNull(match);
        Assert.Equal("Frequently bought products in a quarter", match!.Title);
    }

    [Fact]
    public void LooksLikeProductMonthlySql_detects_saved_reference_sql()
    {
        const string sql = """
            SELECT ProductCode, TotalQtySold FROM _btblInvoiceLines l
            JOIN InvNum h ON h.AutoIndex = l.iInvoiceID
            JOIN StkItem s ON s.StockLink = l.iStockCodeID
            GROUP BY ProductCode
            """;
        Assert.True(InsightSavedSqlPromptMatcher.LooksLikeProductMonthlySql(sql));
    }

    [Fact]
    public void Plan_and_saved_reference_both_target_product_monthly()
    {
        const string UserPrompt =
            "which products are most frequently bought. I need Quantity sold per month between Q3 and Q4 of 2025";
        var classification = SageIntentEngine.Classify(UserPrompt);
        var (op, parameters, _) = ChatRoutePlanner.Plan(UserPrompt, classification);
        Assert.Equal(ProductOrderAnalysisChatMatcher.Operation, op);

        var contract = QueryIntentContract.Parse(UserPrompt, classification);
        Assert.NotNull(contract.Period);
        Assert.True(InsightChatPeriodHelper.TryApplyForOperation(op, UserPrompt, parameters, contract, out _));
        Assert.Equal("2025-07-01", parameters["dateFrom"]);
        Assert.Equal("2025-12-31", parameters["dateTo"]);
    }
}
