namespace WizAccountant.Api.Insight;

internal static class WarehouseTransferChatHelper
{
    public const string SummaryOperation = "warehouse.transfer.summary";
    public const string DetailOperation = "warehouse.transfer.detail";
    public const string TopOperation = "warehouse.transfer.top";
    public const string ByItemOperation = "warehouse.transfer.by.item";
    public const string ByWarehouseOperation = "warehouse.transfer.by.warehouse";

    public static string ResolveOperation(string m)
    {
        if (m.Contains("by item", StringComparison.Ordinal) ||
            m.Contains("per item", StringComparison.Ordinal) ||
            (m.Contains("product", StringComparison.Ordinal) && m.Contains("transfer", StringComparison.Ordinal)))
            return ByItemOperation;

        if (m.Contains("by warehouse", StringComparison.Ordinal) ||
            m.Contains("per warehouse", StringComparison.Ordinal))
            return ByWarehouseOperation;

        if (m.Contains("detail", StringComparison.Ordinal) ||
            (m.Contains("list", StringComparison.Ordinal) && m.Contains("transfer", StringComparison.Ordinal)))
            return DetailOperation;

        if (m.Contains("top", StringComparison.Ordinal) ||
            m.Contains("largest", StringComparison.Ordinal) ||
            m.Contains("biggest", StringComparison.Ordinal))
            return TopOperation;

        return SummaryOperation;
    }
}
