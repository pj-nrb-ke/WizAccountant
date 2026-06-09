using System.Text.Json;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

/// <summary>
/// Phase 4 Block 3 (Task #16) — posts a sales credit note in Sage Evolution.
/// Payload: { customerCode, reference, amount, originalInvoiceRef (optional), reason (optional) }
///
/// SDK: Document DocType 1 = Sales Credit Note (debtor credit).
/// The credit reduces the debtor's balance without requiring a matched invoice.
/// If originalInvoiceRef is supplied it is stored in the document description for audit.
/// </summary>
internal static class SalesCreditNotePostHandler
{
    private const int DocTypeSalesCN = 1; // Sales Credit Note

    public static string Post(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<CreditNotePayload>(payloadJson)
                      ?? throw new ArgumentException("Invalid sales credit note payload.");

        if (string.IsNullOrWhiteSpace(payload.CustomerCode))
            throw new ArgumentException("customerCode is required.");
        if (payload.Amount <= 0)
            throw new ArgumentException("amount must be positive.");

        if (!DatabaseContext.BeginTran())
            throw new InvalidOperationException("Could not begin Sage transaction.");

        try
        {
            var customer = new Customer(payload.CustomerCode.Trim());
            if (string.IsNullOrWhiteSpace(customer.Code))
                throw new InvalidOperationException($"Customer '{payload.CustomerCode}' not found in Sage.");

            var doc = new Document();
            doc.DocType     = DocTypeSalesCN;
            doc.AccountCode = payload.CustomerCode.Trim();
            doc.Reference   = payload.Reference?.Trim() ?? $"CN-{payload.CustomerCode}";
            doc.Description = payload.Reason
                              ?? (payload.OriginalInvoiceRef is not null
                                  ? $"Credit re invoice {payload.OriginalInvoiceRef}"
                                  : "Sales credit note (WizAccountant)");

            var line = doc.Detail.New();
            line.Description       = doc.Description;
            line.UnitSellingPrice  = (double)payload.Amount;
            line.Quantity          = 1;

            doc.Save();

            DatabaseContext.CommitTran();
            return JsonSerializer.Serialize(new
            {
                ok                = true,
                operation         = "salescreditnote.post",
                customerCode      = payload.CustomerCode,
                reference         = doc.Reference,
                amount            = payload.Amount,
                originalInvoiceRef = payload.OriginalInvoiceRef,
                evolutionRef      = doc.Reference,
            });
        }
        catch
        {
            DatabaseContext.RollbackTran();
            throw;
        }
    }
}

internal sealed class CreditNotePayload
{
    public string CustomerCode { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public decimal Amount { get; set; }
    public string? OriginalInvoiceRef { get; set; }
    public string? Reason { get; set; }
}
