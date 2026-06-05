using WizAccountant.Contracts;

namespace WizAccountant.Api.Insight;

internal static class InsightChatPeriodHelper
{
    public static InsightPeriodResolution? ResolveFromMessage(string message, int? year = null) =>
        InsightDateRangeParser.TryResolvePeriod(message, year, out var period) ? period : null;

    public static bool TryApplyToParameters(string message, Dictionary<string, string> parameters, int? year = null)
    {
        if (!InsightDateRangeParser.TryResolvePeriod(message, year, out var period))
            return false;
        InsightDateRangeParser.ApplyToParameters(period, parameters);
        return true;
    }

    public static bool TryApplyForOperation(
        string? operation,
        string message,
        Dictionary<string, string> parameters,
        QueryIntentContract contract,
        out string? blockReason)
    {
        blockReason = null;
        if (!InsightPeriodPolicy.ShouldApplyPeriodRange(operation))
            return true;

        var period = contract.Period;
        if (period is null && !InsightDateRangeParser.TryResolvePeriod(message, contract.YearHint, out period))
        {
            var year = contract.YearHint ?? InsightDateRangeParser.ReferenceUtcToday.Year;
            period = new InsightPeriodResolution
            {
                DateFrom = new DateTime(year, 1, 1),
                DateTo = new DateTime(year, 12, 31),
                PeriodType = InsightPeriodTypes.DefaultFullYear,
                IsContiguous = true,
                Segments =
                [
                    new InsightPeriodSegment
                    {
                        From = new DateTime(year, 1, 1),
                        To = new DateTime(year, 12, 31),
                        Label = $"Calendar {year}"
                    }
                ],
                OriginalText = message
            };
        }

        if (period is null)
            return true;

        if (!period.IsContiguous)
        {
            var cap = HandlerCapabilityRegistry.Get(operation);
            if (cap is null || !cap.SupportsSegmentedPeriods)
            {
                blockReason = InsightPeriodPolicy.FormatSegmentedBlockMessage(operation);
                return false;
            }
        }

        InsightDateRangeParser.ApplyToParameters(period, parameters);
        return true;
    }
}
