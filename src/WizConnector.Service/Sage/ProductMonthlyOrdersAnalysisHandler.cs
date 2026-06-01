using System.Data.SqlClient;
using System.Globalization;
using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-PATCH-009 — product-wise monthly sales/order quantity and value from posted sales invoices.</summary>
internal static class ProductMonthlyOrdersAnalysisHandler
{
    public const string QuerySerial = "SAGE-PRODUCT-MONTHLY-ORDERS-001";
    public const string Operation = "product.monthly.orders.analysis";

    private const string AnalysisSql = """
        SELECT
            YEAR(CAST(H.InvDate AS DATE)) AS SalesYear,
            MONTH(CAST(H.InvDate AS DATE)) AS SalesMonth,
            SI.Code AS ProductCode,
            ISNULL(SI.Description_1, SI.Code) AS ProductName,
            SUM(ABS(ISNULL(L.fQtyChange, 0))) AS TotalQuantity,
            SUM(ISNULL(L.fLineTotExcl, 0)) AS TotalValue
        FROM InvNum H
        INNER JOIN _btblInvoiceLines L ON L.iInvoiceID = H.AutoIndex
        INNER JOIN StkItem SI ON SI.StockLink = L.iStockCodeID
        WHERE CAST(H.InvDate AS DATE) >= @pDateFrom
          AND CAST(H.InvDate AS DATE) <= @pDateTo
          AND (H.DocType = 4 OR H.DocType IN (0, 4))
          AND ISNULL(L.iStockCodeID, 0) > 0
        GROUP BY
            YEAR(CAST(H.InvDate AS DATE)),
            MONTH(CAST(H.InvDate AS DATE)),
            SI.Code,
            SI.Description_1
        ORDER BY TotalQuantity DESC, TotalValue DESC;
        """;

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrWhiteSpace(companyConnectionString))
            throw new InvalidOperationException("Sage company database connection is not configured.");

        var topProducts = InvNumSqlHelper.ParseTop(parameters, 10);
        var (from, to) = InvNumSqlHelper.ParseDateFromOnward(parameters, parameters.GetValueOrDefault("message"));
        var rankByValue = parameters.GetValueOrDefault("rankBy", "quantity")
            .Equals("value", StringComparison.OrdinalIgnoreCase);

        var monthlyRows = new List<MonthlyRow>();
        using (var conn = new SqlConnection(companyConnectionString))
        {
            conn.Open();
            using var cmd = new SqlCommand(AnalysisSql, conn) { CommandTimeout = 180 };
            InvNumSqlHelper.AddDateParameters(cmd, from, to);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                monthlyRows.Add(new MonthlyRow(
                    Convert.ToInt32(reader["SalesYear"]),
                    Convert.ToInt32(reader["SalesMonth"]),
                    reader["ProductCode"]?.ToString() ?? "",
                    reader["ProductName"]?.ToString() ?? "",
                    Convert.ToDecimal(reader["TotalQuantity"]),
                    Convert.ToDecimal(reader["TotalValue"])));
            }
        }

        var productTotals = monthlyRows
            .GroupBy(r => (r.ProductCode, r.ProductName))
            .Select(g => new ProductTotal(
                g.Key.ProductCode,
                g.Key.ProductName,
                g.Sum(x => x.Quantity),
                g.Sum(x => x.Value)))
            .OrderByDescending(p => rankByValue ? p.TotalValue : p.TotalQuantity)
            .ThenByDescending(p => rankByValue ? p.TotalQuantity : p.TotalValue)
            .ToList();

        var topCodes = productTotals.Take(topProducts).Select(p => p.ProductCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var detailRows = monthlyRows
            .Where(r => topCodes.Contains(r.ProductCode))
            .OrderBy(r => r.Year).ThenBy(r => r.Month)
            .ThenByDescending(r => rankByValue ? r.Value : r.Quantity)
            .Select(r => new
            {
                month = FormatMonth(r.Year, r.Month),
                productCode = r.ProductCode,
                productName = r.ProductName,
                quantity = r.Quantity,
                value = r.Value
            })
            .ToList();

        var top = productTotals.FirstOrDefault();
        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            operation = Operation,
            dateFrom = from.ToString("yyyy-MM-dd"),
            dateTo = to.ToString("yyyy-MM-dd"),
            rankBy = rankByValue ? "value" : "quantity",
            requestedTopProducts = topProducts,
            evidenceNote = "Using posted sales invoices (InvNum + _btblInvoiceLines) as order/sales evidence — not open AR or stock master list.",
            topProductByQuantity = top is null ? null : new
            {
                top.ProductCode,
                top.ProductName,
                top.TotalQuantity,
                top.TotalValue
            },
            productTotals = productTotals.Take(topProducts).Select((p, i) => new
            {
                rank = i + 1,
                p.ProductCode,
                p.ProductName,
                p.TotalQuantity,
                p.TotalValue
            }),
            monthlyBreakdown = detailRows,
            finding = top is null
                ? $"No product invoice lines found from {from:yyyy-MM-dd} to {to:yyyy-MM-dd}."
                : $"Top product by {(rankByValue ? "value" : "quantity")}: {top.ProductCode} — {top.ProductName}.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }

    private static string FormatMonth(int year, int month) =>
        new DateTime(year, month, 1).ToString("MMM yyyy", CultureInfo.InvariantCulture);

    private sealed record MonthlyRow(int Year, int Month, string ProductCode, string ProductName, decimal Quantity, decimal Value);

    private sealed record ProductTotal(string ProductCode, string ProductName, decimal TotalQuantity, decimal TotalValue);
}
