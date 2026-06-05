using System.Text.Json;
using WizAccountant.Contracts;

namespace WizConnector.Service.Sage;

internal static class SupplierCreditNoteCountHandler
{
    public const string Operation = "suppliercreditnote.count";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var result = SupplierCreditNoteEngine.Load(companyConnectionString, parameters, false, false, false, 0, 0);
        return SupplierCreditNoteHandlerSupport.Serialize(result, Operation, includeList: false, includeTop: false, includeMonthly: false);
    }
}

internal static class SupplierCreditNoteListHandler
{
    public const string Operation = "suppliercreditnote.list";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 25);
        var result = SupplierCreditNoteEngine.Load(companyConnectionString, parameters, true, false, false, top, 0);
        return SupplierCreditNoteHandlerSupport.Serialize(result, Operation, includeList: true, includeTop: false, includeMonthly: false);
    }
}

internal static class SupplierCreditNoteSummaryHandler
{
    public const string Operation = "suppliercreditnote.summary";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var result = SupplierCreditNoteEngine.Load(companyConnectionString, parameters, false, false, true, 0, 0);
        return SupplierCreditNoteHandlerSupport.Serialize(result, Operation, includeList: false, includeTop: false, includeMonthly: true);
    }
}

internal static class SupplierCreditNoteTopHandler
{
    public const string Operation = "suppliercreditnote.top";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 10);
        var result = SupplierCreditNoteEngine.Load(companyConnectionString, parameters, false, true, false, 0, top);
        return SupplierCreditNoteHandlerSupport.Serialize(result, Operation, includeList: false, includeTop: true, includeMonthly: false);
    }
}

internal static class SupplierCreditNoteHandlerSupport
{
    public static string Serialize(
        SupplierCreditNoteEngine.LoadResult result,
        string operation,
        bool includeList,
        bool includeTop,
        bool includeMonthly)
    {
        var period = result.Period;
        var periodLabel = period.Segments.Count == 1 && !string.IsNullOrWhiteSpace(period.Segments[0].Label)
            ? period.Segments[0].Label
            : $"{period.DateFrom:yyyy-MM-dd} to {period.DateTo:yyyy-MM-dd}";

        return JsonSerializer.Serialize(new
        {
            querySerial = SupplierCreditNoteEngine.QuerySerial,
            operation,
            dateFrom = period.DateFrom.ToString("yyyy-MM-dd"),
            dateTo = period.DateTo.ToString("yyyy-MM-dd"),
            periodType = period.PeriodType,
            periodLabel,
            creditNoteCount = result.Count,
            totalValue = result.TotalValue,
            countOnly = operation == SupplierCreditNoteCountHandler.Operation,
            aggregationMode = operation == SupplierCreditNoteCountHandler.Operation,
            documents = includeList
                ? result.Documents.Select((d, i) => new
                {
                    rank = i + 1,
                    invNumber = d.GetValueOrDefault("InvNumber")?.ToString(),
                    invDate = d.GetValueOrDefault("InvDate")?.ToString(),
                    totalValue = GlSqlHelper.ToDecimal(d, "InvTotIncl"),
                    supplierCode = d.GetValueOrDefault("SupplierCode")?.ToString(),
                    supplierName = d.GetValueOrDefault("SupplierName")?.ToString()
                })
                : null,
            topSuppliers = includeTop
                ? result.TopSuppliers.Select((d, i) => new
                {
                    rank = i + 1,
                    supplierCode = d.GetValueOrDefault("SupplierCode")?.ToString(),
                    supplierName = d.GetValueOrDefault("SupplierName")?.ToString(),
                    creditNoteCount = Convert.ToInt32(GlSqlHelper.ToDecimal(d, "CreditNoteCount")),
                    totalValue = GlSqlHelper.ToDecimal(d, "TotalValue")
                })
                : null,
            monthlyBreakdown = includeMonthly
                ? result.Monthly.Select(m => new
                {
                    year = Convert.ToInt32(GlSqlHelper.ToDecimal(m, "TxYear")),
                    monthNo = Convert.ToInt32(GlSqlHelper.ToDecimal(m, "MonthNo")),
                    month = m.GetValueOrDefault("MonthName")?.ToString(),
                    creditNoteCount = Convert.ToInt32(GlSqlHelper.ToDecimal(m, "CreditNoteCount")),
                    totalValue = GlSqlHelper.ToDecimal(m, "TotalValue")
                })
                : null,
            finding = result.Count == 0
                ? $"No supplier credit notes found for {periodLabel}."
                : $"Supplier credit notes for {periodLabel}: {result.Count:N0} (value {result.TotalValue:N2}).",
            note = "InvNum RTS documents (DocType 3). Not supplier credit balances or customer credit notes.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
