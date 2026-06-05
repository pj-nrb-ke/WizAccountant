using WizAccountant.Contracts;

namespace WizAccountant.Insight.Intents.Tests;

public class InsightSqlGuardTests
{
    [Fact]
    public void Select_query_is_allowed()
    {
        Assert.True(InsightSqlGuard.IsReadOnlySelect("SELECT TOP 10 * FROM StkItem", out _));
    }

    [Fact]
    public void Cte_select_is_allowed()
    {
        Assert.True(InsightSqlGuard.IsReadOnlySelect(
            "WITH x AS (SELECT 1 AS n) SELECT * FROM x", out _));
    }

    [Fact]
    public void Delete_is_rejected()
    {
        Assert.False(InsightSqlGuard.IsReadOnlySelect("DELETE FROM StkItem", out var reason));
        Assert.Contains("SELECT", reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Multiple_statements_rejected()
    {
        Assert.False(InsightSqlGuard.IsReadOnlySelect("SELECT 1; SELECT 2", out var reason));
        Assert.Contains("single", reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Select_into_is_rejected()
    {
        Assert.False(InsightSqlGuard.IsReadOnlySelect(
            "SELECT Code INTO #tmp FROM StkItem", out _));
    }

    [Fact]
    public void Into_in_string_literal_is_allowed()
    {
        Assert.True(InsightSqlGuard.IsReadOnlySelect(
            "SELECT Code FROM StkItem WHERE Description_1 LIKE '%INTO%'", out _));
    }
}
