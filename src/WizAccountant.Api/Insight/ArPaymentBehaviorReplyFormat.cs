using System.Text.Json;

namespace WizAccountant.Api.Insight;

internal static class ArPaymentBehaviorReplyFormat
{
    public static bool TryFormat(string operation, JsonElement root, out string reply)
    {
        reply = "";
        if (!operation.StartsWith("customer.payment.", StringComparison.OrdinalIgnoreCase))
            return false;

        if (root.TryGetProperty("lowConfidence", out var lc) && lc.ValueKind == JsonValueKind.True)
        {
            reply = root.TryGetProperty("finding", out var f) ? f.GetString() ?? "" : "";
            if (root.TryGetProperty("status", out var st))
                reply += Environment.NewLine + st.GetString();
            return !string.IsNullOrWhiteSpace(reply);
        }

        var lines = new List<string>();
        if (root.TryGetProperty("querySerial", out var qs))
            lines.Add($"Query run: {qs.GetString()}");
        if (root.TryGetProperty("operation", out var op))
            lines.Add($"Operation: {op.GetString()}");

        if (root.TryGetProperty("finding", out var finding))
            lines.Add(finding.GetString() ?? "");

        if (root.TryGetProperty("customers", out var customers) && customers.ValueKind == JsonValueKind.Array)
        {
            var title = operation.Contains("late", StringComparison.OrdinalIgnoreCase)
                ? "Slow / Late-Paying Customers"
                : "Top Prompt-Paying Customers";
            lines.Add("");
            lines.Add(title);
            var n = 0;
            foreach (var c in customers.EnumerateArray())
            {
                n++;
                if (n > 15) break;
                lines.Add(FormatCustomerLine(n, c));
            }
        }

        if (root.TryGetProperty("customer", out var single) && single.ValueKind == JsonValueKind.Object)
        {
            lines.Add("");
            lines.Add(FormatCustomerLine(1, single));
        }

        if (root.TryGetProperty("dataAsOfUtc", out var da))
            lines.Add($"Data as of: {da.GetString()}");

        reply = string.Join(Environment.NewLine, lines);
        return true;
    }

    private static string FormatCustomerLine(int rank, JsonElement c)
    {
        var code = c.TryGetProperty("code", out var cd) ? cd.GetString() : "";
        var name = c.TryGetProperty("name", out var nm) ? nm.GetString() : "";
        var score = c.TryGetProperty("paymentDisciplineScore", out var sc) ? sc.GetInt32() : 0;
        var pct = c.TryGetProperty("paidWithinTermsPercent", out var p) ? p.GetDecimal() : 0m;
        var avgPay = c.TryGetProperty("averagePaymentDays", out var ap) ? ap.GetDecimal() : 0m;
        var avgLate = c.TryGetProperty("averageDaysLate", out var al) ? al.GetDecimal() : 0m;
        var overdue = c.TryGetProperty("currentOverdueBalance", out var ob) ? ob.GetDecimal() : 0m;

        return $"{rank}. {name} ({code}) — Score {score}, Paid within terms {pct:N1}%, " +
               $"Avg pay days {avgPay:N1}, Avg days late {avgLate:N1}, Current overdue {overdue:N2}";
    }
}
