using System.Data.SqlClient;
using System.Globalization;
using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>Sales Order form enrichments — customer addresses, stock by warehouse, default selling price.</summary>
internal static class SalesOrderEnrichmentHandler
{
    public static string CustomerAddress(string companyConnectionString, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrWhiteSpace(companyConnectionString))
            throw new InvalidOperationException("Sage company database connection is not configured.");
        if (!parameters.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Parameter 'code' is required.");

        const string sql = """
            SELECT TOP 1 Account, Name,
                   Physical1, Physical2, Physical3, Physical4, Physical5,
                   Post1, Post2, Post3, Post4, Post5
            FROM Client
            WHERE Account = @code
            """;

        using var conn = new SqlConnection(companyConnectionString);
        conn.Open();
        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
        cmd.Parameters.AddWithValue("@code", code.Trim());
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            throw new InvalidOperationException($"Customer '{code}' was not found in Client.");

        var physical = JoinLines(
            ReadString(reader, "Physical1"), ReadString(reader, "Physical2"), ReadString(reader, "Physical3"),
            ReadString(reader, "Physical4"), ReadString(reader, "Physical5"));
        var delivery = JoinLines(
            ReadString(reader, "Post1"), ReadString(reader, "Post2"), ReadString(reader, "Post3"),
            ReadString(reader, "Post4"), ReadString(reader, "Post5"));

        return JsonSerializer.Serialize(new
        {
            code = ReadString(reader, "Account") ?? code,
            name = ReadString(reader, "Name"),
            physicalAddress = physical,
            deliveryAddress = delivery,
            postalAddress = physical,
            note = "Delivery = Post1–5; Physical/Postal = Physical1–5."
        });
    }

    public static string ItemStockQty(string companyConnectionString, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrWhiteSpace(companyConnectionString))
            throw new InvalidOperationException("Sage company database connection is not configured.");
        if (!parameters.TryGetValue("code", out var itemCode) || string.IsNullOrWhiteSpace(itemCode))
            throw new ArgumentException("Parameter 'code' (item code) is required.");

        parameters.TryGetValue("warehouse", out var warehouseCode);
        var warehouseFilter = ResolveWarehouseDocumentFilter(parameters);

        var sql = string.IsNullOrWhiteSpace(warehouseCode)
            ? $"""
              SELECT s.Code AS ItemCode, w.Code AS WarehouseCode, w.Name AS WarehouseName,
                     q.QtyOnHand, q.QtyOnSO, q.QtyOnPO,
                     w.bAllowToBuyInto AS AllowToBuyInto, w.bAllowToSellFrom AS AllowToSellFrom
              FROM StkItem s
              INNER JOIN _etblStockQtys q ON s.StockLink = q.StockID
              INNER JOIN WhseMst w ON q.WhseID = w.WhseLink
              WHERE s.ItemActive = 1 AND s.Code = @itemCode AND {warehouseFilter}
              ORDER BY w.Code
              """
            : $"""
              SELECT TOP 1 s.Code AS ItemCode, w.Code AS WarehouseCode, w.Name AS WarehouseName,
                     q.QtyOnHand, q.QtyOnSO, q.QtyOnPO,
                     w.bAllowToBuyInto AS AllowToBuyInto, w.bAllowToSellFrom AS AllowToSellFrom
              FROM StkItem s
              INNER JOIN _etblStockQtys q ON s.StockLink = q.StockID
              INNER JOIN WhseMst w ON q.WhseID = w.WhseLink
              WHERE s.ItemActive = 1 AND s.Code = @itemCode AND w.Code = @warehouseCode AND {warehouseFilter}
              """;

        using var conn = new SqlConnection(companyConnectionString);
        conn.Open();
        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
        cmd.Parameters.AddWithValue("@itemCode", itemCode.Trim());
        if (!string.IsNullOrWhiteSpace(warehouseCode))
            cmd.Parameters.AddWithValue("@warehouseCode", warehouseCode.Trim());

