namespace WizConnector.Service.Sage;

internal static class CustomerCollectionsByCustomerHandler
{
    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        var top = InvNumSqlHelper.ParseTop(parameters, 500);
        return CustomerCollectionsSummaryHandler.Serialize(
            companyConnectionString, parameters, "customer.collections.by.customer",
            includeMonthly: false, includeCustomers: true, customerTop: top);
    }
}
