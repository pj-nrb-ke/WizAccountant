using System.Data.SqlClient;



namespace WizConnector.Service.Sage;



/// <summary>Resolves InvNum document columns and discovery queries per company DB (SAGE-DOCS-001).</summary>

internal static class InvNumDocumentSqlHelper

{

    private static readonly string[] DocumentFlagColumnCandidates = ["DocFlag", "InvDocState", "DocumentFlag"];



    internal sealed record InvNumDocumentHint(

        IReadOnlyList<string> InvNumColumns,

        string? DocumentFlagColumn,

        string SalesDocTypeFilter,

        string PurchaseDocTypeFilter,

        string DocStateAnalyticsExclusionFilter,

        IReadOnlyList<Dictionary<string, object?>> DocTypeDocStateMatrix,

        IReadOnlyList<Dictionary<string, object?>> DocTypeFlagMatrix,

        IReadOnlyList<Dictionary<string, object?>> RtsSample,

        IReadOnlyList<Dictionary<string, object?>> PostArDebitSample,

        IReadOnlyList<Dictionary<string, object?>> PostApCreditSample,

        IReadOnlyList<Dictionary<string, object?>> InventoryTrCodes,

        string? TrCodeTableName,

        IReadOnlyList<string> Warnings);



    public static InvNumDocumentHint BuildHint(SqlConnection conn)

    {

        var warnings = new List<string>();

        var invNumColumns = LoadColumns(conn, "InvNum").OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();

        var flagColumn = ResolveDocumentFlagColumn(invNumColumns);

        if (flagColumn is null)

            warnings.Add("Document flag column not found on InvNum — sales filter falls back to DocType 0 OR 4 without flag check.");



        var salesFilter = BuildSalesDocTypeFilter(flagColumn);

        var docTypeDocState = QueryRows(conn,

            """

            SELECT DocType, DocState, COUNT(*) AS Cnt

            FROM InvNum

            GROUP BY DocType, DocState

            ORDER BY DocType, DocState

            """);



        IReadOnlyList<Dictionary<string, object?>> docTypeFlag;

        if (flagColumn is not null)

        {

            docTypeFlag = QueryRows(conn,

                $"""

                SELECT DocType, {flagColumn} AS DocFlag, COUNT(*) AS Cnt

                FROM InvNum

                WHERE DocType IN (0, 2, 4, 5)

                GROUP BY DocType, {flagColumn}

                ORDER BY DocType, {flagColumn}

                """);

        }

        else

        {

            docTypeFlag = [];

        }



        var rtsSample = QueryRows(conn,

            """

            SELECT TOP 10 AutoIndex, DocType, InvNumber, InvDate, AccountID, InvTotIncl, DocState

            FROM InvNum

            WHERE DocType = 3

            ORDER BY InvDate DESC

            """);



        var trCodeTable = ResolveTrCodeTable(conn);

        var inventoryTrCodes = trCodeTable is null

            ? []

            : QueryRows(conn,

                $"""

                SELECT TOP 30 iModule, idTrCodes, Code, Description

                FROM {trCodeTable}

                WHERE iModule = 11

                ORDER BY Code

                """);



        if (trCodeTable is null)

            warnings.Add("TrCode table not found — inventory transfer TrCodes cannot be listed.");



        var postArDebit = QueryRows(conn, BuildPostArDebitSql(trCodeTable));

        var postApCredit = QueryRows(conn, BuildPostApCreditSql(trCodeTable));



        return new InvNumDocumentHint(

            invNumColumns,

            flagColumn,

            salesFilter,

            InvNumSqlHelper.PurchaseDocTypeFilter,

            InvNumSqlHelper.DocStateAnalyticsExclusionFilter,

            docTypeDocState,

            docTypeFlag,

            rtsSample,

            postArDebit,

            postApCredit,

            inventoryTrCodes,

            trCodeTable,

            warnings);

    }



    internal static string? ResolveDocumentFlagColumn(IReadOnlyCollection<string> invNumColumns) =>

        DocumentFlagColumnCandidates.FirstOrDefault(invNumColumns.Contains);



    internal static string BuildSalesDocTypeFilter(string? flagColumn) =>

        flagColumn is null

            ? "(H.DocType = 0 OR H.DocType = 4)"

            : $"(H.DocType = 0 OR (H.DocType = 4 AND ISNULL(H.{flagColumn}, 0) IN (0, 2)))";



    private static string? ResolveTrCodeTable(SqlConnection conn)

    {

        foreach (var name in new[] { "TrCodes", "TrCode", "_btblTrCodes" })

        {

            if (LoadColumns(conn, name).Count > 0)

                return name;

        }



        return null;

    }



    private static string BuildPostArDebitSql(string? trCodeTable)

    {

        if (trCodeTable is null)

        {

            return """

                SELECT TOP 20 AutoIdx, TrCodeID, Reference, Description, Debit, Credit, TxDate, InvNumKey

                FROM PostAR

                WHERE ISNULL(Debit, 0) > 0

                ORDER BY TxDate DESC

                """;

        }



        return $"""

            SELECT TOP 20 P.AutoIdx, P.TrCodeID, T.Code AS TrCode, T.Description AS TrCodeDesc,

                   P.Reference, P.Description, P.Debit, P.Credit, P.TxDate, P.InvNumKey

            FROM PostAR P

            LEFT JOIN {trCodeTable} T ON T.idTrCodes = P.TrCodeID

            WHERE ISNULL(P.Debit, 0) > 0

            ORDER BY P.TxDate DESC

            """;

    }



    private static string BuildPostApCreditSql(string? trCodeTable)

    {

        if (trCodeTable is null)

        {

            return """

                SELECT TOP 20 AutoIdx, TrCodeID, Reference, Description, Debit, Credit, TxDate

                FROM PostAP

                WHERE ISNULL(Credit, 0) > 0

                ORDER BY TxDate DESC

                """;

        }



        return $"""

            SELECT TOP 20 P.AutoIdx, P.TrCodeID, T.Code AS TrCode, T.Description AS TrCodeDesc,

                   P.Reference, P.Description, P.Debit, P.Credit, P.TxDate

            FROM PostAP P

            LEFT JOIN {trCodeTable} T ON T.idTrCodes = P.TrCodeID

            WHERE ISNULL(P.Credit, 0) > 0

            ORDER BY P.TxDate DESC

            """;

    }



    private static List<Dictionary<string, object?>> QueryRows(SqlConnection conn, string sql)

    {

        var rows = new List<Dictionary<string, object?>>();

        try

        {

            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 120 };

            using var reader = cmd.ExecuteReader();

            while (reader.Read())

            {

                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

                for (var i = 0; i < reader.FieldCount; i++)

                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);

                rows.Add(row);

            }

        }

        catch (SqlException ex)

        {

            rows.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)

            {

                ["error"] = ex.Message

            });

        }



        return rows;

    }



    private static HashSet<string> LoadColumns(SqlConnection conn, string table)

    {

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var cmd = new SqlCommand(

            """

            SELECT COLUMN_NAME

            FROM INFORMATION_SCHEMA.COLUMNS

            WHERE TABLE_NAME = @tableName

            """,

            conn);

        cmd.Parameters.AddWithValue("@tableName", table);

        using var reader = cmd.ExecuteReader();

        while (reader.Read())

            set.Add(reader.GetString(0));

        return set;

    }

}


