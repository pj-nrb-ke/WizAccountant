using System.Text.Json;
using WizAccountant.Api.Insight;

namespace WizAccountant.Insight.Intents.Tests;

public class OutputValidationAndSafetyTests
{
    [Fact]
    public void Product_monthly_valid_json_passes_contract()
    {
        var json = """
            {
              "monthlyBreakdown": [
                { "month": "Jan 2026", "productCode": "A", "productName": "Prod A", "quantity": 10, "value": 100 }
              ],
              "topProductByQuantity": { "productCode": "A", "productName": "Prod A", "totalQuantity": 10, "totalValue": 100 }
            }
            """;
        var contract = QueryIntentContract.Parse(
            "product monthly quantity value",
            SageIntentEngine.Classify("product monthly"),
            null);
        var result = OutputContractValidator.Validate(contract, ProductOrderAnalysisChatMatcher.Operation, json);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Product_monthly_missing_monthlyBreakdown_fails()
    {
        var json = """{ "topProductByQuantity": { "productCode": "A" } }""";
        var contract = QueryIntentContract.Parse(
            "per product per month by quantity and value",
            SageIntentEngine.Classify("top products"),
            null);
        var result = OutputContractValidator.Validate(contract, ProductOrderAnalysisChatMatcher.Operation, json);
        Assert.False(result.IsValid);
        Assert.Contains("monthly", result.SafeFailureMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SafeExecutionBoundary_strips_stack_traces()
    {
        var msg = "System.NotSupportedException: SQLite does not support DateTimeOffset\r\n   at Microsoft.EntityFrameworkCore...";
        var safe = SafeExecutionBoundary.SanitizeForUser(msg);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", safe);
        Assert.Contains("database", safe, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Product_monthly_invalid_json_rejected()
    {
        var contract = QueryIntentContract.Parse(
            "per product per month",
            SageIntentEngine.Classify("product monthly"),
            null);
        var result = OutputContractValidator.Validate(contract, ProductOrderAnalysisChatMatcher.Operation, "{not json");
        Assert.False(result.IsValid);
        Assert.Contains("valid JSON", result.MissingFields);
    }

    [Fact]
    public void SafeExecutionBoundary_format_includes_operation()
    {
        var text = SafeExecutionBoundary.FormatHandlerFailure("product.monthly.orders.analysis", "Test reason.");
        Assert.Contains("product.monthly.orders.analysis", text);
        Assert.Contains("Execution failed safely", text);
    }
}
