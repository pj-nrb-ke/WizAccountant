using System.Data.SqlClient;
using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-INV-MOVE-TOP-001 — top items by stock movement quantity in period.</summary>
internal static class InventoryMovementTopHandler
{
    public const string QuerySerial = "SAGE-INV-MOVE-TOP-001";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrWhiteSpace(companyConnectionString))
            throw new InvalidOperationException("Sage company database connection is not configured.");

        var top = InvNumSqlHelper.ParseTop(parameters, 10);
        var (from, to) = InvNumSqlHelper.ParseDateRange(parameters, parameters.GetValueOrDefault("message"));
        var sql = """
            SELECT TOP (@pTop)
                SI.Code AS ItemCode,
                SI.Description_1 AS Description,
                SUM(ABS(ISNULL(T.TransQtyIn, 0)) + ABS(ISNULL(T.TransQtyOut, 0))) AS MovementQty
            FROM _bvSTTransactionsFull T
            INNER JOIN StkItem SI ON SI.StockLink = T.AccountLink
            WHERE CAST(T.TxDate AS DATE) >= @pDateFrom AND CAST(T.TxDate AS DATE) <= @pDateTo
            GROUP BY SI.Code, SI.Description_1
            ORDER BY SUM(ABS(ISNULL(T.TransQtyIn, 0)) + ABS(ISNULL(T.TransQtyOut, 0))) DESC;
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
                code = reader["ItemCode"]?.ToString(),
                description = reader["Description"]?.ToString(),
                movementQty = Convert.ToDecimal(reader["MovementQty"])
            });
        }

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = top,
            items = rows,
            dateFrom = from.ToString("yyyy-MM-dd"),
            dateTo = to.ToString("yyyy-MM-dd"),
            note = "Top items by stock transaction movement quantity in period (_bvSTTransactionsFull).",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
