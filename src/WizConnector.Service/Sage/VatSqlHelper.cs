using System.Data.SqlClient;

namespace WizConnector.Service.Sage;

/// <summary>VAT aggregation from InvNum tax fields (SAGE-TRAIN-005).</summary>
internal static class VatSqlHelper
{
    public static (DateTime From, DateTime To) ParsePeriod(Dictionary<string, string> parameters, string? message = null)
    {
        var month = GlSqlHelper.ExtractMonthFromMessage(message ?? parameters.GetValueOrDefault("message"));
        var year = InvNumSqlHelper.ParseYear(parameters, message);
        if (month.HasValue)
        {
            var from = new DateTime(year, month.Value, 1);
            return (from, from.AddMonths(1).AddDays(-1));
        }
        return InvNumSqlHelper.ParseDateRange(parameters, message);
    }

    public static decimal RunScalar(string connectionString, string sql, DateTime from, DateTime to)
    {
        using var conn = new SqlConnection(connectionString);
        conn.Open();
        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 120 };
        InvNumSqlHelper.AddDateParameters(cmd, from, to);
        var result = cmd.ExecuteScalar();
        return result is null or DBNull ? 0m : Convert.ToDecimal(result);
    }
}
