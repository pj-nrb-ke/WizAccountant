using System.Text.Json;
using WizAccountant.Contracts;

namespace WizConnector.Service.Sage;

internal static class CustomerCollectionsSummaryHandler
{
    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters) =>
        Serialize(companyConnectionString, parameters, "customer.collections.summary", includeMonthly: true, includeCustomers: false, 0);

    internal static string Serialize(
        string companyConnectionString,
        Dictionary<string, string> parameters,
        string operation,
        bool includeMonthly,
        bool includeCustomers,
        int customerTop)
    {
        var result = CustomerCollectionsEngine.Load(
            companyConnectionString, parameters, includeMonthly, includeCustomers, customerTop);

        if (!result.Success)
            throw new InvalidOperationException(result.Error);

        var period = result.Period;

        return JsonSerializer.Serialize(new
        {
            querySerial = CustomerCollectionsEngine.QuerySerial,
            operation,
            dateFrom = period.DateFrom.ToString("yyyy-MM-dd"),
            dateTo = period.DateTo.ToString("yyyy-MM-dd"),
            periodType = period.PeriodType,
            periodOriginalText = period.OriginalText,
            periodIsContiguous = period.IsContiguous,
            segments = period.Segments.Select(s => new
            {
                from = s.From.ToString("yyyy-MM-dd"),
                to = s.To.ToString("yyyy-MM-dd"),
                label = s.Label
            }),
            totalCollections = result.TotalCollections,
            segmentTotals = result.SegmentTotals.Select(s => new
            {
                label = s.Label,
                dateFrom = s.From.ToString("yyyy-MM-dd"),
                dateTo = s.To.ToString("yyyy-MM-dd"),
                collectionAmount = s.Amount
            }),
            monthlyBreakdown = result.Monthly.Select(m => new
            {
                year = m.Year,
                monthNo = m.MonthNo,
                month = m.MonthName,
                segmentLabel = m.SegmentLabel,
                collectionAmount = m.Amount
            }),
            byCustomer = result.Customers.Select(c => new
            {
                rank = c.Rank,
                customerCode = c.Code,
                customerName = c.Name,
                collectionAmount = c.Amount
            }),
            note = "Customer receipts from PostAR credit postings (money received). Not customer master balances.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
