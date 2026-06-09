using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>
/// SAGE-GL-PCLOSE-001 — Period-close readiness checklist.
/// Runs 5 lightweight COUNT queries against the Sage DB and reports
/// blockers / warnings that must be resolved before the period can be closed.
/// </summary>
internal static class GlPeriodCloseReadinessHandler
{
    public const string Operation = "gl.period.close.readiness";
    public const string QuerySerial = "SAGE-GL-PCLOSE-001";

    // Severity labels
    private const string Blocker = "blocker";
    private const string Warning = "warning";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var (from, to) = GlSqlHelper.ParseDateRange(parameters);
        var periodLabel = from.ToString("MMMM yyyy");

        var checks = new List<CheckResult>
        {
            RunBackdatedCheck(connectionString, from, to),
            RunManualJournalsCheck(connectionString, from, to),
            RunRoundJournalsCheck(connectionString, from, to),
            RunUnmatchedBankCheck(connectionString, from, to),
            RunDuplicateJournalCheck(connectionString, from, to),
        };

        var blockers = checks.Where(c => c.Status == "FAIL" && c.Severity == Blocker).ToList();
        var warnings = checks.Where(c => c.Status is "FAIL" or "WARN" && c.Severity == Warning).ToList();
        var readyToClose = blockers.Count == 0;

        var finding = readyToClose
            ? warnings.Count == 0
                ? $"Period {periodLabel} is ready to close — all checks passed."
                : $"Period {periodLabel} has no blockers but {warnings.Count} warning(s) to review."
            : $"Period {periodLabel} cannot be closed: {blockers.Count} blocker(s) outstanding.";

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            periodLabel,
            dateFrom = from.ToString("yyyy-MM-dd"),
            dateTo = to.ToString("yyyy-MM-dd"),
            readyToClose,
            finding,
            blockers = blockers.Select(SerializeCheck),
            warnings = warnings.Select(SerializeCheck),
            checks = checks.Select(SerializeCheck),
            note = "Period-close readiness: PASS = no items found; FAIL = items require resolution; WARN = review recommended.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }

    // ── Checks ────────────────────────────────────────────────────────────────

    private static CheckResult RunBackdatedCheck(string conn, DateTime from, DateTime to)
    {
        // Backdated: journals where TxDate is in-period but DTStamp (post date) is after period end.
        const string sql = """
            SELECT COUNT(*) FROM PostGL
            WHERE CAST(TxDate AS DATE) >= @pDateFrom AND CAST(TxDate AS DATE) <= @pDateTo
              AND DTStamp IS NOT NULL
              AND CAST(DTStamp AS DATE) > @pDateTo;
            """;
        var count = CountQuery(conn, sql, from, to);
        return new CheckResult(
            "backdated_transactions",
            count == 0 ? "PASS" : "FAIL",
            Blocker,
            count,
            count == 0
                ? "No backdated GL transactions found in period."
                : $"{count} GL transaction(s) have a TxDate inside the period but were posted after period-end.");
    }

    private static CheckResult RunManualJournalsCheck(string conn, DateTime from, DateTime to)
    {
        // Manual journals (Id JL/JNL) in period — should be reviewed before close.
        const string sql = """
            SELECT COUNT(*) FROM PostGL
            WHERE CAST(TxDate AS DATE) >= @pDateFrom AND CAST(TxDate AS DATE) <= @pDateTo
              AND UPPER(ISNULL(Id, '')) IN ('JL', 'JNL');
            """;
        var count = CountQuery(conn, sql, from, to);
        return new CheckResult(
            "manual_journals",
            count == 0 ? "PASS" : "WARN",
            Warning,
            count,
            count == 0
                ? "No manual journal entries in period."
                : $"{count} manual journal line(s) in period — verify all are approved before closing.");
    }

