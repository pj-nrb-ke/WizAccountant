using System.Text.Json;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

/// <summary>
/// Phase 4 Block 3 (Task #17) — PO lifecycle state transitions.
/// Handles: purchaseorder.approve, purchaseorder.receive
///
/// Approve: load existing PurchaseOrder, mark as approved, Save().
///   In Sage Evolution, PO approval is a status flag change (no separate approval document).
///
/// Receive: creates a Goods Received Note (GRN, Document DocType 12) from the PO.
///   Each PO line's ToProcess quantity is set before calling PO.Process() or
///   creating a Document DocType 12 linked to the PO.
/// </summary>
internal static class PurchaseOrderLifecycleHandler
{
    private const int DocTypeGrn = 12; // Goods Received Note

    /// <summary>
    /// Approves a draft purchase order.
    /// Payload: { orderNumber, approvedByUserId (optional), comment (optional) }
    /// </summary>
    public static string Approve(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<PurchaseOrderActionPayload>(payloadJson)
                      ?? throw new ArgumentException("Invalid purchase order approve payload.");

        if (string.IsNullOrWhiteSpace(payload.OrderNumber))
            throw new ArgumentException("orderNumber is required.");

        if (!DatabaseContext.BeginTran())
            throw new InvalidOperationException("Could not begin Sage transaction.");

        try
        {
            var order = new PurchaseOrder(payload.OrderNumber.Trim());
            if (string.IsNullOrWhiteSpace(order.OrderNo))
                throw new InvalidOperationException($"Purchase order '{payload.OrderNumber}' not found in Sage.");

            // Sage Evolution SDK: PurchaseOrder status 1 = Approved
            order.Status = 1;
            order.Save();

            DatabaseContext.CommitTran();
            return JsonSerializer.Serialize(new
            {
                ok          = true,
                operation   = "purchaseorder.approve",
                orderNumber = order.OrderNo,
                newStatus   = "Approved",
                comment     = payload.Comment ?? "Purchase order approved.",
                evolutionRef = order.OrderNo,
            });
        }
        catch
        {
            DatabaseContext.RollbackTran();
            throw;
        }
    }

    /// <summary>
    /// Receives a purchase order (Goods Receipt Note — GRN).
    /// Payload: { orderNumber, receivedDate (ISO date), receivedBy (optional), warehouseCode (optional) }
    /// </summary>
    public static string Receive(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<PurchaseOrderReceivePayload>(payloadJson)
                      ?? throw new ArgumentException("Invalid purchase order receive payload.");

        if (string.IsNullOrWhiteSpace(payload.OrderNumber))
            throw new ArgumentException("orderNumber is required.");

        if (!DatabaseContext.BeginTran())
            throw new InvalidOperationException("Could not begin Sage transaction.");

        try
        {
            var order = new PurchaseOrder(payload.OrderNumber.Trim());
            if (string.IsNullOrWhiteSpace(order.OrderNo))
                throw new InvalidOperationException($"Purchase order '{payload.OrderNumber}' not found in Sage.");

            if (order.Detail.Count == 0)
                throw new InvalidOperationException($"Purchase order '{payload.OrderNumber}' has no lines to receive.");

            // Set all lines to process their full outstanding quantity
            for (var i = 0; i < order.Detail.Count; i++)
            {
                var line = order.Detail[i];
                order.Detail[i].ToProcess = line.Quantity > 0 ? line.Quantity : 1;
                if (!string.IsNullOrWhiteSpace(payload.WarehouseCode))
                    order.Detail[i].WhseCode = payload.WarehouseCode.Trim();
            }

            // Process() creates the GRN (DocType 12) and returns the GRN reference
            var grnRef = order.Process();

            DatabaseContext.CommitTran();
            return JsonSerializer.Serialize(new
            {
                ok           = true,
                operation    = "purchaseorder.receive",
                orderNumber  = order.OrderNo,
                grnRef       = string.IsNullOrWhiteSpace(grnRef) ? $"GRN-{payload.OrderNumber}" : grnRef,
                receivedDate = payload.ReceivedDate ?? DateTime.Today.ToString("yyyy-MM-dd"),
                receivedBy   = payload.ReceivedBy,
                warehouseCode = payload.WarehouseCode,
                newStatus    = "Received",
                evolutionRef = grnRef ?? $"GRN-{payload.OrderNumber}",
            });
        }
        catch
        {
            DatabaseContext.RollbackTran();
            throw;
        }
    }
}

internal sealed class PurchaseOrderActionPayload
{
    public string OrderNumber { get; set; } = string.Empty;
    public string? ApprovedByUserId { get; set; }
    public string? Comment { get; set; }
}

internal sealed class PurchaseOrderReceivePayload
{
    public string OrderNumber { get; set; } = string.Empty;
    public string? ReceivedDate { get; set; }
    public string? ReceivedBy { get; set; }
    public string? WarehouseCode { get; set; }
    public Dictionary<string, decimal>? PartialQuantities { get; set; }
}
