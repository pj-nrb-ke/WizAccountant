using System.Data.SqlClient;
using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-PUR-SUPPLIER-TOP-001 — top suppliers by purchase value in period (InvNum SQL).</summary>
internal static class SupplierPurchasesTopHandler
{
    public const string QuerySerial = "SAGE-PUR-SUPPLIER-TOP-001";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrWhiteSpace(companyConnectionString))
            throw new InvalidOperationException("Sage company database connection is not configured.");

        var top = InvNumSqlHelper.ParseTop(parameters, 10);
        var (from, to) = InvNumSqlHelper.ParseDateRange(parameters, parameters.GetValueOrDefault("message"));
        var sql = $"""
            SELECT TOP (@pTop)
                H.AccountID AS SupplierCode,
                SUM(ISNULL(H.InvTotIncl, 0)) AS PurchaseValue
            FROM InvNum H
            WHERE CAST(H.InvDate AS DATE) >= @pDateFrom AND CAST(H.InvDate AS DATE) <= @pDateTo
              AND {InvNumSqlHelper.PurchaseDocTypeFilter}
              AND ISNULL(H.AccountID, '') <> ''
            GROUP BY H.AccountID
            ORDER BY SUM(ISNULL(H.InvTotIncl, 0)) DESC;
            """;

        var rows = new List<object>();
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
                code = reader["SupplierCode"]?.ToString(),
                name = reader["SupplierCode"]?.ToString(),
                purchaseValue = Convert.ToDecimal(reader["PurchaseValue"])
            });
        }

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = top,
            topSuppliers = rows,
            dateFrom = from.ToString("yyyy-MM-dd"),
            dateTo = to.ToString("yyyy-MM-dd"),
            note = "Top suppliers by InvNum purchase total in period.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
