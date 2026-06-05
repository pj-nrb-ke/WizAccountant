using System.Text.Json;
using WizAccountant.Contracts;

namespace WizConnector.Service.Sage;

internal static class SalesDebitNoteCountHandler
{
    public const string Operation = "salesdebitnote.count";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var result = SalesDebitNoteEngine.Load(companyConnectionString, parameters, false, false, false, 0, 0);
        return SalesDebitNoteHandlerSupport.Serialize(result, Operation, includeList: false, includeTop: false, includeMonthly: false);
    }
}

internal static class SalesDebitNoteListHandler
{
    public const string Operation = "salesdebitnote.list";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 25);
        var result = SalesDebitNoteEngine.Load(companyConnectionString, parameters, true, false, false, top, 0);
        return SalesDebitNoteHandlerSupport.Serialize(result, Operation, includeList: true, includeTop: false, includeMonthly: false);
    }
}

internal static class SalesDebitNoteSummaryHandler
{
    public const string Operation = "salesdebitnote.summary";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var result = SalesDebitNoteEngine.Load(companyConnectionString, parameters, false, false, true, 0, 0);
        return SalesDebitNoteHandlerSupport.Serialize(result, Operation, includeList: false, includeTop: false, includeMonthly: true);
    }
}

internal static class SalesDebitNoteTopHandler
{
    public const string Operation = "salesdebitnote.top";

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 10);
        var result = SalesDebitNoteEngine.Load(companyConnectionString, parameters, false, true, false, 0, top);
        return SalesDebitNoteHandlerSupport.Serialize(result, Operation, includeList: false, includeTop: true, includeMonthly: false);
    }
}

internal static class SalesDebitNoteHandlerSupport
{
    public static string Serialize(
        SalesDebitNoteEngine.LoadResult result,
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
            querySerial = SalesDebitNoteEngine.QuerySerial,
            operation,
            dateFrom = period.DateFrom.ToString("yyyy-MM-dd"),
            dateTo = period.DateTo.ToString("yyyy-MM-dd"),
            periodType = period.PeriodType,
            periodLabel,
            debitNoteCount = result.Count,
            totalValue = result.TotalValue,
            countOnly = operation == SalesDebitNoteCountHandler.Operation,
            aggregationMode = operation == SalesDebitNoteCountHandler.Operation,
            documents = includeList
                ? result.Documents.Select((d, i) => new
                {
                    rank = i + 1,
                    reference = d.GetValueOrDefault("Reference")?.ToString(),
                    description = d.GetValueOrDefault("Description")?.ToString(),
                    debit = GlSqlHelper.ToDecimal(d, "Debit"),
                    txDate = d.GetValueOrDefault("TxDate")?.ToString(),
                    customerCode = d.GetValueOrDefault("CustomerCode")?.ToString(),
                    customerName = d.GetValueOrDefault("CustomerName")?.ToString()
                })
                : null,
            topCustomers = includeTop
                ? result.TopCustomers.Select((d, i) => new
                {
                    rank = i + 1,
                    customerCode = d.GetValueOrDefault("CustomerCode")?.ToString(),
                    customerName = d.GetValueOrDefault("CustomerName")?.ToString(),
                    debitNoteCount = Convert.ToInt32(GlSqlHelper.ToDecimal(d, "DebitNoteCount")),
                    totalValue = GlSqlHelper.ToDecimal(d, "TotalValue")
                })
                : null,
            monthlyBreakdown = includeMonthly
                ? result.Monthly.Select(m => new
                {
                    year = Convert.ToInt32(GlSqlHelper.ToDecimal(m, "TxYear")),
                    monthNo = Convert.ToInt32(GlSqlHelper.ToDecimal(m, "MonthNo")),
                    month = m.GetValueOrDefault("MonthName")?.ToString(),
                    debitNoteCount = Convert.ToInt32(GlSqlHelper.ToDecimal(m, "DebitNoteCount")),
                    totalValue = GlSqlHelper.ToDecimal(m, "TotalValue")
                })
                : null,
            finding = result.Count == 0
                ? $"No customer debit notes found for {periodLabel}."
                : $"Customer debit notes for {periodLabel}: {result.Count:N0} (value {result.TotalValue:N2}).",
            note = "PostAR debit postings with TrCode DN (AR). Not GRV DocType 2 or customer outstanding balances.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
