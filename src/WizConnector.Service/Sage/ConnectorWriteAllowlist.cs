namespace WizConnector.Service.Sage;

public static class ConnectorWriteAllowlist
{
    public static readonly HashSet<string> Operations = new(StringComparer.OrdinalIgnoreCase)
    {
        "gltransaction.post",
        "customertransaction.post",
        "suppliertransaction.post",
        "allocation.save",
        "customer.save",
        "supplier.save",
        "salesorder.save"
    };

    public static bool IsWrite(string operation) => Operations.Contains(operation);
}
