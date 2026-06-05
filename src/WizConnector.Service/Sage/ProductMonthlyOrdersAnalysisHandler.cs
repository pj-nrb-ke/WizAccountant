using System.Data.SqlClient;
using System.Globalization;
using System.Text.Json;
using WizAccountant.Contracts;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-PATCH-009 — product-wise monthly sales/order quantity and value from posted sales invoices.</summary>
internal static class ProductMonthlyOrdersAnalysisHandler
{
    public const string QuerySerial = "SAGE-PRODUCT-MONTHLY-ORDERS-001";
    public const string Operation = "product.monthly.orders.analysis";

    private static string BuildAnalysisSql(InvoiceLineSqlHelper.LineColumnMap lineCols) => $"""
        SELECT
            YEAR(CAST(H.InvDate AS DATE)) AS SalesYear,
            DATENAME(MONTH, H.InvDate) AS SalesMonthName,
            MONTH(CAST(H.InvDate AS DATE)) AS SalesMonth,
            SI.Code AS ProductCode,
            ISNULL(SI.Description_1, SI.Code) AS ProductName,
            SUM({lineCols.QtyExpression}) AS TotalQuantity,
            SUM({lineCols.ValueExpression}) AS TotalValue
        FROM _btblInvoiceLines L
        INNER JOIN InvNum H ON H.AutoIndex = L.iInvoiceID
        INNER JOIN StkItem SI ON SI.StockLink = L.iStockCodeID
        WHERE CAST(H.InvDate AS DATE) >= @pDateFrom
          AND CAST(H.InvDate AS DATE) <= @pDateTo
          AND {InvNumSqlHelper.DocStateAnalyticsExclusionFilter}
          AND {InvNumSqlHelper.SalesDocTypeFilter}
          AND ISNULL(L.iStockCodeID, 0) > 0
        GROUP BY
            YEAR(CAST(H.InvDate AS DATE)),
            MONTH(CAST(H.InvDate AS DATE)),
            DATENAME(MONTH, H.InvDate),
            SI.Code,
            SI.Description_1
        ORDER BY TotalQuantity DESC, TotalValue DESC, SalesYear, SalesMonth;
        """;

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrWhiteSpace(companyConnectionString))
            throw new InvalidOperationException("Sage company database connection is not configured.");

        var topProducts = InvNumSqlHelper.ParseTop(parameters, 10);
        var period = InsightDateRangeParser.ResolvePeriod(parameters);
        var from = period.DateFrom;
        var to = period.DateTo;
        var rankByValue = parameters.GetValueOrDefault("rankBy", "quantity")
            .Equals("value", StringComparison.OrdinalIgnoreCase);
        var savedRefTitle = parameters.GetValueOrDefault("savedSqlReferenceTitle");
        var gridCap = parameters.TryGetValue("top", out var topRaw) && int.TryParse(topRaw, out var topN)
            ? Math.Clamp(topN, 1, 500)
            : 500;

        var segments = !period.IsContiguous && period.Segments.Count > 0
            ? period.Segments
            : [new InsightPeriodSegment { From = from, To = to, Label = period.OriginalText }];

        var monthlyRows = new List<MonthlyRow>();
        string valueSource = "unknown";
        using (var conn = new SqlConnection(companyConnectionString))
        {
            conn.Open();
            var lineCols = InvoiceLineSqlHelper.Resolve(conn);
            valueSource = lineCols.ValueSource;
            var sql = BuildAnalysisSql(lineCols);
            foreach (var segment in segments)
            {
                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 180 };
                InvNumSqlHelper.AddDateParameters(cmd, segment.From, segment.To);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    monthlyRows.Add(new MonthlyRow(
                        Convert.ToInt32(reader["SalesYear"]),
                        Convert.ToInt32(reader["SalesMonth"]),
                        reader["SalesMonthName"]?.ToString() ?? "",
                        reader["ProductCode"]?.ToString() ?? "",
                        reader["ProductName"]?.ToString() ?? "",
                        Convert.ToDecimal(reader["TotalQuantity"]),
                        Convert.ToDecimal(reader["TotalValue"]),
                        segments.Count > 1 ? segment.Label : null));
                }
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

        var detailRows = monthlyRows
            .Where(r => r.Quantity != 0 || r.Value != 0)
            .OrderByDescending(r => r.Quantity)
            .ThenByDescending(r => r.Value)
            .ThenBy(r => r.Year)
            .ThenBy(r => r.Month)
            .Take(gridCap)
            .Select(r => new
            {
                month = FormatMonth(r.Year, r.Month),
                salesYear = r.Year,
                salesMonth = r.Month,
                salesMonthName = r.MonthName,
                segmentLabel = r.SegmentLabel,
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
            periodType = parameters.GetValueOrDefault("periodType") ?? period.PeriodType,
            periodOriginalText = parameters.GetValueOrDefault("periodOriginalText") ?? period.OriginalText,
            periodIsContiguous = period.IsContiguous,
            segments = period.Segments.Select(s => new { from = s.From.ToString("yyyy-MM-dd"), to = s.To.ToString("yyyy-MM-dd"), label = s.Label }),
            rankBy = rankByValue ? "value" : "quantity",
            requestedTopProducts = topProducts,
            evidenceNote = string.IsNullOrWhiteSpace(savedRefTitle)
                ? "Using posted sales invoices (InvNum + _btblInvoiceLines) as sold quantity/value — not open AR or stock master list."
                : $"Aligned with saved SQL reference “{savedRefTitle}” (InvNum + _btblInvoiceLines, excludes quote/template/cancelled DocState 2/5/6/7).",
            savedSqlReferenceTitle = savedRefTitle,
            lineValueSource = valueSource,
            topProductByQuantity = top is null ? null : new
            {
                productCode = top.ProductCode,
                productName = top.ProductName,
                totalQuantity = top.TotalQuantity,
                totalValue = top.TotalValue
            },
            productTotals = productTotals.Take(topProducts).Select((p, i) => new
            {
                rank = i + 1,
                productCode = p.ProductCode,
                productName = p.ProductName,
                totalQuantity = p.TotalQuantity,
                totalValue = p.TotalValue
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

    private sealed record MonthlyRow(
        int Year, int Month, string MonthName, string ProductCode, string ProductName,
        decimal Quantity, decimal Value, string? SegmentLabel);

    private sealed record ProductTotal(string ProductCode, string ProductName, decimal TotalQuantity, decimal TotalValue);
}
