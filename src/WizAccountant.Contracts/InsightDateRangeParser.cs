using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WizAccountant.Contracts;

/// <summary>
/// Centralized calendar-year period parser (SAGE-DATE-001A Phase 1).
/// Relative periods anchor to UTC today. Fiscal-year calendars are Phase 2.
/// </summary>
public static class InsightDateRangeParser
{
    private static readonly Regex YearPattern = new(@"\b(20\d{2})\b", RegexOptions.Compiled);
    private static readonly Regex QuarterToken = new(@"\bq([1-4])\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HalfYearToken = new(@"\bh([12])\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex QuarterRangePhrase = new(
        @"\bq([1-4])\s*(?:[-–—]|to|through)\s*q([1-4])\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BetweenQuartersPhrase = new(
        @"\bbetween\s+q([1-4])\s+and\s+q([1-4])\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex QuarterAmpersand = new(
        @"\bq([1-4])\s*&\s*q([1-4])\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex QuarterAnd = new(
        @"\bq([1-4])\s+and\s+q([1-4])\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static DateTime ReferenceUtcToday => DateTime.UtcNow.Date;

    public static InsightPeriodResolution ResolvePeriod(
        IReadOnlyDictionary<string, string> parameters,
        DateTime? referenceUtc = null)
    {
        var reference = (referenceUtc ?? ReferenceUtcToday).Date;

        if (parameters.TryGetValue("dateFrom", out var df) && DateTime.TryParse(df, out var from))
        {
            var to = parameters.TryGetValue("dateTo", out var dt) && DateTime.TryParse(dt, out var toParsed)
                ? toParsed.Date
                : reference;
            return BuildExplicit(from.Date, to, parameters);
        }

        if (parameters.TryGetValue("segmentsJson", out var segmentsJson) &&
            !string.IsNullOrWhiteSpace(segmentsJson))
        {
            try
            {
                var segments = JsonSerializer.Deserialize<List<InsightPeriodSegment>>(segmentsJson) ?? [];
                if (segments.Count > 0)
                    return FromSegments(segments, parameters.GetValueOrDefault("periodOriginalText") ?? "",
                        parameters.GetValueOrDefault("periodType") ?? InsightPeriodTypes.QuarterSegments);
            }
            catch
            {
                /* fall through */
            }
        }

        parameters.TryGetValue("message", out var message);
        if (TryResolvePeriod(message, ExtractYear(parameters, message), reference, out var resolved))
            return resolved;

        var year = ExtractYear(parameters, message) ?? reference.Year;
        return DefaultFullYear(year, message ?? "");
    }

    public static bool TryResolvePeriod(
        string? message,
        int? yearOverride,
        out InsightPeriodResolution resolution) =>
        TryResolvePeriod(message, yearOverride, ReferenceUtcToday, out resolution);

    public static bool TryResolvePeriod(
        string? message,
        int? yearOverride,
        DateTime referenceUtc,
        out InsightPeriodResolution resolution)
    {
        resolution = null!;
        if (string.IsNullOrWhiteSpace(message))
            return false;

        var text = message.ToLowerInvariant();
        var year = yearOverride ?? ExtractYearFromText(text) ?? referenceUtc.Year;

        if (TryParseRelative(text, referenceUtc, year, out resolution))
            return true;
        if (TryParseHalfYear(text, year, out resolution))
            return true;
        if (TryParseQuarters(text, year, message, out resolution))
            return true;
        if (TryParseMonthRange(text, year, message, out resolution))
            return true;
        if (TryParseFromMonthOnward(text, year, referenceUtc, message, out resolution))
            return true;

        return false;
    }

    public static void ApplyToParameters(InsightPeriodResolution period, IDictionary<string, string> parameters)
    {
        parameters["dateFrom"] = period.DateFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        parameters["dateTo"] = period.DateTo.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        parameters["periodType"] = period.PeriodType;
        parameters["periodOriginalText"] = period.OriginalText;
        parameters["periodIsContiguous"] = period.IsContiguous ? "true" : "false";

        if (!period.IsContiguous && period.Segments.Count > 0)
            parameters["segmentsJson"] = JsonSerializer.Serialize(period.Segments);
        else
            parameters.Remove("segmentsJson");
    }

    /// <summary>Legacy contiguous range helper.</summary>
    public static (DateTime From, DateTime To) Resolve(IReadOnlyDictionary<string, string> parameters)
    {
        var period = ResolvePeriod(parameters);
        return (period.DateFrom, period.DateTo);
    }

    /// <summary>Legacy try-parse (contiguous envelope only).</summary>
    public static bool TryParse(string? message, int? yearOverride, out (DateTime From, DateTime To) range)
    {
        range = default;
        if (!TryResolvePeriod(message, yearOverride, out var resolution))
            return false;
        range = (resolution.DateFrom, resolution.DateTo);
        return true;
    }

    public static int? ExtractYearFromText(string text)
    {
        var match = YearPattern.Match(text);
        return match.Success && int.TryParse(match.Groups[1].Value, out var y) ? y : null;
    }

    private static InsightPeriodResolution BuildExplicit(
        DateTime from,
        DateTime to,
        IReadOnlyDictionary<string, string> parameters) =>
        new()
        {
            DateFrom = from,
            DateTo = to,
            PeriodType = parameters.GetValueOrDefault("periodType") ?? InsightPeriodTypes.ExplicitRange,
            IsContiguous = true,
            Segments =
            [
                new InsightPeriodSegment
                {
                    From = from,
                    To = to,
                    Label = $"{from:yyyy-MM-dd} to {to:yyyy-MM-dd}"
                }
            ],
            OriginalText = parameters.GetValueOrDefault("periodOriginalText") ?? ""
        };

    private static InsightPeriodResolution FromSegments(
        List<InsightPeriodSegment> segments,
        string originalText,
        string periodType)
    {
        segments = segments.OrderBy(s => s.From).ToList();
        return new InsightPeriodResolution
        {
            DateFrom = segments[0].From,
            DateTo = segments[^1].To,
            PeriodType = periodType,
            IsContiguous = false,
            Segments = segments,
            OriginalText = originalText
        };
    }

    private static InsightPeriodResolution DefaultFullYear(int year, string originalText) =>
        new()
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
            OriginalText = originalText
        };

    private static bool TryParseRelative(string text, DateTime referenceUtc, int year, out InsightPeriodResolution resolution)
    {
        resolution = null!;
        if (text.Contains("ytd") || text.Contains("year to date"))
        {
            resolution = SingleSegment(new DateTime(year, 1, 1), referenceUtc, InsightPeriodTypes.Ytd, "YTD");
            return true;
        }

        if (Regex.IsMatch(text, @"\bmtd\b") || text.Contains("month to date"))
        {
            var from = new DateTime(referenceUtc.Year, referenceUtc.Month, 1);
            resolution = SingleSegment(from, referenceUtc, InsightPeriodTypes.Mtd, "MTD");
            return true;
        }

        if (text.Contains("qtd") || text.Contains("quarter to date") || text.Contains("this quarter"))
        {
            var q = CalendarQuarter(referenceUtc.Month);
            resolution = SingleSegment(QuarterStart(referenceUtc.Year, q), referenceUtc,
                text.Contains("this quarter") ? InsightPeriodTypes.ThisQuarter : InsightPeriodTypes.Qtd,
                text.Contains("this quarter") ? "This quarter" : "QTD");
            return true;
        }

        if (text.Contains("last quarter") || text.Contains("past quarter"))
        {
            var (y, q) = PreviousQuarter(referenceUtc);
            resolution = SingleSegment(QuarterStart(y, q), QuarterEnd(y, q), InsightPeriodTypes.LastQuarter, $"Q{q} {y}");
            return true;
        }

        var lastMonths = Regex.Match(text, @"\blast\s+(\d+)\s+months?\b");
        if (lastMonths.Success && int.TryParse(lastMonths.Groups[1].Value, out var months) && months is > 0 and <= 36)
        {
            var from = referenceUtc.AddMonths(-months);
            resolution = SingleSegment(from, referenceUtc, InsightPeriodTypes.LastMonths, $"Last {months} months");
            return true;
        }

        return false;
    }

    private static bool TryParseHalfYear(string text, int year, out InsightPeriodResolution resolution)
    {
        resolution = null!;
        int? half = null;
        var hm = HalfYearToken.Match(text);
        if (hm.Success)
            half = int.Parse(hm.Groups[1].Value);
        else if (text.Contains("first half"))
            half = 1;
        else if (text.Contains("second half"))
            half = 2;

        if (half is null)
            return false;

        var (from, to) = half == 1
            ? (new DateTime(year, 1, 1), new DateTime(year, 6, 30))
            : (new DateTime(year, 7, 1), new DateTime(year, 12, 31));

        resolution = SingleSegment(from, to, InsightPeriodTypes.HalfYear, half == 1 ? $"H1 {year}" : $"H2 {year}");
        return true;
    }

    private static bool TryParseQuarters(string text, int year, string originalText, out InsightPeriodResolution resolution)
    {
        resolution = null!;
        var quarters = ParseQuarterNumbers(text);
        if (quarters.Count == 0)
            return false;

        var rangeMatch = QuarterRangePhrase.Match(text);
        if (!rangeMatch.Success)
            rangeMatch = BetweenQuartersPhrase.Match(text);

        if (rangeMatch.Success)
        {
            var qFrom = int.Parse(rangeMatch.Groups[1].Value);
            var qTo = int.Parse(rangeMatch.Groups[2].Value);
            var minQ = Math.Min(qFrom, qTo);
            var maxQ = Math.Max(qFrom, qTo);
            var from = QuarterStart(year, minQ);
            var to = QuarterEnd(year, maxQ);
            resolution = SingleSegment(from, to, InsightPeriodTypes.QuarterRange,
                $"Q{minQ}-Q{maxQ} {year}");
            return true;
        }

        if (IsNonContiguousQuarterRequest(text, quarters))
        {
            var segments = quarters.Select(q => new InsightPeriodSegment
            {
                From = QuarterStart(year, q),
                To = QuarterEnd(year, q),
                Label = $"Q{q} {year}"
            }).ToList();

            resolution = FromSegments(segments, originalText, InsightPeriodTypes.QuarterSegments);
            return true;
        }

        if (quarters.Count == 1)
        {
            var q = quarters[0];
            resolution = SingleSegment(QuarterStart(year, q), QuarterEnd(year, q),
                InsightPeriodTypes.SingleQuarter, $"Q{q} {year}");
            return true;
        }

        if (AreConsecutive(quarters))
        {
            var minQ = quarters.Min();
            var maxQ = quarters.Max();
            resolution = SingleSegment(QuarterStart(year, minQ), QuarterEnd(year, maxQ),
                InsightPeriodTypes.QuarterRange, $"Q{minQ}-Q{maxQ} {year}");
            return true;
        }

        var splitSegments = quarters.Select(q => new InsightPeriodSegment
        {
            From = QuarterStart(year, q),
            To = QuarterEnd(year, q),
            Label = $"Q{q} {year}"
        }).ToList();
        resolution = FromSegments(splitSegments, originalText, InsightPeriodTypes.QuarterSegments);
        return true;
    }

    private static bool IsNonContiguousQuarterRequest(string text, List<int> quarters)
    {
        if (QuarterAmpersand.IsMatch(text))
            return true;
        if (QuarterAnd.IsMatch(text) && !text.Contains("between"))
            return true;
        return quarters.Count >= 2 && !AreConsecutive(quarters);
    }

    private static bool TryParseMonthRange(string text, int year, string originalText, out InsightPeriodResolution resolution)
    {
        resolution = null!;
        var between = Regex.Match(text,
            @"\bbetween\s+(jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|jun(?:e)?|jul(?:y)?|aug(?:ust)?|sep(?:tember)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)\s+(?:and|to)\s+(jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|jun(?:e)?|jul(?:y)?|aug(?:ust)?|sep(?:tember)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)\b");
        if (!between.Success)
        {
            between = Regex.Match(text,
                @"(jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|jun(?:e)?|jul(?:y)?|aug(?:ust)?|sep(?:tember)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)\s+(?:to|through)\s+(jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|jun(?:e)?|jul(?:y)?|aug(?:ust)?|sep(?:ember)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)");
        }

        if (!between.Success)
            return false;

        var fromMonth = MonthFromToken(between.Groups[1].Value);
        var toMonth = MonthFromToken(between.Groups[2].Value);
        if (fromMonth <= 0 || toMonth <= 0)
            return false;

        var from = new DateTime(year, fromMonth, 1);
        var to = LastDayOfMonth(year, toMonth);
        resolution = SingleSegment(from, to, InsightPeriodTypes.MonthRange, originalText.Trim());
        return true;
    }

    private static bool TryParseFromMonthOnward(
        string text,
        int year,
        DateTime referenceUtc,
        string originalText,
        out InsightPeriodResolution resolution)
    {
        resolution = null!;
        var startMonth = ParseStartMonth(text);
        if (startMonth is null)
            return false;
        if (!text.Contains("from ") && !text.Contains("starting from") && !text.Contains(" onward"))
            return false;

        var from = new DateTime(year, startMonth.Value, 1);
        resolution = SingleSegment(from, referenceUtc, InsightPeriodTypes.FromMonthOnward, originalText.Trim());
        return true;
    }

    private static InsightPeriodResolution SingleSegment(
        DateTime from,
        DateTime to,
        string periodType,
        string label) =>
        new()
        {
            DateFrom = from,
            DateTo = to,
            PeriodType = periodType,
            IsContiguous = true,
            Segments = [new InsightPeriodSegment { From = from, To = to, Label = label }],
            OriginalText = label
        };

    private static List<int> ParseQuarterNumbers(string text)
    {
        var quarters = new HashSet<int>();
        foreach (Match m in QuarterToken.Matches(text))
            quarters.Add(int.Parse(m.Groups[1].Value));

        if (text.Contains("first quarter") || text.Contains("1st quarter")) quarters.Add(1);
        if (text.Contains("second quarter") || text.Contains("2nd quarter")) quarters.Add(2);
        if (text.Contains("third quarter") || text.Contains("3rd quarter")) quarters.Add(3);
        if (text.Contains("fourth quarter") || text.Contains("4th quarter")) quarters.Add(4);

        return quarters.OrderBy(q => q).ToList();
    }

    private static bool AreConsecutive(IReadOnlyList<int> quarters)
    {
        if (quarters.Count <= 1) return true;
        for (var i = 1; i < quarters.Count; i++)
        {
            if (quarters[i] - quarters[i - 1] != 1)
                return false;
        }
        return true;
    }

    private static int CalendarQuarter(int month) => (month - 1) / 3 + 1;

    private static (int Year, int Quarter) PreviousQuarter(DateTime referenceUtc)
    {
        var q = CalendarQuarter(referenceUtc.Month);
        var y = referenceUtc.Year;
        if (q == 1)
            return (y - 1, 4);
        return (y, q - 1);
    }

    private static int? ExtractYear(IReadOnlyDictionary<string, string> parameters, string? message)
    {
        if (parameters.TryGetValue("year", out var y) && int.TryParse(y, out var year) && year is >= 1990 and <= 2100)
            return year;
        return ExtractYearFromText(message ?? "");
    }

    private static int? ParseStartMonth(string text)
    {
        foreach (var (key, num) in MonthTokens)
        {
            if (text.Contains(key, StringComparison.Ordinal))
                return num;
        }

        return null;
    }

    public static DateTime QuarterStart(int year, int quarter) => quarter switch
    {
        1 => new DateTime(year, 1, 1),
        2 => new DateTime(year, 4, 1),
        3 => new DateTime(year, 7, 1),
        4 => new DateTime(year, 10, 1),
        _ => new DateTime(year, 1, 1)
    };

    public static DateTime QuarterEnd(int year, int quarter) => quarter switch
    {
        1 => new DateTime(year, 3, 31),
        2 => new DateTime(year, 6, 30),
        3 => new DateTime(year, 9, 30),
        4 => new DateTime(year, 12, 31),
        _ => new DateTime(year, 12, 31)
    };

    private static DateTime LastDayOfMonth(int year, int month) =>
        new DateTime(year, month, DateTime.DaysInMonth(year, month));

    private static int MonthFromToken(string token)
    {
        token = token.ToLowerInvariant();
        foreach (var (key, num) in MonthTokens)
        {
            if (token.StartsWith(key.TrimEnd(), StringComparison.Ordinal) || token == key.Trim())
                return num;
        }

        return 0;
    }

    private static readonly (string key, int num)[] MonthTokens =
    [
        ("january", 1), ("jan ", 1), ("jan.", 1),
        ("february", 2), ("feb ", 2),
        ("march", 3), ("mar ", 3),
        ("april", 4), ("apr ", 4),
        ("may", 5),
        ("june", 6), ("jun ", 6),
        ("july", 7), ("jul ", 7),
        ("august", 8), ("aug ", 8),
        ("september", 9), ("sep ", 9),
        ("october", 10), ("oct ", 10),
        ("november", 11), ("nov ", 11),
        ("december", 12), ("dec ", 12)
    ];
}
