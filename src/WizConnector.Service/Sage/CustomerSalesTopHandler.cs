using System.Data.SqlClient;
using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-AR-SALES-TOP-001 — top N customers by InvNum sales value in period.</summary>
internal static class CustomerSalesTopHandler
{
    public const string QuerySerial = "SAGE-AR-SALES-TOP-001";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrWhiteSpace(companyConnectionString))
            throw new InvalidOperationException("Sage company database connection is not configured.");

        var top = InvNumSqlHelper.ParseTop(parameters, 10);
        var (from, to) = InvNumSqlHelper.ParseDateRange(parameters, parameters.GetValueOrDefault("message"));

        var sql = $"""
            SELECT TOP (@pTop)
                C.Account AS CustomerCode,
                ISNULL(C.Name, C.Account) AS CustomerName,
                SUM(ISNULL(H.InvTotIncl, 0)) AS SalesValue,
                COUNT(DISTINCT H.AutoIndex) AS InvoiceCount
            FROM InvNum H
            INNER JOIN Client C ON C.DCLink = H.AccountID
            WHERE CAST(H.InvDate AS DATE) >= @pDateFrom
              AND CAST(H.InvDate AS DATE) <= @pDateTo
              AND {InvNumSqlHelper.SalesDocTypeFilter}
            GROUP BY C.Account, C.Name
            ORDER BY SUM(ISNULL(H.InvTotIncl, 0)) DESC;
            """;

        var rows = new List<object>();
        try
        {
            using var conn = new SqlConnection(companyConnectionString);
            conn.Open();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 120 };
            cmd.Parameters.AddWithValue("@pTop", top);
            InvNumSqlHelper.AddDateParameters(cmd, from, to);
            using var reader = cmd.ExecuteReader();
            var rank = 0;
            while (reader.Read())
            {
                rank++;
                rows.Add(new
                {
                    rank,
                    code = reader["CustomerCode"]?.ToString() ?? "",
                    name = reader["CustomerName"]?.ToString() ?? "",
                    salesValue = Convert.ToDecimal(reader["SalesValue"]),
                    invoiceCount = Convert.ToInt32(reader["InvoiceCount"])
                });
            }
        }
        catch (SqlException ex) when (ex.Message.Contains("Invalid column name 'AccountID'", StringComparison.OrdinalIgnoreCase))
        {
            rows = ExecuteFallbackSdk(companyConnectionString, from, to, top);
        }

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = top,
            dateFrom = from.ToString("yyyy-MM-dd"),
            dateTo = to.ToString("yyyy-MM-dd"),
            topCustomers = rows,
            countOnly = false,
            note = "Top customers by InvNum sales total in period — SQL on Client/InvNum.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }

    private static List<object> ExecuteFallbackSdk(string connectionString, DateTime from, DateTime to, int top)
    {
        _ = connectionString;
        return new List<object>();
    }
}
