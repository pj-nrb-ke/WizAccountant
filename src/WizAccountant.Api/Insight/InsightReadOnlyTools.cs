namespace WizAccountant.Api.Insight;

/// <summary>Phase 2 AI allowlist — read handlers only.</summary>
public static class InsightReadOnlyTools
{
    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "dashboard.summary",
        "search.global",
        "customer.list",
        "customer.get",
        "customer.openitems",
        "customertransaction.list",
        "customertransaction.get",
        "supplier.list",
        "supplier.get",
        "supplier.openitems",
        "suppliertransaction.list",
        "glaccount.list",
        "glaccount.get",
        "gltransaction.list",
        "salesorder.list",
        "purchaseorder.list",
        "inventoryitem.list",
        "inventoryitem.get",
        "project.list",
        "warehouse.list",
        "taxrate.list",
        "transactioncode.list",
        "site.health"
    };

    public static bool IsAllowed(string operation) => Allowed.Contains(operation);
}
