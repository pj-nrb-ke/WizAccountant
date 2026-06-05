namespace WizAccountant.Contracts;

/// <summary>Phase 1 calendar-year period resolution (SAGE-DATE-001A). Fiscal-year parsing is Phase 2.</summary>
public sealed class InsightPeriodSegment
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string Label { get; set; } = "";
}

public sealed class InsightPeriodResolution
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public string PeriodType { get; set; } = InsightPeriodTypes.DefaultFullYear;
    public bool IsContiguous { get; set; } = true;
    public List<InsightPeriodSegment> Segments { get; set; } = [];
    public string OriginalText { get; set; } = "";
}

public static class InsightPeriodTypes
{
    public const string DefaultFullYear = "default_full_year";
    public const string ExplicitRange = "explicit_range";
    public const string SingleQuarter = "single_quarter";
    public const string QuarterRange = "quarter_range";
    public const string QuarterSegments = "quarter_segments";
    public const string HalfYear = "half_year";
    public const string MonthRange = "month_range";
    public const string FromMonthOnward = "from_month_onward";
    public const string Ytd = "ytd";
    public const string Mtd = "mtd";
    public const string Qtd = "qtd";
    public const string ThisQuarter = "this_quarter";
    public const string LastQuarter = "last_quarter";
    public const string LastMonths = "last_months";
}
