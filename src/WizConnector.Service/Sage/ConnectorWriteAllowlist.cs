namespace WizConnector.Service.Sage;

public static class ConnectorWriteAllowlist
{
    public static readonly HashSet<string> Operations = new(StringComparer.OrdinalIgnoreCase)
    {
        // Phase 3 — original 7 allowlisted operations
        "gltransaction.post",
        "customertransaction.post",
        "suppliertransaction.post",
        "allocation.save",
        "customer.save",
        "supplier.save",
        "salesorder.save",

        // Phase 4 Block 3 — inventory writes (Task #16)
        "inventory.adjustment.post",
        "warehouse.transfer.post",
        "salescreditnote.post",
        "suppliercreditnote.post",

        // Phase 4 Block 3 — order lifecycle (Task #17)
        "salesorder.confirm",
        "salesorder.ship",
        "purchaseorder.approve",
        "purchaseorder.receive"
    };

    public static bool IsWrite(string operation) => Operations.Contains(operation);
}
