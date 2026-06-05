using WizAccountant.Contracts;

namespace WizAccountant.Insight.Intents.Tests;

public class InsightDateRangeParserTests
{
    private static readonly DateTime Ref = new(2026, 6, 15);

    [Fact]
    public void Q3_and_Q4_of_2025_is_july_through_december()
    {
        var q = "which products are most frequently bought between Q3 and Q4 of 2025";
        Assert.True(InsightDateRangeParser.TryResolvePeriod(q, 2025, Ref, out var period));
        Assert.True(period.IsContiguous);
        Assert.Equal(new DateTime(2025, 7, 1), period.DateFrom);
        Assert.Equal(new DateTime(2025, 12, 31), period.DateTo);
        Assert.Equal(InsightPeriodTypes.QuarterRange, period.PeriodType);
    }

    [Fact]
    public void Q1_and_Q3_2025_is_non_contiguous_segments()
    {
        var q = "sales in Q1 & Q3 2025";
        Assert.True(InsightDateRangeParser.TryResolvePeriod(q, 2025, Ref, out var period));
        Assert.False(period.IsContiguous);
        Assert.Equal(2, period.Segments.Count);
        Assert.Equal(new DateTime(2025, 1, 1), period.Segments[0].From);
        Assert.Equal(new DateTime(2025, 3, 31), period.Segments[0].To);
        Assert.Equal(new DateTime(2025, 7, 1), period.Segments[1].From);
        Assert.Equal(new DateTime(2025, 9, 30), period.Segments[1].To);
        Assert.Equal(InsightPeriodTypes.QuarterSegments, period.PeriodType);
    }

    [Fact]
    public void H1_2025_is_january_through_june()
    {
        Assert.True(InsightDateRangeParser.TryResolvePeriod("purchases H1 2025", 2025, Ref, out var period));
        Assert.Equal(new DateTime(2025, 1, 1), period.DateFrom);
        Assert.Equal(new DateTime(2025, 6, 30), period.DateTo);
        Assert.Equal(InsightPeriodTypes.HalfYear, period.PeriodType);
    }

    [Fact]
    public void Ytd_uses_utc_reference_year_start()
    {
        Assert.True(InsightDateRangeParser.TryResolvePeriod("expenses YTD", 2026, Ref, out var period));
        Assert.Equal(new DateTime(2026, 1, 1), period.DateFrom);
        Assert.Equal(Ref, period.DateTo);
        Assert.Equal(InsightPeriodTypes.Ytd, period.PeriodType);
    }

    [Fact]
    public void Last_quarter_is_previous_calendar_quarter()
    {
        Assert.True(InsightDateRangeParser.TryResolvePeriod("sales last quarter", null, Ref, out var period));
        Assert.Equal(new DateTime(2026, 1, 1), period.DateFrom);
        Assert.Equal(new DateTime(2026, 3, 31), period.DateTo);
    }

    [Fact]
    public void Resolve_uses_parameters_when_set()
    {
        var p = new Dictionary<string, string>
        {
            ["dateFrom"] = "2025-07-01",
            ["dateTo"] = "2025-12-31"
        };
        var period = InsightDateRangeParser.ResolvePeriod(p, Ref);
        Assert.Equal(new DateTime(2025, 7, 1), period.DateFrom);
        Assert.Equal(new DateTime(2025, 12, 31), period.DateTo);
    }

    [Fact]
    public void From_jan_2026_runs_through_utc_reference()
    {
        var q = "product monthly from Jan 2026";
        Assert.True(InsightDateRangeParser.TryResolvePeriod(q, 2026, Ref, out var period));
        Assert.Equal(new DateTime(2026, 1, 1), period.DateFrom);
        Assert.Equal(Ref, period.DateTo);
    }

    [Fact]
    public void Default_full_year_when_only_year_in_message()
    {
        var period = InsightDateRangeParser.ResolvePeriod(new Dictionary<string, string>
        {
            ["message"] = "top customers in 2025",
            ["year"] = "2025"
        }, Ref);
        Assert.Equal(InsightPeriodTypes.DefaultFullYear, period.PeriodType);
        Assert.Equal(new DateTime(2025, 1, 1), period.DateFrom);
        Assert.Equal(new DateTime(2025, 12, 31), period.DateTo);
    }

    [Theory]
    [InlineData("top customers Q2 2025")]
    [InlineData("supplier purchases Q2 2025")]
    [InlineData("product monthly Q2 2025")]
    public void Cross_phrase_Q2_2025_is_consistent(string phrase)
    {
        Assert.True(InsightDateRangeParser.TryResolvePeriod(phrase, 2025, Ref, out var period));
        Assert.Equal(new DateTime(2025, 4, 1), period.DateFrom);
        Assert.Equal(new DateTime(2025, 6, 30), period.DateTo);
    }
}
