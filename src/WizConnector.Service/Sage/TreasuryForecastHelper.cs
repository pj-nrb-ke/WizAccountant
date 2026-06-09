using System.Data;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

internal static class TreasuryForecastHelper
{
    public static (decimal arOutstanding, int arLines) SumOpenAr()
    {
        var table = CustomerTransaction.List("Outstanding <> 0");
        if (table is null) return (0, 0);
        decimal total = 0;
        var count = 0;
        foreach (DataRow row in table.Rows)
        {
            var o = ArCustomerRankingHelper.ResolveOutstanding(row);
            if (o is null or <= 0) continue;
            total += o.Value;
            count++;
        }
        return (total, count);
    }

    /// <summary>Like SumOpenAr but also returns top-N customers by outstanding (GAP-030).</summary>
    public static (decimal arOutstanding, int arLines, List<(string Account, decimal Outstanding)> top) SumOpenArWithTop(int top = 5)
    {
        var table = CustomerTransaction.List("Outstanding <> 0");
        if (table is null) return (0, 0, []);
        decimal total = 0;
        var count = 0;
        var byAccount = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (DataRow row in table.Rows)
        {
            var o = ArCustomerRankingHelper.ResolveOutstanding(row);
            if (o is null or <= 0) continue;
            total += o.Value;
            count++;
            var acct = SageListHelpers.Col(row, "Account", "cAccount", "CustomerCode") ?? "";
            if (!string.IsNullOrWhiteSpace(acct))
                byAccount[acct] = byAccount.TryGetValue(acct, out var ex) ? ex + o.Value : o.Value;
        }
        var topList = byAccount.OrderByDescending(kv => kv.Value).Take(top).Select(kv => (kv.Key, kv.Value)).ToList();
        return (total, count, topList);
    }

    public static (decimal apOutstanding, int apLines) SumOpenAp()
    {
        var table = SupplierTransaction.List("Outstanding <> 0");
        if (table is null) return (0, 0);
        decimal total = 0;
        var count = 0;
        foreach (DataRow row in table.Rows)
        {
            var o = ApSupplierRankingHelper.ResolveOutstanding(row);
            if (o is null or <= 0) continue;
            total += o.Value;
            count++;
        }
        return (total, count);
    }

    /// <summary>Like SumOpenAp but also returns top-N suppliers by outstanding (GAP-030).</summary>
    public static (decimal apOutstanding, int apLines, List<(string Account, decimal Outstanding)> top) SumOpenApWithTop(int top = 5)
    {
        var table = SupplierTransaction.List("Outstanding <> 0");
        if (table is null) return (0, 0, []);
        decimal total = 0;
        var count = 0;
        var byAccount = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (DataRow row in table.Rows)
        {
            var o = ApSupplierRankingHelper.ResolveOutstanding(row);
            if (o is null or <= 0) continue;
            total += o.Value;
            count++;
            var acct = ApSupplierRankingHelper.ResolveSupplierCode(row) ?? "";
            if (!string.IsNullOrWhiteSpace(acct))
                byAccount[acct] = byAccount.TryGetValue(acct, out var ex) ? ex + o.Value : o.Value;
        }
        var topList = byAccount.OrderByDescending(kv => kv.Value).Take(top).Select(kv => (kv.Key, kv.Value)).ToList();
        return (total, count, topList);
    }

    public static decimal SumBankBalance(string connectionString)
    {
        var sql = $"""
            SELECT ISNULL(SUM({GlSqlHelper.NetValueExpr}), 0)
            FROM PostGL PG
            {GlSqlHelper.BankJoin}
            WHERE {GlSqlHelper.BankFilter}
              AND CAST(PG.TxDate AS DATE) <= CAST(GETDATE() AS DATE);
            """;
        return VatSqlHelper.RunScalar(connectionString, sql, DateTime.Today.AddYears(-10), DateTime.Today);
    }
}
