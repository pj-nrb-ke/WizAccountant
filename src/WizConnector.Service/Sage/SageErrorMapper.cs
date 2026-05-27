namespace WizConnector.Service.Sage;

public static class SageErrorMapper
{
    public static (string code, string message) Map(Exception ex)
    {
        var text = ex.Message;
        if (text.Contains("balance", StringComparison.OrdinalIgnoreCase))
            return ("SAGE_UNBALANCED", "Journal is not balanced.");
        if (text.Contains("period", StringComparison.OrdinalIgnoreCase))
            return ("SAGE_PERIOD_CLOSED", "Posting period is closed.");
        if (text.Contains("auth", StringComparison.OrdinalIgnoreCase))
            return ("SAGE_AUTH_FAILED", "Sage agent authentication failed.");
        if (text.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
            return ("SAGE_DUPLICATE", "Duplicate reference or document.");
        return ("SAGE_ERROR", text);
    }
}
