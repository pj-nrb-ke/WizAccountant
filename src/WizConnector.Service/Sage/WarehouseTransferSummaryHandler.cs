using System.Data.SqlClient;
using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>SAGE-WH-TRANSFER-001 — warehouse transfer activity summary for period.</summary>
internal static class WarehouseTransferSummaryHandler
{
    public const string QuerySerial = "SAGE-WH-TRANSFER-001";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrWhiteSpace(companyConnectionString))
            throw new InvalidOperationException("Sage company database connection is not configured.");

        var top = InvNumSqlHelper.ParseTop(parameters, 15);
        var (from, to) = InvNumSqlHelper.ParseDateRange(parameters, parameters.GetValueOrDefault("message"));
        var sql = """
            SELECT TOP (@pTop)
                ISNULL(W.Code, CAST(T.WarehouseID AS VARCHAR(20))) AS WarehouseCode,
                COUNT(*) AS TransferLineCount,
                SUM(ABS(ISNULL(T.TransQtyIn, 0)) + ABS(ISNULL(T.TransQtyOut, 0))) AS TransferQty
            FROM _bvSTTransactionsFull T
            LEFT JOIN WhseMst W ON W.WhseLink = T.WarehouseID
            WHERE CAST(T.TxDate AS DATE) >= @pDateFrom AND CAST(T.TxDate AS DATE) <= @pDateTo
              AND (
                    LOWER(ISNULL(T.Description, '')) LIKE '%transfer%'
                    OR LOWER(ISNULL(T.Reference, '')) LIKE '%transfer%'
                    OR LOWER(ISNULL(T.TrCode, '')) LIKE '%trf%'
                  )
            GROUP BY ISNULL(W.Code, CAST(T.WarehouseID AS VARCHAR(20)))
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
                warehouseCode = reader["WarehouseCode"]?.ToString(),
                transferLineCount = Convert.ToInt32(reader["TransferLineCount"]),
                transferQty = Convert.ToDecimal(reader["TransferQty"])
            });
        }

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            requestedTop = top,
            warehouses = rows,
            dateFrom = from.ToString("yyyy-MM-dd"),
            dateTo = to.ToString("yyyy-MM-dd"),
            note = "Warehouse transfer lines from stock transactions (transfer reference/description).",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
