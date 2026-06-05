using System.Text.Json;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

internal static class SageSdkWriteHandlers
{
    public static string? TryExecute(
        string operation,
        Dictionary<string, string> parameters,
        bool writesEnabled,
        bool consentRequired,
        WriteConsentStore consent,
        IdempotencyStore idempotency)
    {
        if (!ConnectorWriteAllowlist.IsWrite(operation))
            return null;

        var idemKey = parameters.GetValueOrDefault("idempotencyKey");
        if (!string.IsNullOrWhiteSpace(idemKey) && idempotency.TryGetResult(idemKey, out var cached) && cached is not null)
            return cached;

        if (!writesEnabled)
            return Finish(idempotency, idemKey, JsonSerializer.Serialize(new
            {
                simulated = true,
                operation,
                message = "Writes are disabled. Set Connector:WritesEnabled=true after pilot approval."
            }));

        if (consentRequired && !consent.IsAllowed())
            return Finish(idempotency, idemKey, JsonSerializer.Serialize(new
            {
                error = "WRITE_CONSENT_REQUIRED",
                message = "Grant write consent from WizConnector Tray (Allow cloud posts)."
            }));

        var payloadJson = parameters.GetValueOrDefault("payload") ?? "{}";

        try
        {
            var result = operation.ToLowerInvariant() switch
            {
                "gltransaction.post" => PostGlJournal(payloadJson),
                "customertransaction.post" => PostCustomerTransaction(payloadJson),
                "suppliertransaction.post" => PostSupplierTransaction(payloadJson),
                "allocation.save" => SaveAllocation(payloadJson),
                "customer.save" => SaveCustomer(payloadJson),
                "supplier.save" => SaveSupplier(payloadJson),
                "salesorder.save" => SalesOrderSaveHandler.Save(payloadJson),
                _ => throw new NotSupportedException($"Write operation not implemented: {operation}")
            };
            return Finish(idempotency, idemKey, result);
        }
        catch (Exception ex)
        {
            var (code, message) = SageErrorMapper.Map(ex);
            var err = JsonSerializer.Serialize(new { error = code, message });
            return Finish(idempotency, idemKey, err);
        }
    }

    private static string Finish(IdempotencyStore idempotency, string? key, string json)
    {
        if (!string.IsNullOrWhiteSpace(key) && !json.Contains("\"error\""))
            idempotency.SaveResult(key, json);
        return json;
    }

    private static string PostGlJournal(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<GlJournalPayload>(payloadJson)
                      ?? throw new ArgumentException("Invalid GL journal payload.");

        if (payload.Lines.Count < 2)
            throw new ArgumentException("GL journal requires at least two lines.");

        var debit = payload.Lines.Sum(l => l.Debit);
        var credit = payload.Lines.Sum(l => l.Credit);
        if (Math.Abs(debit - credit) > 0.01m)
            throw new InvalidOperationException("Journal is not balanced.");

        if (!DatabaseContext.BeginTran())
            throw new InvalidOperationException("Could not begin Sage transaction.");

        try
        {
            var posted = new List<object>();

            foreach (var line in payload.Lines)
            {
                var tx = new GLTransaction
                {
                    Description = payload.Description ?? payload.Reference,
                    Reference = payload.Reference
                };
                if (line.Debit > 0) tx.Debit = (double)line.Debit;
                if (line.Credit > 0) tx.Credit = (double)line.Credit;
                if (!string.IsNullOrWhiteSpace(line.Account))
                    tx.Account = new GLAccount(line.Account);
                if (!tx.Post())
                    throw new InvalidOperationException($"GL post failed for account {line.Account}.");
                posted.Add(new { line.Account, line.Debit, line.Credit });
            }

            DatabaseContext.CommitTran();
            return JsonSerializer.Serialize(new
            {
                ok = true,
                operation = "gltransaction.post",
                reference = payload.Reference,
                lines = posted,
                evolutionRef = payload.Reference,
                rollbackNotice = "SDK posts are not auto-reversed by WizAccountant."
            });
        }
        catch
        {
            DatabaseContext.RollbackTran();
            throw;
        }
    }

    private static string PostCustomerTransaction(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<ArApPostPayload>(payloadJson)
                      ?? throw new ArgumentException("Invalid AR payload.");

        if (!DatabaseContext.BeginTran())
            throw new InvalidOperationException("Could not begin Sage transaction.");

        try
        {
            var tx = new CustomerTransaction
            {
                Customer = new Customer(payload.Account),
                Reference = payload.Reference,
                Description = payload.Description ?? payload.Reference,
                Debit = (double)payload.Amount
            };
            if (payload.TxDate is not null) tx.Date = payload.TxDate.Value;

            if (!tx.Post())
                throw new InvalidOperationException("Customer transaction post failed.");

            DatabaseContext.CommitTran();
            return JsonSerializer.Serialize(new
            {
                ok = true,
                operation = "customertransaction.post",
                account = payload.Account,
                reference = payload.Reference,
                amount = payload.Amount,
                evolutionRef = payload.Reference
            });
        }
        catch
        {
            DatabaseContext.RollbackTran();
            throw;
        }
    }

