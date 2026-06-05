using System.Text.RegularExpressions;

namespace WizAccountant.Contracts;

/// <summary>Read-only guard for ad-hoc Insight SQL (SELECT / WITH only).</summary>
public static class InsightSqlGuard
{
    private static readonly Regex BlockedKeyword = new(
        @"\b(INSERT|UPDATE|DELETE|DROP|ALTER|CREATE|TRUNCATE|MERGE|EXEC|EXECUTE|GRANT|REVOKE|DENY|BACKUP|RESTORE|KILL|SHUTDOWN|DBCC|xp_|sp_\w+)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SelectInto = new(
        @"\bINTO\b\s+(#|[@\[]|[A-Za-z_])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool IsReadOnlySelect(string? sql, out string? rejectionReason)
    {
        rejectionReason = null;
        if (string.IsNullOrWhiteSpace(sql))
        {
            rejectionReason = "SQL query is empty.";
            return false;
        }

        var normalized = StripComments(sql).Trim();
        if (normalized.Length == 0)
        {
            rejectionReason = "SQL query is empty after removing comments.";
            return false;
        }

        if (ContainsMultipleStatements(normalized))
        {
            rejectionReason = "Only a single SQL statement is allowed.";
            return false;
        }

        if (!StartsWithSelectOrCte(normalized))
        {
            rejectionReason = "Only SELECT queries (optionally starting with WITH) are allowed.";
            return false;
        }

        if (BlockedKeyword.IsMatch(normalized) || SelectInto.IsMatch(normalized))
        {
            rejectionReason = "The query contains a blocked keyword (writes, DDL, or stored procedures).";
            return false;
        }

        return true;
    }

    private static string StripComments(string sql)
    {
        var noBlock = Regex.Replace(sql, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        return Regex.Replace(noBlock, @"--[^\r\n]*", " ");
    }

    private static bool ContainsMultipleStatements(string sql)
    {
        var trimmed = sql.TrimEnd().TrimEnd(';').Trim();
        return trimmed.Contains(';');
    }

    private static bool StartsWithSelectOrCte(string sql)
    {
        var lead = Regex.Replace(sql, @"^\s+", "");
        return lead.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
               || lead.StartsWith("WITH", StringComparison.OrdinalIgnoreCase);
    }
}
