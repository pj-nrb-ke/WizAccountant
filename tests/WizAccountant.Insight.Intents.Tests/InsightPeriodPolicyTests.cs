using WizAccountant.Api.Insight;
using WizAccountant.Contracts;

namespace WizAccountant.Insight.Intents.Tests;

public class InsightPeriodPolicyTests
{
    [Fact]
    public void Snapshot_handler_skips_period_application()
    {
        Assert.False(InsightPeriodPolicy.ShouldApplyPeriodRange("customer.aged.top"));
        Assert.False(InsightPeriodPolicy.ShouldApplyPeriodRange("customer.openitems"));
    }

    [Fact]
    public void Date_range_handler_applies_period()
    {
        Assert.True(InsightPeriodPolicy.ShouldApplyPeriodRange("customer.sales.top"));
        Assert.True(InsightPeriodPolicy.ShouldApplyPeriodRange("product.monthly.orders.analysis"));
    }

    [Fact]
    public void CompatibilityGate_blocks_segmented_on_sales_top()
    {
        var contract = new QueryIntentContract
        {
            RawQuery = "customer sales Q1 & Q3 2025",
            Period = new InsightPeriodResolution
            {
                DateFrom = new DateTime(2025, 1, 1),
                DateTo = new DateTime(2025, 9, 30),
                PeriodType = InsightPeriodTypes.QuarterSegments,
                IsContiguous = false,
                Segments =
                [
                    new InsightPeriodSegment { From = new DateTime(2025, 1, 1), To = new DateTime(2025, 3, 31), Label = "Q1 2025" },
                    new InsightPeriodSegment { From = new DateTime(2025, 7, 1), To = new DateTime(2025, 9, 30), Label = "Q3 2025" }
                ]
            }
        };

        Assert.False(CompatibilityGate.IsCompatible(contract, "customer.sales.top", out var reason));
        Assert.Contains("split-period", reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompatibilityGate_allows_segmented_on_product_monthly()
    {
        var contract = new QueryIntentContract
        {
            RawQuery = "products Q1 & Q3 2025",
            Groupings = ["product", "month"],
            Metrics = ["quantity", "value"],
            Period = new InsightPeriodResolution
            {
                IsContiguous = false,
                Segments = [new InsightPeriodSegment(), new InsightPeriodSegment()]
            }
        };

        Assert.True(CompatibilityGate.IsCompatible(contract, ProductOrderAnalysisChatMatcher.Operation, out _));
    }
}
