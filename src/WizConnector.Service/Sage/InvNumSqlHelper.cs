using System.Data.SqlClient;
using System.Text.RegularExpressions;

namespace WizConnector.Service.Sage;

internal static class InvNumSqlHelper
{
    public const string SalesDocTypeFilter = "(H.DocType = 4 OR H.DocType IN (0, 4))";

    /// <summary>Supplier / purchase invoices on InvNum (DocType 5).</summary>
    public const string PurchaseDocTypeFilter = "(H.DocType = 5 OR H.DocType IN (1, 5))";

    public static string DiscountPredicateForPurchase => DiscountPredicate;

    public static string DiscountPredicate => """
        (
            ISNULL(H.InvDisc, 0) <> 0
            OR ISNULL(H.InvDiscAmnt, 0) <> 0
            OR ISNULL(H.InvDiscAmntEx, 0) <> 0
            OR ISNULL(H.DiscValue, 0) > 0
            OR ISNULL(H.DiscPercentage, 0) > 0
            OR EXISTS (
                SELECT 1 FROM _btblInvoiceLines L
                WHERE L.iInvoiceID = H.AutoIndex
                  AND (
                        ISNULL(L.fLineDiscount, 0) <> 0
                        OR ISNULL(L.fLineDiscountAmnt, 0) <> 0
                        OR ISNULL(L.fLineDiscountAmntEx, 0) <> 0
                      )
            )
        )
        """;

    public static string TotalDiscountExpression =>
        "ISNULL(H.InvDiscAmnt, 0) + ISNULL(H.DiscValue, 0)";

    public static (DateTime From, DateTime To) ParseDateRange(Dictionary<string, string> parameters, string? message = null)
    {
        if (parameters.TryGetValue("dateFrom", out var df) &&
            DateTime.TryParse(df, out var from) &&
            parameters.TryGetValue("dateTo", out var dt) &&
            DateTime.TryParse(dt, out var to))
            return (from.Date, to.Date);

        var year = ParseYear(parameters, message);
        return (new DateTime(year, 1, 1), new DateTime(year, 12, 31));
    }

    /// <summary>From a start month/year onward through today (e.g. "starting from Jan 2026").</summary>
    public static (DateTime From, DateTime To) ParseDateFromOnward(Dictionary<string, string> parameters, string? message = null)
    {
        if (parameters.TryGetValue("dateFrom", out var df) && DateTime.TryParse(df, out var from))
        {
            var to = parameters.TryGetValue("dateTo", out var dt) && DateTime.TryParse(dt, out var toParsed)
                ? toParsed.Date
                : DateTime.Today;
            return (from.Date, to);
        }

        var text = (message ?? parameters.GetValueOrDefault("message") ?? "").ToLowerInvariant();
        var year = ParseYear(parameters, message);
        var month = ParseStartMonthFromText(text) ?? 1;
        if (text.Contains("from jan") || text.Contains("starting from jan") || text.Contains("january 20"))
            month = 1;

        var start = new DateTime(year, month, 1);
        return (start, DateTime.Today);
    }

    private static int? ParseStartMonthFromText(string text)
    {
        var months = new (string key, int num)[]
        {
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
        };
        foreach (var (key, num) in months)
        {
            if (text.Contains(key, StringComparison.Ordinal))
                return num;
        }

        return null;
    }

    public static int ParseYear(Dictionary<string, string> parameters, string? message = null)
    {
        if (parameters.TryGetValue("year", out var y) && int.TryParse(y, out var year) && year is >= 1990 and <= 2100)
            return year;

        if (!string.IsNullOrWhiteSpace(message))
        {
            var fromMsg = ExtractYearFromText(message);
            if (fromMsg.HasValue)
                return fromMsg.Value;
        }

        if (parameters.TryGetValue("message", out var msg))
        {
            var fromMsg = ExtractYearFromText(msg);
            if (fromMsg.HasValue)
                return fromMsg.Value;
        }

        return DateTime.Today.Year;
    }

    public static int? ExtractYearFromText(string text)
    {
        var match = Regex.Match(text, @"\b(20\d{2})\b");
        return match.Success && int.TryParse(match.Groups[1].Value, out var y) ? y : null;
    }

    public static int ParseTop(Dictionary<string, string> parameters, int defaultTop = 5) =>
        Math.Clamp(SageListHelpers.ParseIntParam(parameters, "top", defaultTop), 1, 50);

    public static void AddDateParameters(SqlCommand cmd, DateTime from, DateTime to)
    {
        cmd.Parameters.AddWithValue("@pDateFrom", from);
        cmd.Parameters.AddWithValue("@pDateTo", to);
    }
}
