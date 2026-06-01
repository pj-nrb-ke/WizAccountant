using System.Data.SqlClient;
using System.Text.RegularExpressions;

namespace WizConnector.Service.Sage;

/// <summary>Shared PostGL / Accounts SQL helpers for GL analytics (SAGE-TRAIN-005).</summary>
internal static class GlSqlHelper
{
    public const string ExpenseJoin = """
        INNER JOIN Accounts A ON PG.AccountLink = A.AccountLink
        LEFT JOIN _etblGLAccountTypes AT ON A.iAccountType = AT.idGLAccountType
        """;

    public const string ExpenseFilter = """
        (
            LOWER(ISNULL(AT.cDescription, '')) LIKE '%expense%'
            OR LOWER(ISNULL(AT.cGLAccountTypeCode, '')) LIKE '%exp%'
            OR LOWER(ISNULL(AT.cCode, '')) LIKE '%exp%'
        )
        """;

    public const string BankJoin = ExpenseJoin;

    public const string BankFilter = """
        (
            LOWER(ISNULL(AT.cDescription, '')) LIKE '%bank%'
            OR LOWER(ISNULL(AT.cDescription, '')) LIKE '%cash%'
            OR LOWER(ISNULL(A.Description, '')) LIKE '%bank%'
        )
        """;

    public const string NetValueExpr = "ISNULL(PG.Debit, 0) - ISNULL(PG.Credit, 0)";

    public static (DateTime From, DateTime To) ParseDateRange(Dictionary<string, string> parameters, string? message = null) =>
        InvNumSqlHelper.ParseDateRange(parameters, message ?? parameters.GetValueOrDefault("message"));

    public static int ParseTop(Dictionary<string, string> parameters, int defaultTop = 10) =>
        InvNumSqlHelper.ParseTop(parameters, defaultTop);

    public static int ParseHorizonDays(Dictionary<string, string> parameters, int defaultDays = 30)
    {
        if (parameters.TryGetValue("horizonDays", out var h) && int.TryParse(h, out var d))
            return Math.Clamp(d, 7, 365);
        return defaultDays;
    }

    public static int? ExtractMonthFromMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;
        var m = message.ToLowerInvariant();
        string[] months = ["january", "february", "march", "april", "may", "june",
            "july", "august", "september", "october", "november", "december"];
        for (var i = 0; i < months.Length; i++)
            if (m.Contains(months[i])) return i + 1;
        return null;
    }

    public static List<Dictionary<string, object?>> ExecuteQuery(
        string connectionString,
        string sql,
        Action<SqlCommand>? configure = null)
    {
        var rows = new List<Dictionary<string, object?>>();
        using var conn = new SqlConnection(connectionString);
        conn.Open();
        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 180 };
        configure?.Invoke(cmd);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
                dict[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(dict);
        }
        return rows;
    }

    public static decimal ToDecimal(Dictionary<string, object?> r, string key) =>
        r.TryGetValue(key, out var v) && v is not null ? Convert.ToDecimal(v) : 0m;
}