    private static CheckResult RunRoundJournalsCheck(string conn, DateTime from, DateTime to)
    {
        // Round-thousand manual journals — audit flag.
        const string sql = """
            SELECT COUNT(*) FROM PostGL
            WHERE CAST(TxDate AS DATE) >= @pDateFrom AND CAST(TxDate AS DATE) <= @pDateTo
              AND UPPER(ISNULL(Id, '')) IN ('JL', 'JNL')
              AND (
                    (ISNULL(Debit,  0) >= 1000 AND ISNULL(Debit,  0) % 1000 = 0)
                    OR (ISNULL(Credit, 0) >= 1000 AND ISNULL(Credit, 0) % 1000 = 0)
                  );
            """;
        var count = CountQuery(conn, sql, from, to);
        return new CheckResult(
            "round_journals",
            count == 0 ? "PASS" : "WARN",
            Warning,
            count,
            count == 0
                ? "No suspicious round-thousand journal amounts."
                : $"{count} round-thousand journal amount(s) — investigate before closing.");
    }

    private static CheckResult RunUnmatchedBankCheck(string conn, DateTime from, DateTime to)
    {
        // Bank entries in period with no statement match (iReconciled = 0 or equivalent).
        // Uses Cashbook / _btblBankDetails pattern known from BankUnusualHandler.
        const string sql = """
            SELECT COUNT(*)
            FROM PostGL PG
            INNER JOIN Accounts A ON PG.AccountLink = A.AccountLink
            LEFT JOIN _etblGLAccountTypes AT ON A.iAccountType = AT.idGLAccountType
            WHERE CAST(PG.TxDate AS DATE) >= @pDateFrom AND CAST(PG.TxDate AS DATE) <= @pDateTo
              AND (
                    LOWER(ISNULL(AT.cDescription, '')) LIKE '%bank%'
                    OR LOWER(ISNULL(AT.cDescription, '')) LIKE '%cash%'
                    OR LOWER(ISNULL(A.Description, '')) LIKE '%bank%'
                  )
              AND ISNULL(PG.iReconciled, 0) = 0;
            """;
        var count = CountQuery(conn, sql, from, to);
        return new CheckResult(
            "unreconciled_bank",
            count == 0 ? "PASS" : "BLOCKER",
            count == 0 ? "PASS" : Blocker,
            count,
            count == 0
                ? "All bank transactions in period are reconciled."
                : $"{count} bank GL line(s) not yet reconciled — bank must be reconciled before period close.");
    }

    private static CheckResult RunDuplicateJournalCheck(string conn, DateTime from, DateTime to)
    {
        // Detect probable duplicate journals: same audit number appears more than once.
        const string sql = """
            SELECT COUNT(*) FROM (
                SELECT cAuditNumber
                FROM PostGL
                WHERE CAST(TxDate AS DATE) >= @pDateFrom AND CAST(TxDate AS DATE) <= @pDateTo
                  AND cAuditNumber IS NOT NULL
                GROUP BY cAuditNumber
                HAVING COUNT(*) > 2
                   AND SUM(ISNULL(Debit, 0) - ISNULL(Credit, 0)) <> 0
            ) AS Suspects;
            """;
        var count = CountQuery(conn, sql, from, to);
        return new CheckResult(
            "possible_duplicates",
            count == 0 ? "PASS" : "WARN",
            Warning,
            count,
            count == 0
                ? "No probable duplicate journal batches detected."
                : $"{count} audit batch(es) may contain duplicate entries — verify before closing.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int CountQuery(string connectionString, string sql, DateTime from, DateTime to)
    {
        try
        {
            var rows = GlSqlHelper.ExecuteQuery(connectionString, sql, cmd =>
                InvNumSqlHelper.AddDateParameters(cmd, from, to));
            if (rows.Count == 1)
            {
                var val = rows[0].Values.FirstOrDefault();
                return Convert.ToInt32(val ?? 0);
            }
        }
        catch
        {
            // Swallow: if a table/column is missing, treat as 0 (connector may be on older schema).
        }
        return 0;
    }

    private static object SerializeCheck(CheckResult c) => new
    {
        checkId = c.CheckId,
        status = c.Status,
        severity = c.Severity,
        count = c.Count,
        description = c.Description
    };

    private sealed record CheckResult(
        string CheckId,
        string Status,
        string Severity,
        int Count,
        string Description);
}
