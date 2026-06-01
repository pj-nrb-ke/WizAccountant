using System.Text.Json;

namespace WizConnector.Service.Sage;

internal static class VatPayableEstimateHandler
{
    public const string QuerySerial = "SAGE-VAT-PAYABLE-001";

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
            countOnly = true,
            aggregationMode = true,
            finding = $"Estimated VAT payable: {payable:N2} (output {output:N2} − input {input:N2}).",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
