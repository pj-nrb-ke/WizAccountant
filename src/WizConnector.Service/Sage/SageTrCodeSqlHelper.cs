namespace WizConnector.Service.Sage;

/// <summary>TrCode-based filters discovered from live Sage DB (SAGE-DISCOVERY-001).</summary>
internal static class SageTrCodeSqlHelper
{
    /// <summary>AR customer debit notes — TrCode DN, iModule 5 (AR).</summary>
    public const string PostArDebitNoteFilter = """
        EXISTS (
            SELECT 1 FROM TrCodes Tdn
            WHERE Tdn.idTrCodes = P.TrCodeID
              AND Tdn.Code = 'DN'
              AND Tdn.iModule = 5
        )
        """;

    /// <summary>Inventory warehouse transfers — TrCode WHT / WHTC, iModule 11.</summary>
    public const string WarehouseTransferTrCodeFilter = "T.TrCode IN ('WHT', 'WHTC')";

    public const string WarehouseTransferOutboundFilter =
        $"{WarehouseTransferTrCodeFilter} AND ISNULL(T.TransQtyOut, 0) > 0";
}
