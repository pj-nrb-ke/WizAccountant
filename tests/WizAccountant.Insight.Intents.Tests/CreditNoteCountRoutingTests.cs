using System.Text.Json;
using WizAccountant.Api.Insight;
using WizAccountant.Contracts;

namespace WizAccountant.Insight.Intents.Tests;

public class CreditNoteCountRoutingTests
{
    [Theory]
    [InlineData("total credit notes issues in Q1 2025")]
    [InlineData("How many credit notes were issued last month?")]
    [InlineData("total credit notes issued in Q1 2025")]
    [InlineData("number of credit notes in 2025")]
    public void Routes_to_sales_credit_note_count(string query)
    {
        var classification = SageIntentEngine.Classify(query);
        var (op, parameters, _) = ChatRoutePlanner.Plan(query, classification);

        Assert.Equal(CreditNoteChatHelper.SalesCreditNoteCountOperation, op);
        Assert.True(InsightReadOnlyTools.IsAllowed(op!));
    }

    [Fact]
    public void Q1_2025_period_applied()
    {
        const string query = "total credit notes issues in Q1 2025";
        var classification = SageIntentEngine.Classify(query);
        var (op, parameters, _) = ChatRoutePlanner.Plan(query, classification);
        var contract = QueryIntentContract.Parse(query, classification);

        Assert.NotNull(op);
        Assert.True(InsightChatPeriodHelper.TryApplyForOperation(op, query, parameters, contract, out _));
        Assert.Equal("2025-01-01", parameters.GetValueOrDefault("dateFrom"));
        Assert.Equal("2025-03-31", parameters.GetValueOrDefault("dateTo"));
    }

    [Fact]
    public void Does_not_route_to_aged_credit_top()
    {
        const string query = "total credit notes issues in Q1 2025";
        var classification = SageIntentEngine.Classify(query);
        var (op, _, _) = ChatRoutePlanner.Plan(query, classification);
        Assert.NotEqual("customer.aged.credit.top", op);
    }

    [Fact]
    public void Connector_switch_registers_operation()
    {
        var path = FindPhase2HandlersSource();
        var source = File.ReadAllText(path);
        Assert.Contains("\"salescreditnote.count\"", source);
    }

    private static string FindPhase2HandlersSource()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = start;
            for (var i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++, dir = Path.GetDirectoryName(dir))
            {
                var candidate = Path.Combine(dir, "src", "WizConnector.Service", "Sage", "SageSdkPhase2Handlers.cs");
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        throw new FileNotFoundException("SageSdkPhase2Handlers.cs not found");
    }
}
