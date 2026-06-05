using System.Globalization;
using System.Text.Json;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

/// <summary>
/// Save sales quotations/orders and process invoices via Sage Evolution SDK.
/// Follows "C Sales Orders" — Order Entry (SalesOrder.Save / SalesOrder.Process / SalesOrderQuotation).
/// </summary>
internal static class SalesOrderSaveHandler
{
    public static string Save(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<SalesOrderSavePayload>(payloadJson,
                         new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                     ?? throw new ArgumentException("Invalid sales order payload.");

        var docType = (payload.DocumentType ?? "").Trim().ToLowerInvariant();
        if (docType is not ("quote" or "order" or "invoice"))
            throw new ArgumentException("documentType must be quote, order, or invoice.");

        if (string.IsNullOrWhiteSpace(payload.CustomerCode))
            throw new ArgumentException("customerCode is required.");
        if (string.IsNullOrWhiteSpace(payload.OrderNo))
            throw new ArgumentException("orderNo is required.");
        if (payload.Lines is null || payload.Lines.Count == 0)
            throw new ArgumentException("At least one line is required.");

        if (!DatabaseContext.BeginTran())
            throw new InvalidOperationException("Could not begin Sage transaction.");

        try
        {
            // Quote: SalesOrderQuotation + Save()
            // Order: SalesOrder + Save()
            // Invoice: SalesOrder + Save() then Detail[i].ToProcess + Process()
            SalesOrder order = docType == "quote" ? new SalesOrderQuotation() : new SalesOrder();
            ApplyHeader(order, payload);
            ApplyLines(order, payload, docType);

            string? invoiceReference = null;
            if (docType == "invoice")
            {
                order.Save();
                for (var i = 0; i < order.Detail.Count; i++)
                {
                    var src = payload.Lines[i];
                    var toProcess = src.ConfirmQty > 0 ? src.ConfirmQty : src.Quantity;
                    if (toProcess <= 0)
                        throw new ArgumentException($"Line {i + 1}: confirm quantity must be greater than zero to process.");
                    order.Detail[i].ToProcess = toProcess;
                }

                invoiceReference = order.Process();
            }
            else
            {
                order.Save();
            }

            var savedOrderNo = ResolveSavedDocumentNo(order, payload);

            DatabaseContext.CommitTran();

            return JsonSerializer.Serialize(new
            {
                ok = true,
                operation = "salesorder.save",
                documentType = docType,
                orderNo = savedOrderNo,
                invoiceReference,
                message = docType switch
                {
                    "quote" => $"Sales quotation {savedOrderNo} saved.",
                    "order" => $"Sales order {savedOrderNo} placed.",
                    _ => $"Sales order {savedOrderNo} processed to invoice {invoiceReference ?? "(see Sage)"}."
                }
            });
        }
        catch
        {
            DatabaseContext.RollbackTran();
            throw;
        }
    }

    private static void ApplyHeader(SalesOrder order, SalesOrderSavePayload payload)
    {
        var customer = new Customer(payload.CustomerCode.Trim());
        order.Customer = customer;
        order.InvoiceTo = customer.PostalAddress.Condense();

        if (order is SalesOrderQuotation quotation)
            quotation.QuoteNo = payload.OrderNo.Trim();
        else
            order.OrderNo = payload.OrderNo.Trim();

        if (!string.IsNullOrWhiteSpace(payload.ExternalOrderNo))
            order.ExternalOrderNo = payload.ExternalOrderNo.Trim();

        if (TryParseDate(payload.OrderDate, out var orderDate))
            order.OrderDate = orderDate;
        if (TryParseDate(payload.DueDate, out var dueDate))
            order.DueDate = dueDate;
        if (TryParseDate(payload.InvoiceDate, out var invoiceDate))
            order.InvoiceDate = invoiceDate;

        if (!string.IsNullOrWhiteSpace(payload.Description))
            order.Description = payload.Description.Trim();

        if (!string.IsNullOrWhiteSpace(payload.DeliverTo))
            order.DeliverTo = ParseAddress(payload.DeliverTo);

        // SDK: set TaxMode before line unit prices.
        order.TaxMode = payload.TaxInclusive ? TaxMode.Inclusive : TaxMode.Exclusive;

        if (!string.IsNullOrWhiteSpace(payload.RepresentativeCode))
            order.Representative = new SalesRepresentative(payload.RepresentativeCode.Trim());
        if (!string.IsNullOrWhiteSpace(payload.ProjectCode))
            order.Project = new Project(payload.ProjectCode.Trim());

        if (payload.DiscountAmount > 0)
            order.Discount = (double)payload.DiscountAmount;
        else if (payload.DiscountPercent > 0)
            order.DiscountPercent = (double)payload.DiscountPercent;
    }

    private static string ResolveSavedDocumentNo(SalesOrder order, SalesOrderSavePayload payload)
    {
        if (order is SalesOrderQuotation quotation)
        {
            var quoteNo = quotation.QuoteNo?.Trim();
            return string.IsNullOrWhiteSpace(quoteNo) ? payload.OrderNo.Trim() : quoteNo;
        }

        var orderNo = order.OrderNo?.Trim();
        return string.IsNullOrWhiteSpace(orderNo) ? payload.OrderNo.Trim() : orderNo;
    }

    private static Address ParseAddress(string value)
    {
        var lines = value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        return lines.Length switch
        {
            0 => new Address(value.Trim()),
            1 => new Address(lines[0]),
            2 => new Address(lines[0], lines[1], ""),
            3 => new Address(lines[0], lines[1], lines[2]),
            _ => new Address(
                lines[0],
                lines.Length > 1 ? lines[1] : "",
                lines.Length > 2 ? lines[2] : "",
                lines.Length > 3 ? lines[3] : "",
                lines.Length > 4 ? lines[4] : "",
                lines.Length > 5 ? lines[5] : "")
        };
    }

    /// <summary>
    /// Builds lines using the SDK OrderDetail pattern from the C Sales Orders guide:
    /// OrderDetail OD = new OrderDetail(); SO.Detail.Add(OD); OD.InventoryItem = ...; OD.Quantity = ...;
    /// </summary>
    private static void ApplyLines(SalesOrder order, SalesOrderSavePayload payload, string docType)
    {
        foreach (var line in payload.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.ItemCode))
                throw new ArgumentException("Each line requires itemCode.");
            if (line.Quantity <= 0)
                throw new ArgumentException($"Line item {line.ItemCode}: quantity must be greater than zero.");

            var module = (line.Module ?? "ST").Trim().ToUpperInvariant();
            var detail = new OrderDetail();
            order.Detail.Add(detail);

            if (module == "GL")
            {
                detail.GLAccount = new GLAccount(line.ItemCode.Trim());
            }
            else
            {
                detail.InventoryItem = new InventoryItem(line.ItemCode.Trim());
                if (!string.IsNullOrWhiteSpace(line.WarehouseCode))
                    detail.Warehouse = new Warehouse(line.WarehouseCode.Trim());
            }

            detail.Quantity = line.Quantity;

            // Quotation sample sets ToProcess before Save(); place-order sample does not process.
            if (docType == "quote")
                detail.ToProcess = line.Quantity;

            if (line.UnitPrice > 0)
                detail.UnitSellingPrice = line.UnitPrice;

            if (!string.IsNullOrWhiteSpace(line.Description))
                detail.Description = line.Description.Trim();

            if (!string.IsNullOrWhiteSpace(line.TaxCode))
                detail.TaxType = new TaxRate(line.TaxCode.Trim());

            if (line.DiscountPercent > 0)
                detail.DiscountPercent = line.DiscountPercent;
        }
    }

    private static bool TryParseDate(string? value, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out date)
               || DateTime.TryParse(value, out date);
    }

    private sealed class SalesOrderSavePayload
    {
        public string? DocumentType { get; set; }
        public string? CustomerCode { get; set; }
        public string? OrderNo { get; set; }
        public string? ExternalOrderNo { get; set; }
        public string? OrderDate { get; set; }
        public string? DueDate { get; set; }
        public string? InvoiceDate { get; set; }
        public string? Description { get; set; }
        public string? DeliverTo { get; set; }
        public string? ProjectCode { get; set; }
        public string? RepresentativeCode { get; set; }
        public string? CurrencyCode { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal DiscountAmount { get; set; }
        public bool TaxInclusive { get; set; } = true;
        public List<SalesOrderSaveLinePayload> Lines { get; set; } = new();
    }

    private sealed class SalesOrderSaveLinePayload
    {
        public string? Module { get; set; }
        public string? ItemCode { get; set; }
        public string? Description { get; set; }
        public string? WarehouseCode { get; set; }
        public double Quantity { get; set; }
        public double ConfirmQty { get; set; }
        public double UnitPrice { get; set; }
        public string? TaxCode { get; set; }
        public double DiscountPercent { get; set; }
    }
}
