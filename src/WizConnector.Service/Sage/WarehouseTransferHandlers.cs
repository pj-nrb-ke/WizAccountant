using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class WarehouseTransferSummaryHandler
{
    public const string Operation = "warehouse.transfer.summary";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 15);
        var result = WarehouseTransferEngine.Load(companyConnectionString, parameters, true, false, false, false, top);
        return WarehouseTransferHandlerSupport.Serialize(result, Operation, top);
    }
}

internal static class WarehouseTransferDetailHandler
{
    public const string Operation = "warehouse.transfer.detail";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 25);
        var result = WarehouseTransferEngine.Load(companyConnectionString, parameters, false, true, false, false, top);
        return WarehouseTransferHandlerSupport.Serialize(result, Operation, top);
    }
}

internal static class WarehouseTransferTopHandler
{
    public const string Operation = "warehouse.transfer.top";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 15);
        var result = WarehouseTransferEngine.Load(companyConnectionString, parameters, false, false, true, false, top);
        return WarehouseTransferHandlerSupport.Serialize(result, Operation, top);
    }
}

internal static class WarehouseTransferByItemHandler
{
    public const string Operation = "warehouse.transfer.by.item";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 15);
        var result = WarehouseTransferEngine.Load(companyConnectionString, parameters, false, false, false, true, top);
        return WarehouseTransferHandlerSupport.Serialize(result, Operation, top);
    }
}

internal static class WarehouseTransferByWarehouseHandler
{
    public const string Operation = "warehouse.transfer.by.warehouse";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 15);
        var result = WarehouseTransferEngine.Load(companyConnectionString, parameters, true, false, false, false, top);
        return WarehouseTransferHandlerSupport.Serialize(result, Operation, top);
    }
}

internal static class WarehouseTransferHandlerSupport
{
    public static string Serialize(WarehouseTransferEngine.LoadResult result, string operation, int top)
    {
        var period = result.Period;
        return JsonSerializer.Serialize(new
        {
            querySerial = WarehouseTransferEngine.QuerySerial,
            operation,
            requestedTop = top,
            dateFrom = period.DateFrom.ToString("yyyy-MM-dd"),
            dateTo = period.DateTo.ToString("yyyy-MM-dd"),
            periodLabel = period.Segments.Count == 1 ? period.Segments[0].Label : null,
            byWarehouse = result.ByWarehouse.Select((w, i) => new
            {
                rank = i + 1,
                warehouseCode = w.GetValueOrDefault("WarehouseCode")?.ToString(),
                transferLineCount = Convert.ToInt32(GlSqlHelper.ToDecimal(w, "TransferLineCount")),
                transferQty = GlSqlHelper.ToDecimal(w, "TransferQtyOut")
            }),
            transfers = result.Detail.Select((d, i) => new
            {
                rank = i + 1,
                txDate = d.GetValueOrDefault("TxDate")?.ToString(),
                reference = d.GetValueOrDefault("Reference")?.ToString(),
                fromWarehouse = d.GetValueOrDefault("FromWarehouse")?.ToString(),
                toWarehouse = d.GetValueOrDefault("ToWarehouse")?.ToString(),
                productCode = d.GetValueOrDefault("ProductCode")?.ToString(),
                description = d.GetValueOrDefault("Description")?.ToString(),
                qty = GlSqlHelper.ToDecimal(d, "Qty")
            }),
            topTransfers = result.TopTransfers.Select((d, i) => new
            {
                rank = i + 1,
                reference = d.GetValueOrDefault("Reference")?.ToString(),
                txDate = d.GetValueOrDefault("TxDate")?.ToString(),
                fromWarehouse = d.GetValueOrDefault("FromWarehouse")?.ToString(),
                toWarehouse = d.GetValueOrDefault("ToWarehouse")?.ToString(),
                transferQty = GlSqlHelper.ToDecimal(d, "TransferQty")
            }),
            byItem = result.ByItem.Select((d, i) => new
            {
                rank = i + 1,
                productCode = d.GetValueOrDefault("ProductCode")?.ToString(),
                productName = d.GetValueOrDefault("ProductName")?.ToString(),
                transferLineCount = Convert.ToInt32(GlSqlHelper.ToDecimal(d, "TransferLineCount")),
                transferQty = GlSqlHelper.ToDecimal(d, "TransferQty")
            }),
            note = "Warehouse transfers from _bvSTTransactionsFull using TrCode WHT/WHTC (inventory module). Outbound legs only for qty totals.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
