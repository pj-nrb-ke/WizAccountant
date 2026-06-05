namespace WizAccountant.Api.Insight;

/// <summary>Routes historical customer collection / receipt queries (SAGE-PATCH-010).</summary>
internal static class CustomerCollectionsChatMatcher
{
    public static bool TryRoute(
        string message,
        string m,
        Dictionary<string, string> parameters,
        List<string> tools,
        out string? operation)
    {
        operation = null;
        if (!CustomerCollectionsHelper.IsCustomerCollectionsQuery(m))
            return false;

        parameters["message"] = message;
        operation = CustomerCollectionsHelper.ResolveOperation(m);
        tools.Add(operation);
        return true;
    }
}
