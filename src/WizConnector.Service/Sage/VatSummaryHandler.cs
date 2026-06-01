using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class VatSummaryHandler
{
    public const string QuerySerial = "SAGE-VAT-SUMMARY-001";

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var (from, to) = VatSqlHelper.ParsePeriod(parameters);
        var output = VatOutputHandler.RunOutputTotal(connectionString, from, to);
        var input = VatInputHandler.RunInputTotal(connectionString, from, to);
        var payable = output - input;

        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            dateFrom = from.ToString("yyyy-MM-dd"),
            dateTo = to.ToString("yyyy-MM-dd"),
            outputVat = output,
            inputVat = input,
            estimatedVatPayable = payable,
            countOnly = false,
            aggregationMode = true,
            finding = $"VAT summary {from:yyyy-MM-dd} to {to:yyyy-MM-dd}: Output {output:N2}, Input {input:N2}, Payable {payable:N2}.",
            note = "VAT from InvNum tax fields — not guessed from invoice totals.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