    private static string PostSupplierTransaction(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<ArApPostPayload>(payloadJson)
                      ?? throw new ArgumentException("Invalid AP payload.");

        if (!DatabaseContext.BeginTran())
            throw new InvalidOperationException("Could not begin Sage transaction.");

        try
        {
            var tx = new SupplierTransaction
            {
                Supplier = new Supplier(payload.Account),
                Reference = payload.Reference,
                Description = payload.Description ?? payload.Reference,
                Credit = (double)payload.Amount
            };
            if (payload.TxDate is not null) tx.Date = payload.TxDate.Value;

            if (!tx.Post())
                throw new InvalidOperationException("Supplier transaction post failed.");

            DatabaseContext.CommitTran();
            return JsonSerializer.Serialize(new
            {
                ok = true,
                operation = "suppliertransaction.post",
                account = payload.Account,
                reference = payload.Reference,
                amount = payload.Amount,
                evolutionRef = payload.Reference
            });
        }
        catch
        {
            DatabaseContext.RollbackTran();
            throw;
        }
    }

    private static string SaveAllocation(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<AllocationPayload>(payloadJson)
                      ?? throw new ArgumentException("Invalid allocation payload.");

        if (payload.Entries.Count == 0)
            throw new ArgumentException("At least one allocation entry is required.");

        _ = new Customer(payload.Account);

        // Evolution allocation API varies by version — record AR payment against open items via reference.
        if (!DatabaseContext.BeginTran())
            throw new InvalidOperationException("Could not begin Sage transaction.");

        try
        {
            foreach (var entry in payload.Entries)
            {
                var pay = new CustomerTransaction
                {
                    Customer = new Customer(payload.Account),
                    Reference = entry.Reference ?? payload.Reference ?? "ALLOC",
                    Description = "WizAccountant allocation",
                    Credit = (double)entry.Amount
                };
                if (!pay.Post())
                    throw new InvalidOperationException($"Allocation payment failed for {entry.Reference}.");
            }

            DatabaseContext.CommitTran();
            return JsonSerializer.Serialize(new
            {
                ok = true,
                operation = "allocation.save",
                account = payload.Account,
                entryCount = payload.Entries.Count,
                evolutionRef = payload.Reference
            });
        }
        catch
        {
            DatabaseContext.RollbackTran();
            throw;
        }
    }

    private static string SaveCustomer(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<MasterSavePayload>(payloadJson)
                      ?? throw new ArgumentException("Invalid customer save payload.");

        var customer = string.IsNullOrWhiteSpace(payload.Code) ? new Customer() : new Customer(payload.Code);
        customer.Description = payload.Name ?? customer.Description;
        if (!string.IsNullOrWhiteSpace(payload.Email)) customer.EmailAddress = payload.Email;
        customer.Save();

        return JsonSerializer.Serialize(new
        {
            ok = true,
            operation = "customer.save",
            code = customer.Code
        });
    }

    private static string SaveSupplier(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<MasterSavePayload>(payloadJson)
                      ?? throw new ArgumentException("Invalid supplier save payload.");

        var supplier = string.IsNullOrWhiteSpace(payload.Code) ? new Supplier() : new Supplier(payload.Code);
        supplier.Description = payload.Name ?? supplier.Description;
        if (!string.IsNullOrWhiteSpace(payload.Email)) supplier.EmailAddress = payload.Email;
        supplier.Save();

        return JsonSerializer.Serialize(new
        {
            ok = true,
            operation = "supplier.save",
            code = supplier.Code
        });
    }

    private sealed class GlJournalPayload
    {
        public string Reference { get; set; } = "";
        public string? Description { get; set; }
        public List<GlLinePayload> Lines { get; set; } = new();
    }

    private sealed class GlLinePayload
    {
        public string Account { get; set; } = "";
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }

    private sealed class ArApPostPayload
    {
        public string Account { get; set; } = "";
        public string Reference { get; set; } = "";
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public DateTime? TxDate { get; set; }
    }

    private sealed class AllocationPayload
    {
        public string Account { get; set; } = "";
        public string? Reference { get; set; }
        public List<AllocationLinePayload> Entries { get; set; } = new();
    }

    private sealed class AllocationLinePayload
    {
        public decimal Amount { get; set; }
        public string? Reference { get; set; }
    }

    private sealed class MasterSavePayload
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
    }
}
