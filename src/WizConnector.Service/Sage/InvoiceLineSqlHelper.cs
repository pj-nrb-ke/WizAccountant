using System.Collections.Concurrent;
using System.Data.SqlClient;

namespace WizConnector.Service.Sage;

/// <summary>Resolves _btblInvoiceLines column names per company DB (Evolution versions differ).</summary>
internal static class InvoiceLineSqlHelper
{
    private const string TableName = "_btblInvoiceLines";
    private static readonly ConcurrentDictionary<string, LineColumnMap> Cache = new(StringComparer.OrdinalIgnoreCase);

    internal sealed record LineColumnMap(string QtyExpression, string ValueExpression, string ValueSource);

    internal sealed record InvoiceLineSqlHint(
        string TableName,
        IReadOnlyList<string> Columns,
        string QtyColumn,
        string QtyExpression,
        string ValueExpression,
        string ValueSource,
        string SampleProductMonthlySql);

    public static InvoiceLineSqlHint BuildHint(SqlConnection conn)
    {
        var cols = LoadColumns(conn, TableName);
        var map = BuildMap(cols);
        var qtyCol = FirstPresent(cols, "fQtyChange", "fQuantity") ?? "unknown";
        var qtyExpr = map.QtyExpression.Replace("L.", "l.", StringComparison.Ordinal);
        var valueExpr = map.ValueExpression.Replace("L.", "l.", StringComparison.Ordinal);
        var sample = BuildSampleProductMonthlySql(qtyExpr, valueExpr);
        return new InvoiceLineSqlHint(
            TableName,
            cols.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList(),
            qtyCol,
            qtyExpr,
            valueExpr,
            map.ValueSource,
            sample);
    }

    private static string BuildSampleProductMonthlySql(string qtyExpr, string valueExpr) => $"""
        SELECT
            YEAR(h.InvDate) AS SalesYear,
            DATENAME(MONTH, h.InvDate) AS SalesMonth,
            MONTH(h.InvDate) AS MonthNo,
            s.Code AS ProductCode,
            s.Description_1 AS ProductName,
            SUM({qtyExpr}) AS TotalQtySold,
            SUM({valueExpr}) AS TotalSalesValue
        FROM _btblInvoiceLines l
        INNER JOIN InvNum h ON h.AutoIndex = l.iInvoiceID
        INNER JOIN StkItem s ON s.StockLink = l.iStockCodeID
        WHERE h.InvDate >= '2025-07-01'
          AND h.InvDate < '2026-01-01'
          AND {InvNumSqlHelper.DocStateAnalyticsExclusionFilterLowerH}
        GROUP BY
            YEAR(h.InvDate),
            MONTH(h.InvDate),
            DATENAME(MONTH, h.InvDate),
            s.Code,
            s.Description_1
        ORDER BY TotalQtySold DESC, TotalSalesValue DESC, SalesYear, MonthNo;
        """;

    public static LineColumnMap Resolve(SqlConnection conn)
    {
        var key = conn.ConnectionString ?? "default";
        return Cache.GetOrAdd(key, _ => BuildMap(LoadColumns(conn, TableName)));
    }

    internal static LineColumnMap BuildMap(IReadOnlySet<string> cols)
    {
        var qtyCol = FirstPresent(cols, "fQtyChange", "fQuantity");
        var qtyExpr = qtyCol is null ? "0" : $"ABS(ISNULL(L.{qtyCol}, 0))";

        foreach (var candidate in new[]
                 {
                     "fLineTotExcl",
                     "fLineTotExclAfterDisc",
                     "fLineTotalExcl",
                     "fLineTotExclAfterLineDisc",
                     "fLineTotInclExcl",
                 })
        {
            if (cols.Contains(candidate))
                return new LineColumnMap(qtyExpr, $"ISNULL(L.{candidate}, 0)", candidate);
        }

        if (cols.Contains("fUnitPriceExcl") && qtyCol is not null)
        {
            var value = $"ABS(ISNULL(L.{qtyCol}, 0)) * ISNULL(L.fUnitPriceExcl, 0)";
            if (cols.Contains("fLineDiscountAmntEx"))
                value += " - ISNULL(L.fLineDiscountAmntEx, 0)";
            else if (cols.Contains("fLineDiscountAmnt"))
                value += " - ISNULL(L.fLineDiscountAmnt, 0)";

            return new LineColumnMap(qtyExpr, value, "computed:fUnitPriceExcl*qty");
        }

        if (cols.Contains("fUnitPriceIncl") && qtyCol is not null)
        {
            var value = $"ABS(ISNULL(L.{qtyCol}, 0)) * ISNULL(L.fUnitPriceIncl, 0)";
            return new LineColumnMap(qtyExpr, value, "computed:fUnitPriceIncl*qty");
        }

        throw new InvalidOperationException(
            "Could not resolve invoice line value on _btblInvoiceLines — expected fLineTotExcl or fUnitPriceExcl with quantity.");
    }

    private static string? FirstPresent(IReadOnlySet<string> cols, params string[] names)
    {
        foreach (var name in names)
        {
            if (cols.Contains(name))
                return name;
        }

        return null;
    }

    private static HashSet<string> LoadColumns(SqlConnection conn, string table)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var cmd = new SqlCommand(
            """
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = @tableName
            """,
            conn);
        cmd.Parameters.AddWithValue("@tableName", table);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            set.Add(reader.GetString(0));
        return set;
    }
}
