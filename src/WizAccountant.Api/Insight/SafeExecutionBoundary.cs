namespace WizAccountant.Api.Insight;

/// <summary>User-safe error messages — technical detail stays in logs only (SAGE-NEXT-001).</summary>
internal static class SafeExecutionBoundary
{
    public static string FormatHandlerFailure(string? operation, string safeReason) =>
        "Operation understood:\n" +
        (string.IsNullOrWhiteSpace(operation) ? "(no operation)" : operation) +
        "\n\nExecution failed safely.\n\nReason:\n" +
        safeReason +
        "\n\n" + ReadOnlyChatService.GuardrailText;

    public static string SanitizeForUser(string? technical)
    {
        if (string.IsNullOrWhiteSpace(technical))
            return "The read could not be completed.";

        var t = technical;
        if (t.Contains("SQLite does not support", StringComparison.OrdinalIgnoreCase))
            return "A database ordering limitation occurred while loading conversation context. Retry the question or start a new conversation.";
        if (t.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase))
            return "Required Sage tables or columns were not found on this company database.";
        if (t.Contains("Unsupported operation", StringComparison.OrdinalIgnoreCase))
            return "The connector on this PC does not support this read yet. Rebuild pilot apps from WizPilot.";
        if (t.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            return "Could not reach Sage in time. Check that the connector service is running.";
        if (t.Contains("stack", StringComparison.OrdinalIgnoreCase) ||
            t.Contains(" at ", StringComparison.Ordinal) ||
            t.Contains(":\\", StringComparison.Ordinal))
            return "An internal error occurred while processing the request.";

        var firstLine = t.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? t;
        return firstLine.Length > 200 ? firstLine[..200] + "…" : firstLine;
    }

    public static bool LooksLikeRawException(string? text) =>
        !string.IsNullOrEmpty(text) &&
        (text.Contains("Exception:", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("StackTrace", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("HEADERS", StringComparison.Ordinal));
}
