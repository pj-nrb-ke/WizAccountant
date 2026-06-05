using WizAccountant.Api.Insight;
using WizAccountant.Contracts;

namespace WizAccountant.Insight.Intents.Tests;

public class SupplierUnpaidRoutingTests
{
    [Theory]
    [InlineData("how many suppliers are not paid till date", "supplier.unpaid.count")]
    [InlineData("how many suppliers are unpaid", "supplier.unpaid.count")]
    [InlineData("count unpaid suppliers", "supplier.unpaid.count")]
    [InlineData("suppliers not paid as of today", "supplier.unpaid.count")]
    [InlineData("how many creditors are still unpaid", "supplier.unpaid.count")]
    [InlineData("suppliers with outstanding payable balance how many", "supplier.unpaid.count")]
    public void Count_queries_route_to_supplier_unpaid_count(string query, string expectedOp)
    {
        var classification = SageIntentEngine.Classify(query);
        var (op, parameters, _) = ChatRoutePlanner.Plan(query, classification);
        Assert.Equal(expectedOp, op);
        Assert.True(InsightReadOnlyTools.IsAllowed(op!));
        if (expectedOp == ChatIntentMatcher.SupplierUnpaidCountOp)
            Assert.Equal("true", parameters.GetValueOrDefault("countOnly"));
    }

    [Fact]
    public void Which_suppliers_not_paid_routes_to_list()
    {
        const string query = "which suppliers are not paid till date";
        var classification = SageIntentEngine.Classify(query);
        var (op, _, _) = ChatRoutePlanner.Plan(query, classification);
        Assert.Equal(ChatIntentMatcher.SupplierUnpaidListOp, op);
    }

    [Fact]
    public void List_suppliers_not_paid_routes_to_list()
    {
        const string query = "list suppliers not paid till date";
        var classification = SageIntentEngine.Classify(query);
        var (op, _, _) = ChatRoutePlanner.Plan(query, classification);
        Assert.Equal(ChatIntentMatcher.SupplierUnpaidListOp, op);
    }

    [Fact]
    public void Top_suppliers_not_paid_routes_to_top()
    {
        const string query = "top suppliers not paid";
        var classification = SageIntentEngine.Classify(query);
        var (op, _, _) = ChatRoutePlanner.Plan(query, classification);
        Assert.Equal(ChatIntentMatcher.SupplierUnpaidTopOp, op);
    }

    [Fact]
    public void Does_not_match_paid_after_due_digest_fallback()
    {
        const string query = "how many suppliers are not paid till date";
        var classification = SageIntentEngine.Classify(query);

        Assert.False(MegaDigestFallbackMatcher.TryBuildReply(
            query, classification, out var reply, out _));

        Assert.DoesNotContain("paid after due date", reply, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("how many suppliers are not paid till date", "supplier.openitems")]
    [InlineData("how many suppliers are not paid till date", "supplier.list")]
    [InlineData("which suppliers are not paid till date", "supplier.openitems")]
    public void Does_not_misroute_to_master_or_openitems_listing(string query, string wrongOp)
    {
        var classification = SageIntentEngine.Classify(query);
        var (op, _, _) = ChatRoutePlanner.Plan(query, classification);
        Assert.NotEqual(wrongOp, op);
    }

    [Fact]
    public void Paid_after_due_query_does_not_route_to_supplier_unpaid_handlers()
    {
        const string query = "which suppliers were paid after due date";
        var classification = SageIntentEngine.Classify(query);
        var (op, _, _) = ChatRoutePlanner.Plan(query, classification);
        Assert.NotEqual(ChatIntentMatcher.SupplierUnpaidCountOp, op);
        Assert.NotEqual(ChatIntentMatcher.SupplierUnpaidListOp, op);
    }

    [Fact]
    public void Connector_registers_all_supplier_unpaid_operations()
    {
        var path = FindConnectorFile("SageSdkPhase2Handlers.cs");
        var source = File.ReadAllText(path);
        Assert.Contains("\"supplier.unpaid.count\"", source);
        Assert.Contains("\"supplier.unpaid.list\"", source);
        Assert.Contains("\"supplier.unpaid.top\"", source);
    }

    [Fact]
    public void Output_contract_requires_supplier_unpaid_count_fields()
    {
        var contract = QueryIntentContract.Parse(
            "how many suppliers are not paid till date",
            SageIntentEngine.Classify("how many suppliers are not paid till date"));
        var valid = """
            {"totalUnpaidSuppliers":3,"totalOutstandingPayable":100.5,"asOfDate":"2026-05-26"}
            """;
        var result = OutputContractValidator.Validate(contract, ChatIntentMatcher.SupplierUnpaidCountOp, valid);
        Assert.True(result.IsValid);
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
