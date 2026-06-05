namespace WizConnector.Service.Sage;

internal static class CustomerCollectionsTopHandler
{
    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 10);
        return CustomerCollectionsSummaryHandler.Serialize(
            companyConnectionString, parameters, "customer.collections.top",
            includeMonthly: false, includeCustomers: true, customerTop: top);
    }
}
