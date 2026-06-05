using WizAccountant.Api.Insight;
using WizAccountant.Contracts;

namespace WizAccountant.Insight.Intents.Tests;

public class PurchaseProductQuarterlyRoutingTests
{
    private const string UserQuery =
        "how much CPO (Crude Palm Oil) was bought in Q1, Q2, Q3 & Q4 of 2025. I need total per quarter.";

    [Fact]
    public void Routes_cpo_purchase_by_quarter_query()
    {
        var classification = SageIntentEngine.Classify(UserQuery);
        var (op, parameters, _) = ChatRoutePlanner.Plan(UserQuery, classification);

        Assert.Equal(DynamicAnalyticalQueryBuilder.PurchaseItemPeriodSummaryOperation, op);
        Assert.True(InsightReadOnlyTools.IsAllowed(op!));
        Assert.Equal("2025", parameters.GetValueOrDefault("year"));
    }

    [Fact]
    public void Does_not_route_to_sales_credit_note_handlers()
    {
        var classification = SageIntentEngine.Classify(UserQuery);
        var (op, _, _) = ChatRoutePlanner.Plan(UserQuery, classification);

        Assert.NotEqual("salescreditnote.count", op);
        Assert.NotEqual("salesinvoice.discount.count", op);
        Assert.NotEqual("product.monthly.orders.analysis", op);
    }

    [Fact]
    public void Saved_sql_title_detected_as_purchase_quarterly()
    {
        const string sql = """
            SELECT CASE WHEN MONTH(H.InvDate) BETWEEN 1 AND 3 THEN 'Q1 2025' END AS QuarterName,
            SUM(L.fQuantity) AS TotalQty
            FROM InvNum H
            INNER JOIN _btblInvoiceLines L ON L.iInvoiceID = H.AutoIndex
            INNER JOIN StkItem S ON S.StockLink = L.iStockCodeID
            WHERE H.DocType IN (2,5) AND S.Code IN ('DRCPO01','DRCPO02')
            GROUP BY QuarterName
            """;

        Assert.True(InsightSavedSqlPromptMatcher.LooksLikePurchaseProductQuarterlySql(sql));
    }

    [Fact]
    public void Connector_registers_operation()
    {
        var path = FindConnectorFile("SageSdkPhase2Handlers.cs");
        Assert.Contains("\"purchase.item.period.summary\"", File.ReadAllText(path));
    }

    private static string FindConnectorFile(string fileName)
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = start;
            for (var i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++, dir = Path.GetDirectoryName(dir))
            {
                var candidate = Path.Combine(dir, "src", "WizConnector.Service", "Sage", fileName);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        throw new FileNotFoundException(fileName);
    }
}
