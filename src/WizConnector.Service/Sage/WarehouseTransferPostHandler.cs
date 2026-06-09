using System.Text.Json;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

/// <summary>
/// Phase 4 Block 3 (Task #16) — posts a warehouse transfer in Sage Evolution.
/// Payload: { stockCode, fromWarehouse, toWarehouse, quantity, reference }
///
/// SDK: Document DocType 17 = Warehouse Transfer.
/// The Document.FromWhseCode / ToWhseCode properties identify source + destination.
/// Sage creates the matching inbound movement automatically when DocType 17 is saved.
/// </summary>
internal static class WarehouseTransferPostHandler
{
    private const int DocTypeTransfer = 17; // Warehouse Transfer

    public static string Post(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<WarehouseTransferPayload>(payloadJson)
                      ?? throw new ArgumentException("Invalid warehouse transfer payload.");

        if (string.IsNullOrWhiteSpace(payload.StockCode))
            throw new ArgumentException("stockCode is required.");
        if (string.IsNullOrWhiteSpace(payload.FromWarehouse))
            throw new ArgumentException("fromWarehouse is required.");
        if (string.IsNullOrWhiteSpace(payload.ToWarehouse))
            throw new ArgumentException("toWarehouse is required.");
        if (payload.Quantity <= 0)
            throw new ArgumentException("quantity must be positive.");
        if (string.Equals(payload.FromWarehouse, payload.ToWarehouse, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("fromWarehouse and toWarehouse must differ.");

        if (!DatabaseContext.BeginTran())
            throw new InvalidOperationException("Could not begin Sage transaction.");

        try
        {
            // Verify item exists before creating document
            var item = new InventoryItem(payload.StockCode.Trim());
            if (string.IsNullOrWhiteSpace(item.Code))
                throw new InvalidOperationException($"Stock item '{payload.StockCode}' not found in Sage.");

            var doc = new Document();
            doc.DocType      = DocTypeTransfer;
            doc.Reference    = payload.Reference?.Trim() ?? $"XFER-{payload.StockCode}";
            doc.Description  = $"Warehouse transfer: {payload.FromWarehouse} → {payload.ToWarehouse}";
            doc.FromWhseCode = payload.FromWarehouse.Trim();
            doc.ToWhseCode   = payload.ToWarehouse.Trim();

            var line = doc.Detail.New();
            line.StockCode = payload.StockCode.Trim();
            line.Quantity  = (double)payload.Quantity;
            line.WhseCode  = payload.FromWarehouse.Trim();

            doc.Save();

            DatabaseContext.CommitTran();
            return JsonSerializer.Serialize(new
            {
                ok            = true,
                operation     = "warehouse.transfer.post",
                stockCode     = payload.StockCode,
                fromWarehouse = payload.FromWarehouse,
                toWarehouse   = payload.ToWarehouse,
                quantity      = payload.Quantity,
                reference     = doc.Reference,
                evolutionRef  = doc.Reference,
            });
        }
        catch
        {
            DatabaseContext.RollbackTran();
            throw;
        }
    }
}

internal sealed class WarehouseTransferPayload
{
    public string StockCode { get; set; } = string.Empty;
    public string FromWarehouse { get; set; } = string.Empty;
    public string ToWarehouse { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Reference { get; set; }
}
