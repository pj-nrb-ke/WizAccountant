namespace WizConnector.Service.Sage;

internal static class CustomerCollectionsByMonthHandler
{
    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters) =>
        CustomerCollectionsSummaryHandler.Serialize(
            companyConnectionString, parameters, "customer.collections.by.month",
            includeMonthly: true, includeCustomers: false, customerTop: 0);
}
