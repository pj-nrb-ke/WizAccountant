using System.Data.SqlClient;
using System.Text.Json;
using System.Text.RegularExpressions;
using WizAccountant.Contracts;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-PURCHASE-ITEM-PERIOD-SUMMARY-001 — purchased qty/value grouped by quarter or month for stock item(s).</summary>
internal static class PurchaseProductQuarterlyHandler
{
    public const string QuerySerial = "SAGE-PURCHASE-ITEM-PERIOD-SUMMARY-001";
    public const string Operation = "purchase.item.period.summary";
    public const string LegacyOperation = "purchase.product.quarterly";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrWhiteSpace(companyConnectionString))
            throw new InvalidOperationException("Sage company database connection is not configured.");

        var period = InsightDateRangeParser.ResolvePeriod(parameters);
        var year = period.DateFrom.Year;
        if (parameters.TryGetValue("year", out var y) && int.TryParse(y, out var parsedYear))
            year = parsedYear;

        using var conn = new SqlConnection(companyConnectionString);
        conn.Open();
        var productCodes = ResolveProductCodes(conn, parameters);
        if (productCodes.Count == 0)
            throw new InvalidOperationException("No stock item code matched — specify product codes (e.g. DRCPO01) or a product name such as CPO / Crude Palm Oil.");

        var lineCols = InvoiceLineSqlHelper.Resolve(conn);
        var groupBy = parameters.GetValueOrDefault("groupBy", "quarter").Equals("month", StringComparison.OrdinalIgnoreCase)
            ? "month"
            : "quarter";
        var requestedQuarters = groupBy == "quarter" ? ExtractRequestedQuarters(period) : [];
        var sql = groupBy == "month"
            ? BuildMonthlySql(lineCols, productCodes)
            : BuildQuarterlySql(lineCols, productCodes);
        var rows = new List<PeriodRow>();

        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 180 };
        cmd.Parameters.AddWithValue("@pYear", year);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var periodNo = Convert.ToInt32(reader["PeriodNo"]);
            if (groupBy == "quarter" && requestedQuarters.Count > 0 && !requestedQuarters.Contains(periodNo))
                continue;

            rows.Add(new PeriodRow(
                periodNo,
                reader["PeriodName"]?.ToString() ?? "",
                reader["ProductCode"]?.ToString() ?? "",
                reader["ProductName"]?.ToString() ?? "",
                Convert.ToDecimal(reader["TotalQuantity"]),
                Convert.ToDecimal(reader["TotalValue"])));
        }

        var byPeriod = rows
            .GroupBy(r => r.PeriodNo)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                periodNo = g.Key,
                periodName = g.First().PeriodName,
                quarterName = groupBy == "quarter" ? g.First().PeriodName : null,
                totalQuantity = g.Sum(x => x.TotalQuantity),
                totalValue = g.Sum(x => x.TotalValue),
                products = g.Select(p => new
                {
                    productCode = p.ProductCode,
                    productName = p.ProductName,
                    quantity = p.TotalQuantity,
                    value = p.TotalValue
                })
            })
            .ToList();

        var grandQty = byPeriod.Sum(q => q.totalQuantity);
        var grandValue = byPeriod.Sum(q => q.totalValue);
        var productLabel = string.Join(", ", productCodes);
        var periodLabel = requestedQuarters.Count is > 0 and < 4
            ? string.Join(", ", byPeriod.Select(q => q.periodName))
            : year.ToString();

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            operation = Operation,
            groupBy,
            year,
            productCodes,
            itemsIncluded = productCodes,
            dateFrom = new DateTime(year, 1, 1).ToString("yyyy-MM-dd"),
            dateTo = new DateTime(year, 12, 31).ToString("yyyy-MM-dd"),
            periodLabel,
            quarterlyBreakdown = byPeriod,
            periodBreakdown = byPeriod,
            totalQuantity = grandQty,
            totalValue = grandValue,
            qtyColumn = lineCols.QtyExpression,
            valueSource = lineCols.ValueSource,
            savedSqlReferenceTitle = parameters.GetValueOrDefault("savedSqlReferenceTitle"),
            finding = byPeriod.Count == 0
                ? $"No purchase quantity found for {productLabel} in {periodLabel}."
                : $"Purchase total for {productLabel} in {periodLabel}: {grandQty:N2} units ({grandValue:N2} value excl.).",
            note = "Purchases from InvNum GRV/PO lines (DocType 2,5) — not sales or supplier open balances.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }

    private static string BuildQuarterlySql(InvoiceLineSqlHelper.LineColumnMap lineCols, IReadOnlyList<string> productCodes)
    {
        var codeIn = string.Join(", ", productCodes.Select(c => $"'{c.Replace("'", "''")}'"));
        var qtyExpr = lineCols.QtyExpression.Replace("L.", "L.");
        return $"""
            SELECT
                CASE
                    WHEN MONTH(CAST(H.InvDate AS DATE)) BETWEEN 1 AND 3 THEN 1
                    WHEN MONTH(CAST(H.InvDate AS DATE)) BETWEEN 4 AND 6 THEN 2
                    WHEN MONTH(CAST(H.InvDate AS DATE)) BETWEEN 7 AND 9 THEN 3
                    WHEN MONTH(CAST(H.InvDate AS DATE)) BETWEEN 10 AND 12 THEN 4
                END AS PeriodNo,
                CASE
                    WHEN MONTH(CAST(H.InvDate AS DATE)) BETWEEN 1 AND 3 THEN CONCAT('Q1 ', CAST(YEAR(H.InvDate) AS VARCHAR(4)))
                    WHEN MONTH(CAST(H.InvDate AS DATE)) BETWEEN 4 AND 6 THEN CONCAT('Q2 ', CAST(YEAR(H.InvDate) AS VARCHAR(4)))
                    WHEN MONTH(CAST(H.InvDate AS DATE)) BETWEEN 7 AND 9 THEN CONCAT('Q3 ', CAST(YEAR(H.InvDate) AS VARCHAR(4)))
                    WHEN MONTH(CAST(H.InvDate AS DATE)) BETWEEN 10 AND 12 THEN CONCAT('Q4 ', CAST(YEAR(H.InvDate) AS VARCHAR(4)))
                END AS PeriodName,
                S.Code AS ProductCode,
                ISNULL(S.Description_1, S.Code) AS ProductName,
                SUM({qtyExpr}) AS TotalQuantity,
                SUM({lineCols.ValueExpression}) AS TotalValue
            FROM InvNum H
            INNER JOIN _btblInvoiceLines L ON L.iInvoiceID = H.AutoIndex
            INNER JOIN StkItem S ON S.StockLink = L.iStockCodeID
            WHERE YEAR(CAST(H.InvDate AS DATE)) = @pYear
              AND S.Code IN ({codeIn})
              AND {InvNumSqlHelper.DocStateAnalyticsExclusionFilter}
              AND {InvNumSqlHelper.PurchaseDocumentFilter}
              AND ISNULL(L.iStockCodeID, 0) > 0
            GROUP BY
                CASE
                    WHEN MONTH(CAST(H.InvDate AS DATE)) BETWEEN 1 AND 3 THEN 1
                    WHEN MONTH(CAST(H.InvDate AS DATE)) BETWEEN 4 AND 6 THEN 2
                    WHEN MONTH(CAST(H.InvDate AS DATE)) BETWEEN 7 AND 9 THEN 3
                    WHEN MONTH(CAST(H.InvDate AS DATE)) BETWEEN 10 AND 12 THEN 4
                END,
                CASE
                    WHEN MONTH(CAST(H.InvDate AS DATE)) BETWEEN 1 AND 3 THEN CONCAT('Q1 ', CAST(YEAR(H.InvDate) AS VARCHAR(4)))
                    WHEN MONTH(CAST(H.InvDate AS DATE)) BETWEEN 4 AND 6 THEN CONCAT('Q2 ', CAST(YEAR(H.InvDate) AS VARCHAR(4)))
                    WHEN MONTH(CAST(H.InvDate AS DATE)) BETWEEN 7 AND 9 THEN CONCAT('Q3 ', CAST(YEAR(H.InvDate) AS VARCHAR(4)))
                    WHEN MONTH(CAST(H.InvDate AS DATE)) BETWEEN 10 AND 12 THEN CONCAT('Q4 ', CAST(YEAR(H.InvDate) AS VARCHAR(4)))
                END,
                S.Code,
                S.Description_1
            ORDER BY PeriodNo, S.Code
            """;
    }

    private static string BuildMonthlySql(InvoiceLineSqlHelper.LineColumnMap lineCols, IReadOnlyList<string> productCodes)
    {
        var codeIn = string.Join(", ", productCodes.Select(c => $"'{c.Replace("'", "''")}'"));
        return $"""
            SELECT
                MONTH(CAST(H.InvDate AS DATE)) AS PeriodNo,
                CONCAT(DATENAME(MONTH, H.InvDate), ' ', CAST(YEAR(H.InvDate) AS VARCHAR(4))) AS PeriodName,
                S.Code AS ProductCode,
                ISNULL(S.Description_1, S.Code) AS ProductName,
                SUM({lineCols.QtyExpression}) AS TotalQuantity,
                SUM({lineCols.ValueExpression}) AS TotalValue
            FROM InvNum H
            INNER JOIN _btblInvoiceLines L ON L.iInvoiceID = H.AutoIndex
            INNER JOIN StkItem S ON S.StockLink = L.iStockCodeID
            WHERE YEAR(CAST(H.InvDate AS DATE)) = @pYear
              AND S.Code IN ({codeIn})
              AND {InvNumSqlHelper.DocStateAnalyticsExclusionFilter}
              AND {InvNumSqlHelper.PurchaseDocumentFilter}
              AND ISNULL(L.iStockCodeID, 0) > 0
            GROUP BY
                MONTH(CAST(H.InvDate AS DATE)),
                DATENAME(MONTH, H.InvDate),
                YEAR(H.InvDate),
                S.Code,
                S.Description_1
            ORDER BY PeriodNo, S.Code
            """;
    }

    internal static IReadOnlyList<string> ResolveProductCodes(SqlConnection conn, Dictionary<string, string> parameters)
    {
        if (parameters.TryGetValue("productCodes", out var raw) && !string.IsNullOrWhiteSpace(raw))
        {
            return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var message = parameters.GetValueOrDefault("message") ?? "";
        var explicitCodes = Regex.Matches(message, @"\b[A-Z]{2,}[A-Z0-9]*\d+\b")
            .Select(m => m.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (explicitCodes.Count > 0)
            return explicitCodes;

        var searchTerms = new List<string>();
        var lower = message.ToLowerInvariant();
        if (lower.Contains("cpo") || lower.Contains("crude palm oil") || lower.Contains("palm oil"))
        {
            searchTerms.Add("DRCPO%");
            searchTerms.Add("%Palm Oil%");
        }

        if (parameters.TryGetValue("productSearch", out var ps) && !string.IsNullOrWhiteSpace(ps))
            searchTerms.Add($"%{ps.Trim()}%");

        if (searchTerms.Count == 0)
            return [];

        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var term in searchTerms)
        {
            var sql = term.Contains('%')
                ? """
                  SELECT Code FROM StkItem
                  WHERE Code LIKE @pTerm OR Description_1 LIKE @pTerm
                  """
                : """
                  SELECT Code FROM StkItem WHERE Code = @pTerm
                  """;
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@pTerm", term);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                codes.Add(reader.GetString(0));
        }

        return codes.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static HashSet<int> ExtractRequestedQuarters(InsightPeriodResolution period)
    {
        var quarters = new HashSet<int>();
        foreach (var segment in period.Segments)
        {
            var q = ((segment.From.Month - 1) / 3) + 1;
            if (q is >= 1 and <= 4)
                quarters.Add(q);
        }

        return quarters;
    }

    private sealed record PeriodRow(
        int PeriodNo,
        string PeriodName,
        string ProductCode,
        string ProductName,
        decimal TotalQuantity,
        decimal TotalValue);
}
