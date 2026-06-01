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
