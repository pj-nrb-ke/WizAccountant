using System.Text.Json;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

/// <summary>
/// Phase 4 Block 3 (Task #16) — posts a stock adjustment (positive or negative) in Sage Evolution.
/// Payload: { stockCode, warehouseCode, quantity (non-zero), reference, reason (optional) }
///
/// SDK: Document DocType 10 = Inventory Adjustment Plus (received into stock)
///      Document DocType 11 = Inventory Adjustment Minus (written off / negative)
/// </summary>
internal static class InventoryAdjustmentPostHandler
{
    private const int DocTypeAdjPlus  = 10; // Adjustment Plus  (Inventory received)
    private const int DocTypeAdjMinus = 11; // Adjustment Minus (Inventory write-off)

    public static string Post(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<InventoryAdjustmentPayload>(payloadJson)
                      ?? throw new ArgumentException("Invalid inventory adjustment payload.");

        if (string.IsNullOrWhiteSpace(payload.StockCode))
            throw new ArgumentException("stockCode is required.");
        if (payload.Quantity == 0)
            throw new ArgumentException("quantity must be non-zero.");

        var warehouseCode = payload.WarehouseCode?.Trim();

        if (!DatabaseContext.BeginTran())
            throw new InvalidOperationException("Could not begin Sage transaction.");

        try
        {
            // Verify item exists
            var item = new InventoryItem(payload.StockCode.Trim());
            if (string.IsNullOrWhiteSpace(item.Code))
                throw new InvalidOperationException($"Stock item '{payload.StockCode}' not found in Sage.");

            var docType = payload.Quantity > 0 ? DocTypeAdjPlus : DocTypeAdjMinus;
            var doc = new Document();
            doc.DocType = docType;
            doc.Reference = payload.Reference?.Trim() ?? $"ADJ-{payload.StockCode}";
            doc.Description = payload.Reason ?? (payload.Quantity > 0
                ? "Inventory adjustment plus (WizAccountant)"
                : "Inventory adjustment minus (WizAccountant)");

            var line = doc.Detail.New();
            line.StockCode   = payload.StockCode.Trim();
            line.Quantity    = (double)Math.Abs(payload.Quantity);
            line.UnitCost    = 0; // 0 = use current average cost
            if (!string.IsNullOrWhiteSpace(warehouseCode))
                line.WhseCode = warehouseCode;

            doc.Save();

            DatabaseContext.CommitTran();
            return JsonSerializer.Serialize(new
            {
                ok           = true,
                operation    = "inventory.adjustment.post",
                stockCode    = payload.StockCode,
                warehouseCode,
                quantity     = payload.Quantity,
                direction    = payload.Quantity > 0 ? "plus" : "minus",
                docType,
                reference    = doc.Reference,
                evolutionRef = doc.Reference,
            });
        }
        catch
        {
            DatabaseContext.RollbackTran();
            throw;
        }
    }
}

internal sealed class InventoryAdjustmentPayload
{
    public string StockCode { get; set; } = string.Empty;
    public string? WarehouseCode { get; set; }
    public decimal Quantity { get; set; }
    public string? Reference { get; set; }
    public string? Reason { get; set; }
}
