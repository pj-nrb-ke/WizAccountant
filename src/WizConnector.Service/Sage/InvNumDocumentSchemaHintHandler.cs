using System.Data.SqlClient;

using System.Text.Json;



namespace WizConnector.Service.Sage;



internal static class InvNumDocumentSchemaHintHandler

{

    public const string QuerySerial = "INSIGHT-INVNUM-DOCUMENTS-HINT-001";

    public const string Operation = "insight.sql.invnum-documents-hint";



    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)

    {

        _ = parameters;

        if (string.IsNullOrWhiteSpace(companyConnectionString))

            throw new InvalidOperationException("Sage company database connection is not configured.");



        using var conn = new SqlConnection(companyConnectionString);

        conn.Open();

        var hint = InvNumDocumentSqlHelper.BuildHint(conn);

        return JsonSerializer.Serialize(new

        {

            querySerial = QuerySerial,

            operation = Operation,

            invNumColumns = hint.InvNumColumns,

            documentFlagColumn = hint.DocumentFlagColumn,

            salesDocTypeFilter = hint.SalesDocTypeFilter,

            purchaseDocTypeFilter = hint.PurchaseDocTypeFilter,

            docStateAnalyticsExclusionFilter = hint.DocStateAnalyticsExclusionFilter,

            docTypeDocStateMatrix = hint.DocTypeDocStateMatrix,

            docTypeFlagMatrix = hint.DocTypeFlagMatrix,

            rtsSample = hint.RtsSample,

            postArDebitSample = hint.PostArDebitSample,

            postApCreditSample = hint.PostApCreditSample,

            inventoryTrCodes = hint.InventoryTrCodes,

            trCodeTableName = hint.TrCodeTableName,

            warnings = hint.Warnings,

            docTypeReference = new[]

            {

                new { docType = 0, name = "Invoice" },

                new { docType = 1, name = "Credit Note" },

                new { docType = 2, name = "GRV" },

                new { docType = 3, name = "RTS" },

                new { docType = 4, name = "Sales Order" },

                new { docType = 5, name = "Purchase Order" },

                new { docType = 6, name = "POS Inv" },

                new { docType = 7, name = "POS Crn" },

                new { docType = 8, name = "Job Costing Invoice" }

            },

            dataAsOfUtc = DateTimeOffset.UtcNow

        });

    }

}


