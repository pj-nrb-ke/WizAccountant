using WizAccountant.Contracts;

namespace WizConnector.Service.Sage;

/// <summary>Warehouse transfers via inventory TrCodes WHT/WHTC (SAGE-DISCOVERY-001).</summary>
internal static class WarehouseTransferEngine
{
    public const string QuerySerial = "SAGE-WH-TRANSFER-001";

    private static readonly string SummaryByWarehouseSql = $"""
        SELECT
            ISNULL(T.WarehouseCode, CAST(T.WarehouseID AS VARCHAR(20))) AS WarehouseCode,
            COUNT(*) AS TransferLineCount,
            SUM(ABS(ISNULL(T.TransQtyOut, 0))) AS TransferQtyOut
        FROM _bvSTTransactionsFull T
        WHERE CAST(T.TxDate AS DATE) >= @pDateFrom
          AND CAST(T.TxDate AS DATE) <= @pDateTo
          AND {SageTrCodeSqlHelper.WarehouseTransferOutboundFilter}
        GROUP BY ISNULL(T.WarehouseCode, CAST(T.WarehouseID AS VARCHAR(20)))
        ORDER BY SUM(ABS(ISNULL(T.TransQtyOut, 0))) DESC
        """;

    private static readonly string DetailSql = $"""
        SELECT TOP (@pTop)
            o.TxDate,
            o.Reference,
            o.FromWarehouse,
            dest.ToWarehouse,
            o.Description,
            ISNULL(S.Code, CAST(o.AccountLink AS VARCHAR(20))) AS ProductCode,
            o.Qty
        FROM (
            SELECT
                T.TxDate,
                T.Reference,
                T.WarehouseCode AS FromWarehouse,
                T.Description,
                T.AccountLink,
                ABS(ISNULL(T.TransQtyOut, 0)) AS Qty
            FROM _bvSTTransactionsFull T
            WHERE CAST(T.TxDate AS DATE) >= @pDateFrom
              AND CAST(T.TxDate AS DATE) <= @pDateTo
              AND {SageTrCodeSqlHelper.WarehouseTransferOutboundFilter}
        ) o
        OUTER APPLY (
            SELECT TOP 1 i.WarehouseCode AS ToWarehouse
            FROM _bvSTTransactionsFull i
            WHERE {SageTrCodeSqlHelper.WarehouseTransferTrCodeFilter}
              AND i.Reference = o.Reference
              AND CAST(i.TxDate AS DATE) = CAST(o.TxDate AS DATE)
              AND ISNULL(i.TransQtyIn, 0) = o.Qty
              AND ISNULL(i.WarehouseCode, '') <> ISNULL(o.FromWarehouse, '')
        ) dest
        LEFT JOIN StkItem S ON S.StockLink = o.AccountLink
        ORDER BY o.TxDate DESC, o.Qty DESC
        """;

    private static readonly string TopTransfersSql = $"""
        SELECT TOP (@pTop)
            o.Reference,
            o.TxDate,
            o.FromWarehouse,
            dest.ToWarehouse,
            SUM(o.Qty) AS TransferQty
        FROM (
            SELECT
                T.TxDate,
                T.Reference,
                T.WarehouseCode AS FromWarehouse,
                ABS(ISNULL(T.TransQtyOut, 0)) AS Qty
            FROM _bvSTTransactionsFull T
            WHERE CAST(T.TxDate AS DATE) >= @pDateFrom
              AND CAST(T.TxDate AS DATE) <= @pDateTo
              AND {SageTrCodeSqlHelper.WarehouseTransferOutboundFilter}
        ) o
        OUTER APPLY (
            SELECT TOP 1 i.WarehouseCode AS ToWarehouse
            FROM _bvSTTransactionsFull i
            WHERE {SageTrCodeSqlHelper.WarehouseTransferTrCodeFilter}
              AND i.Reference = o.Reference
              AND CAST(i.TxDate AS DATE) = CAST(o.TxDate AS DATE)
              AND ISNULL(i.TransQtyIn, 0) = o.Qty
              AND ISNULL(i.WarehouseCode, '') <> ISNULL(o.FromWarehouse, '')
        ) dest
        GROUP BY o.Reference, o.TxDate, o.FromWarehouse, dest.ToWarehouse
        ORDER BY SUM(o.Qty) DESC
        """;

    private static readonly string ByItemSql = $"""
        SELECT TOP (@pTop)
            ISNULL(S.Code, CAST(T.AccountLink AS VARCHAR(20))) AS ProductCode,
            ISNULL(S.Description_1, S.Code) AS ProductName,
            COUNT(*) AS TransferLineCount,
            SUM(ABS(ISNULL(T.TransQtyOut, 0))) AS TransferQty
        FROM _bvSTTransactionsFull T
        LEFT JOIN StkItem S ON S.StockLink = T.AccountLink
        WHERE CAST(T.TxDate AS DATE) >= @pDateFrom
          AND CAST(T.TxDate AS DATE) <= @pDateTo
          AND {SageTrCodeSqlHelper.WarehouseTransferOutboundFilter}
        GROUP BY ISNULL(S.Code, CAST(T.AccountLink AS VARCHAR(20))), ISNULL(S.Description_1, S.Code)
        ORDER BY SUM(ABS(ISNULL(T.TransQtyOut, 0))) DESC
        """;

    public sealed record LoadResult(
        InsightPeriodResolution Period,
        IReadOnlyList<Dictionary<string, object?>> ByWarehouse,
        IReadOnlyList<Dictionary<string, object?>> Detail,
        IReadOnlyList<Dictionary<string, object?>> TopTransfers,
        IReadOnlyList<Dictionary<string, object?>> ByItem);

    public static LoadResult Load(
        string connectionString,
        Dictionary<string, string> parameters,
        bool includeSummary,
        bool includeDetail,
        bool includeTop,
        bool includeByItem,
        int top)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Sage company database connection is not configured.");

        var period = InsightDateRangeParser.ResolvePeriod(parameters);
        var (from, to) = (period.DateFrom, period.DateTo);

        var byWarehouse = includeSummary
            ? GlSqlHelper.ExecuteQuery(connectionString, SummaryByWarehouseSql, cmd =>
                InvNumSqlHelper.AddDateParameters(cmd, from, to))
            : [];

        var detail = includeDetail
            ? GlSqlHelper.ExecuteQuery(connectionString, DetailSql, cmd =>
            {
                cmd.Parameters.AddWithValue("@pTop", top);
                InvNumSqlHelper.AddDateParameters(cmd, from, to);
            })
            : [];

        var topTransfers = includeTop
            ? GlSqlHelper.ExecuteQuery(connectionString, TopTransfersSql, cmd =>
            {
                cmd.Parameters.AddWithValue("@pTop", top);
                InvNumSqlHelper.AddDateParameters(cmd, from, to);
            })
            : [];

        var byItem = includeByItem
            ? GlSqlHelper.ExecuteQuery(connectionString, ByItemSql, cmd =>
            {
                cmd.Parameters.AddWithValue("@pTop", top);
                InvNumSqlHelper.AddDateParameters(cmd, from, to);
            })
            : [];

        return new LoadResult(period, byWarehouse, detail, topTransfers, byItem);
    }
}
