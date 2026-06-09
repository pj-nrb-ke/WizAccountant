using System.Text.Json;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

/// <summary>
/// Phase 4 Block 3 (Task #16) — posts a supplier credit note / return-to-supplier (RTS)
/// in Sage Evolution.
/// Payload: { supplierCode, reference, amount, originalPoRef (optional), reason (optional) }
///
/// SDK: Document DocType 3 = Purchase Return / Supplier Credit Note (creditor credit).
/// Reduces the creditor balance and records the return in the AP ledger.
/// </summary>
internal static class SupplierCreditNotePostHandler
{
    private const int DocTypeSupplierRTS = 3; // Purchase Return / Supplier Credit Note

    public static string Post(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<SupplierCreditNotePayload>(payloadJson)
                      ?? throw new ArgumentException("Invalid supplier credit note payload.");

        if (string.IsNullOrWhiteSpace(payload.SupplierCode))
            throw new ArgumentException("supplierCode is required.");
        if (payload.Amount <= 0)
            throw new ArgumentException("amount must be positive.");

        if (!DatabaseContext.BeginTran())
            throw new InvalidOperationException("Could not begin Sage transaction.");

        try
        {
            var supplier = new Supplier(payload.SupplierCode.Trim());
            if (string.IsNullOrWhiteSpace(supplier.Code))
                throw new InvalidOperationException($"Supplier '{payload.SupplierCode}' not found in Sage.");

            var doc = new Document();
            doc.DocType     = DocTypeSupplierRTS;
            doc.AccountCode = payload.SupplierCode.Trim();
            doc.Reference   = payload.Reference?.Trim() ?? $"RTS-{payload.SupplierCode}";
            doc.Description = payload.Reason
                              ?? (payload.OriginalPoRef is not null
                                  ? $"Return re PO {payload.OriginalPoRef}"
                                  : "Supplier credit note / RTS (WizAccountant)");

            var line = doc.Detail.New();
            line.Description  = doc.Description;
            line.UnitCost     = (double)payload.Amount;
            line.Quantity     = 1;

            doc.Save();

            DatabaseContext.CommitTran();
            return JsonSerializer.Serialize(new
            {
                ok             = true,
                operation      = "suppliercreditnote.post",
                supplierCode   = payload.SupplierCode,
                reference      = doc.Reference,
                amount         = payload.Amount,
                originalPoRef  = payload.OriginalPoRef,
                evolutionRef   = doc.Reference,
            });
        }
        catch
        {
            DatabaseContext.RollbackTran();
            throw;
        }
    }
}

internal sealed class SupplierCreditNotePayload
{
    public string SupplierCode { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public decimal Amount { get; set; }
    public string? OriginalPoRef { get; set; }
    public string? Reason { get; set; }
}
