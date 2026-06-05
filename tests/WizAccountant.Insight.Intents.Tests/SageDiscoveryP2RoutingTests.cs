using WizAccountant.Api.Insight;
using WizAccountant.Contracts;

namespace WizAccountant.Insight.Intents.Tests;

public class SageDiscoveryP2RoutingTests
{
    [Theory]
    [InlineData("how many debit notes to customers in Q1 2025", DebitNoteChatHelper.CountOperation)]
    [InlineData("list customer debit notes this month", DebitNoteChatHelper.ListOperation)]
    [InlineData("top customers by debit notes in 2025", DebitNoteChatHelper.TopOperation)]
    public void Routes_customer_debit_note_queries(string query, string expectedOp)
    {
        var classification = SageIntentEngine.Classify(query);
        var (op, _, _) = ChatRoutePlanner.Plan(query, classification);
        Assert.Equal(expectedOp, op);
        Assert.True(InsightReadOnlyTools.IsAllowed(op!));
    }

    [Theory]
    [InlineData("total supplier credit notes in Q1 2025", SupplierCreditNoteChatHelper.CountOperation)]
    [InlineData("list supplier credit notes this month", SupplierCreditNoteChatHelper.ListOperation)]
    [InlineData("top suppliers by credit notes in 2025", SupplierCreditNoteChatHelper.TopOperation)]
    public void Routes_supplier_credit_note_queries(string query, string expectedOp)
    {
        var classification = SageIntentEngine.Classify(query);
        var (op, _, _) = ChatRoutePlanner.Plan(query, classification);
        Assert.Equal(expectedOp, op);
        Assert.True(InsightReadOnlyTools.IsAllowed(op!));
    }

    [Fact]
    public void Supplier_credit_notes_do_not_route_to_credit_balances()
    {
        const string query = "total supplier credit notes in Q1 2025";
        var classification = SageIntentEngine.Classify(query);
        var (op, _, _) = ChatRoutePlanner.Plan(query, classification);
        Assert.NotEqual("supplier.credit.balances", op);
    }

    [Theory]
    [InlineData("warehouse transfers this month", WarehouseTransferChatHelper.SummaryOperation)]
    [InlineData("show warehouse transfer detail this month", WarehouseTransferChatHelper.DetailOperation)]
    [InlineData("top warehouse transfers in 2025", WarehouseTransferChatHelper.TopOperation)]
    [InlineData("warehouse transfers by item this year", WarehouseTransferChatHelper.ByItemOperation)]
    public void Routes_warehouse_transfer_queries(string query, string expectedOp)
    {
        var classification = SageIntentEngine.Classify(query);
        var (op, _, _) = ChatRoutePlanner.Plan(query, classification);
        Assert.Equal(expectedOp, op);
    }

    [Fact]
    public void Connector_registers_p2_operations()
    {
        var path = FindPhase2HandlersSource();
        var source = File.ReadAllText(path);
        Assert.Contains("\"salesdebitnote.count\"", source);
        Assert.Contains("\"suppliercreditnote.count\"", source);
        Assert.Contains("\"warehouse.transfer.detail\"", source);
        Assert.Contains("TrCode IN ('WHT', 'WHTC')", File.ReadAllText(FindConnectorFile("SageTrCodeSqlHelper.cs")));
    }

    private static string FindPhase2HandlersSource() => FindConnectorFile("SageSdkPhase2Handlers.cs");

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