        var rows = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new
            {
                itemCode = ReadString(reader, "ItemCode"),
                warehouseCode = ReadString(reader, "WarehouseCode"),
                warehouseName = ReadString(reader, "WarehouseName"),
                qtyOnHand = ReadDecimal(reader, "QtyOnHand"),
                qtyOnSo = ReadDecimal(reader, "QtyOnSO"),
                qtyOnPo = ReadDecimal(reader, "QtyOnPO"),
                allowToBuyInto = ReadBool(reader, "AllowToBuyInto", "bAllowToBuyInto"),
                allowToSellFrom = ReadBool(reader, "AllowToSellFrom", "bAllowToSellFrom")
            });
        }

        if (rows.Count == 0)
        {
            return JsonSerializer.Serialize(new
            {
                itemCode,
                warehouseCode = warehouseCode ?? "",
                qtyOnHand = (decimal?)null,
                rows = Array.Empty<object>(),
                note = "No stock quantity row found for this item/warehouse (after document warehouse filter)."
            });
        }

        if (!string.IsNullOrWhiteSpace(warehouseCode) && rows[0] is not null)
        {
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(rows[0]));
            var root = doc.RootElement;
            return JsonSerializer.Serialize(new
            {
                itemCode,
                warehouseCode,
                warehouseName = root.TryGetProperty("warehouseName", out var wn) ? wn.GetString() : null,
                qtyOnHand = root.TryGetProperty("qtyOnHand", out var qh) ? qh.GetDecimal() : (decimal?)null,
                qtyOnSo = root.TryGetProperty("qtyOnSo", out var qs) ? qs.GetDecimal() : (decimal?)null,
                qtyOnPo = root.TryGetProperty("qtyOnPo", out var qp) ? qp.GetDecimal() : (decimal?)null,
                rows
            });
        }

        return JsonSerializer.Serialize(new { itemCode, rows, documentType = ResolveDocumentTypeLabel(parameters) });
    }

    /// <summary>
    /// Sales documents: bAllowToSellFrom = 1. Purchase documents: bAllowToBuyInto = 1.
    /// Pass parameter documentType = sales | purchase (alias screen).
    /// </summary>
    private static string ResolveWarehouseDocumentFilter(Dictionary<string, string> parameters)
    {
        var docType = ResolveDocumentTypeLabel(parameters);
        return docType == "purchase" ? "w.bAllowToBuyInto = 1" : "w.bAllowToSellFrom = 1";
    }

    private static string ResolveDocumentTypeLabel(Dictionary<string, string> parameters)
    {
        if (parameters.TryGetValue("documentType", out var documentType) && !string.IsNullOrWhiteSpace(documentType))
            return documentType.Trim().ToLowerInvariant() switch
            {
                "purchase" or "po" or "purchaseorder" => "purchase",
                _ => "sales"
            };

        if (parameters.TryGetValue("screen", out var screen) && !string.IsNullOrWhiteSpace(screen))
            return screen.Trim().ToLowerInvariant() switch
            {
                "purchase" or "po" or "purchaseorder" => "purchase",
                _ => "sales"
            };

        return "sales";
    }

    public static string ItemSellingPrice(string companyConnectionString, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrWhiteSpace(companyConnectionString))
            throw new InvalidOperationException("Sage company database connection is not configured.");
        if (!parameters.TryGetValue("code", out var itemCode) || string.IsNullOrWhiteSpace(itemCode))
            throw new ArgumentException("Parameter 'code' (item code) is required.");

        parameters.TryGetValue("unitId", out var unitIdText);
        _ = int.TryParse(unitIdText, out var requestedUnitId);

        const string sql = """
            SELECT s.Code AS ItemCode,
                   s.iUOMStockingUnitID AS StockingUnitId,
                   s.iUOMDefSellUnitID AS SellUnitId,
                   p.iUOMID AS PriceUomIdRaw,
                   CASE
                       WHEN p.iUOMID = 0 THEN
                           CASE WHEN ISNULL(s.iUOMDefSellUnitID, 0) > 0
                                THEN s.iUOMDefSellUnitID
                                ELSE s.iUOMStockingUnitID END
                       ELSE p.iUOMID
                   END AS PriceUomId,
                   n.cName AS PriceListName,
                   ISNULL(c.CurrencyCode, 'KES') AS CurrencyCode,
                   n.bDefault AS IsDefaultPriceList,
                   p.fExclPrice AS ExclusivePrice,
                   p.fInclPrice AS InclusivePrice
            FROM StkItem s
            INNER JOIN _etblPriceListPrices p ON s.StockLink = p.iStockID
            INNER JOIN _etblPriceListName n ON p.iPriceListNameID = n.IDPriceListName
            LEFT JOIN Currency c ON n.iCurrencyID = c.CurrencyLink
            WHERE s.Code = @itemCode
            ORDER BY n.bDefault DESC, n.cName,
                     CASE WHEN p.iUOMID = 0 THEN 0 ELSE 1 END
            """;

        using var conn = new SqlConnection(companyConnectionString);
        conn.Open();
        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
        cmd.Parameters.AddWithValue("@itemCode", itemCode.Trim());
        using var reader = cmd.ExecuteReader();

        var prices = new List<PriceListRow>();
        int sellUnitId = 0;
        int stockingUnitId = 0;
        string? currencyCode = null;
        string? priceListName = null;
        while (reader.Read())
        {
            if (sellUnitId == 0)
                sellUnitId = ReadInt(reader, "SellUnitId");
            if (stockingUnitId == 0)
                stockingUnitId = ReadInt(reader, "StockingUnitId");
            currencyCode ??= ReadString(reader, "CurrencyCode");
            priceListName ??= ReadString(reader, "PriceListName");

            prices.Add(new PriceListRow
            {
                PriceUomIdRaw = ReadInt(reader, "PriceUomIdRaw"),
                PriceUomId = ReadInt(reader, "PriceUomId"),
                PriceListName = ReadString(reader, "PriceListName"),
                IsDefaultPriceList = ReadBool(reader, "IsDefaultPriceList"),
                ExclusivePrice = ReadDecimal(reader, "ExclusivePrice"),
                InclusivePrice = ReadDecimal(reader, "InclusivePrice")
            });
        }

        if (prices.Count == 0)
        {
            return JsonSerializer.Serialize(new
            {
                itemCode,
                sellUnitId,
                stockingUnitId,
                exclusivePrice = (decimal?)null,
                inclusivePrice = (decimal?)null,
                note = "No price list row found for this item."
            });
        }

        var resolved = ResolvePriceForUnit(prices, sellUnitId, stockingUnitId, requestedUnitId);
        return JsonSerializer.Serialize(new
        {
            itemCode,
            sellUnitId,
            stockingUnitId,
            priceUomId = resolved.PriceUomId,
            priceListName = resolved.PriceListName ?? priceListName,
            currencyCode,
            isDefaultPriceList = resolved.IsDefaultPriceList,
            exclusivePrice = resolved.ExclusivePrice,
            inclusivePrice = resolved.InclusivePrice,
            prices = prices.Select(p => new
            {
                priceUomIdRaw = p.PriceUomIdRaw,
                priceUomId = p.PriceUomId,
                priceListName = p.PriceListName,
                isDefaultPriceList = p.IsDefaultPriceList,
                exclusivePrice = p.ExclusivePrice,
                inclusivePrice = p.InclusivePrice
            }),
            note = "iUOMID = 0: price applies to sell unit when set, otherwise stocking unit; iUOMID > 0: price applies to that unit."
        });
    }

    private sealed class PriceListRow
    {
        public int PriceUomIdRaw { get; init; }
        public int PriceUomId { get; init; }
        public string? PriceListName { get; init; }
        public bool IsDefaultPriceList { get; init; }
        public decimal? ExclusivePrice { get; init; }
        public decimal? InclusivePrice { get; init; }
    }

    private static PriceListRow ResolvePriceForUnit(
        IReadOnlyList<PriceListRow> prices,
        int sellUnitId,
        int stockingUnitId,
        int requestedUnitId)
    {
        var defaultPriceUomId = sellUnitId > 0 ? sellUnitId : stockingUnitId;

        if (requestedUnitId > 0)
        {
            var exact = prices.FirstOrDefault(p => p.PriceUomId == requestedUnitId);
            if (exact is not null) return exact;
        }

        return prices.FirstOrDefault(p => p.IsDefaultPriceList && p.PriceUomIdRaw == 0)
               ?? prices.FirstOrDefault(p => p.PriceUomIdRaw == 0)
               ?? (defaultPriceUomId > 0
                   ? prices.FirstOrDefault(p => p.IsDefaultPriceList && p.PriceUomId == defaultPriceUomId)
                     ?? prices.FirstOrDefault(p => p.PriceUomId == defaultPriceUomId)
                   : null)
               ?? prices.FirstOrDefault(p => p.IsDefaultPriceList)
               ?? prices[0];
    }

    public static string ItemSalesTax(string companyConnectionString, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrWhiteSpace(companyConnectionString))
            throw new InvalidOperationException("Sage company database connection is not configured.");
        if (!parameters.TryGetValue("code", out var itemCode) || string.IsNullOrWhiteSpace(itemCode))
            throw new ArgumentException("Parameter 'code' (item code) is required.");

        const string sql = """
            SELECT TOP 1
                   t1.Code AS SalesTaxCode,
                   t1.TaxRate AS SalesTaxRate,
                   t1.Description AS SalesTaxDescription,
                   d.TTInvID AS SalesTaxId
            FROM StkItem s
            INNER JOIN _etblStockDetails d ON s.StockLink = d.StockID
            INNER JOIN TaxRate t1 ON d.TTInvID = t1.idTaxRate
            WHERE s.Code = @itemCode
            ORDER BY d.idStockDetails
            """;

        using var conn = new SqlConnection(companyConnectionString);
        conn.Open();
        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
        cmd.Parameters.AddWithValue("@itemCode", itemCode.Trim());
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return JsonSerializer.Serialize(new
            {
                itemCode,
                salesTaxCode = (string?)null,
                salesTaxRate = (decimal?)null,
                note = "No sales tax (TTInvID) found in _etblStockDetails for this item."
            });
        }

        return JsonSerializer.Serialize(new
        {
            itemCode = ReadString(reader, "ItemCode") ?? itemCode,
            salesTaxCode = ReadString(reader, "SalesTaxCode"),
            salesTaxRate = ReadDecimal(reader, "SalesTaxRate", "TaxRate", "Rate"),
            salesTaxDescription = ReadString(reader, "SalesTaxDescription"),
            salesTaxId = ReadString(reader, "SalesTaxId")
        });
    }

    public static string ItemUnits(string companyConnectionString, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrWhiteSpace(companyConnectionString))
            throw new InvalidOperationException("Sage company database connection is not configured.");
        if (!parameters.TryGetValue("code", out var itemCode) || string.IsNullOrWhiteSpace(itemCode))
            throw new ArgumentException("Parameter 'code' (item code) is required.");

        const string headerSql = """
            SELECT TOP 1 si.iUOMStockingUnitID AS StockingUnitId,
                   si.iUOMDefPurchaseUnitID AS PurchaseUnitId,
                   si.iUOMDefSellUnitID AS SellUnitId
            FROM StkItem si
            WHERE si.Code = @itemCode
            """;

        const string unitsSql = """
            SELECT u.idUnits AS UnitId, u.cUnitCode AS UnitCode, u.cUnitDescription AS UnitDescription
            FROM StkItem si
            INNER JOIN _etblUnits u ON u.idUnits IN (si.iUOMStockingUnitID, si.iUOMDefPurchaseUnitID, si.iUOMDefSellUnitID)
            WHERE si.Code = @itemCode AND u.idUnits > 0
            ORDER BY u.cUnitCode
            """;

        using var conn = new SqlConnection(companyConnectionString);
        conn.Open();

        int stockingUnitId = 0, purchaseUnitId = 0, sellUnitId = 0;
        using (var headerCmd = new SqlCommand(headerSql, conn) { CommandTimeout = 60 })
        {
            headerCmd.Parameters.AddWithValue("@itemCode", itemCode.Trim());
            using var reader = headerCmd.ExecuteReader();
            if (reader.Read())
            {
                stockingUnitId = ReadInt(reader, "StockingUnitId");
                purchaseUnitId = ReadInt(reader, "PurchaseUnitId");
                sellUnitId = ReadInt(reader, "SellUnitId");
            }
        }

        var units = new List<object>();
        var unitIds = new List<int>();
        using (var unitsCmd = new SqlCommand(unitsSql, conn) { CommandTimeout = 60 })
        {
            unitsCmd.Parameters.AddWithValue("@itemCode", itemCode.Trim());
            using var reader = unitsCmd.ExecuteReader();
            while (reader.Read())
            {
                var id = ReadInt(reader, "UnitId");
                if (id <= 0) continue;
                unitIds.Add(id);
                units.Add(new
                {
                    id,
                    code = ReadString(reader, "UnitCode") ?? "",
                    description = ReadString(reader, "UnitDescription") ?? ""
                });
            }
        }

        var conversions = new List<object>();
        if (unitIds.Count > 0)
        {
            var ids = string.Join(",", unitIds);
            var convSql = $"""
                SELECT uc.iUnitAID, uc.fUnitAQty, uc.iUnitBID, uc.fUnitBQty
                FROM _etblUnitConversion uc
                WHERE uc.iUnitAID IN ({ids}) AND uc.iUnitBID IN ({ids})
                """;
            using var convCmd = new SqlCommand(convSql, conn) { CommandTimeout = 60 };
            using var reader = convCmd.ExecuteReader();
            while (reader.Read())
            {
                conversions.Add(new
                {
                    unitAId = ReadInt(reader, "iUnitAID"),
                    unitAQty = ReadDecimal(reader, "fUnitAQty") ?? 1m,
                    unitBId = ReadInt(reader, "iUnitBID"),
                    unitBQty = ReadDecimal(reader, "fUnitBQty") ?? 1m
                });
            }
        }

        return JsonSerializer.Serialize(new
        {
            itemCode,
            stockingUnitId,
            purchaseUnitId,
            sellUnitId,
            units,
            conversions,
            note = "Units from StkItem UOM IDs; conversions from _etblUnitConversion. Qty on hand is in stocking unit."
        });
    }

    private static int ReadInt(SqlDataReader reader, params string[] names)
    {
        foreach (var name in names)
        {
            try
            {
                var ord = reader.GetOrdinal(name);
                if (reader.IsDBNull(ord)) continue;
                return Convert.ToInt32(reader.GetValue(ord), CultureInfo.InvariantCulture);
            }
            catch (IndexOutOfRangeException) { }
        }

        return 0;
    }

    private static string JoinLines(params string?[] parts) =>
        string.Join("\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

    private static string? ReadString(SqlDataReader reader, params string[] names)
    {
        foreach (var name in names)
        {
            try
            {
                var ord = reader.GetOrdinal(name);
                if (reader.IsDBNull(ord)) continue;
                return Convert.ToString(reader.GetValue(ord))?.Trim();
            }
            catch (IndexOutOfRangeException) { }
        }

        return null;
    }

    private static decimal? ReadDecimal(SqlDataReader reader, params string[] names)
    {
        foreach (var name in names)
        {
            try
            {
                var ord = reader.GetOrdinal(name);
                if (reader.IsDBNull(ord)) continue;
                return Convert.ToDecimal(reader.GetValue(ord), CultureInfo.InvariantCulture);
            }
            catch (IndexOutOfRangeException) { }
        }

        return null;
    }

    private static bool ReadBool(SqlDataReader reader, params string[] names)
    {
        foreach (var name in names)
        {
            try
            {
                var ord = reader.GetOrdinal(name);
                if (reader.IsDBNull(ord)) continue;
                var v = reader.GetValue(ord);
                return v switch
                {
                    bool b => b,
                    byte b => b != 0,
                    short s => s != 0,
                    int i => i != 0,
                    _ => string.Equals(Convert.ToString(v), "true", StringComparison.OrdinalIgnoreCase) ||
                         Convert.ToString(v) == "1"
                };
            }
            catch (IndexOutOfRangeException) { }
        }

        return false;
    }
}
