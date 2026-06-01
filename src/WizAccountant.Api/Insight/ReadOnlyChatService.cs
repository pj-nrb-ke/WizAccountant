using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WizAccountant.Contracts;

namespace WizAccountant.Api.Insight;

public sealed class ReadOnlyChatService(AppDbContext db, JobService jobs, ILogger<ReadOnlyChatService> logger)
{
    private const string Guardrail =
        "I only read live Sage data. I cannot post journals or payments. Ask an approver in Phase 3 for writes.";

    public async Task<ChatMessageResponse> AskAsync(ChatMessageRequest request, string? tenantId, CancellationToken ct)
    {
        var site = await db.Sites.FindAsync([request.SiteId], ct);
        if (site is null) throw new InvalidOperationException("Site not found.");

        var conversation = request.ConversationId is { } cid
            ? await db.ChatConversations.FindAsync([cid], ct)
            : null;

        if (conversation is null)
        {
            conversation = new ChatConversationRecord
            {
                ConversationId = Guid.NewGuid(),
                TenantId = tenantId ?? site.TenantId,
                SiteId = request.SiteId,
                Title = Truncate(request.Message, 60),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            db.ChatConversations.Add(conversation);
        }

        db.ChatMessages.Add(new ChatMessageRecord
        {
            MessageId = Guid.NewGuid(),
            ConversationId = conversation.ConversationId,
            Role = "user",
            Content = request.Message,
            TimestampUtc = DateTimeOffset.UtcNow
        });

        var intentClassification = SageIntentEngine.Classify(request.Message);
        var intentResolution = SageIntentResolver.Resolve(request.Message);
        var (operation, parameters, toolsUsed) = PlanToolCall(request.Message, intentClassification);

        toolsUsed.Add($"domain:{intentClassification.Domain}:{intentClassification.DomainConfidence:F2}");
        if (intentResolution.HandlerId is not null)
            toolsUsed.Add($"handler:{intentResolution.HandlerId}");
        else if (intentResolution.Route == SageIntentResolver.RouteKind.MegaDigestFallback)
            toolsUsed.Add($"route:mega.digest:{intentResolution.MegaDigestId}");

        if (operation is not null && QueryAggregationMode.RejectMisroutedListing(request.Message, operation))
        {
            operation = null;
            toolsUsed.Clear();
        }

        if (operation is not null && RankingQueryPolicy.RejectMisroutedBulkList(intentClassification, operation))
        {
            operation = null;
            toolsUsed.Clear();
        }

        string reply;
        ChatGridDto? grid = null;
        var citations = new List<string>();
        var dataAsOf = DateTimeOffset.UtcNow;

        if (operation is null)
        {
            if (MegaDigestFallbackMatcher.TryBuildReply(request.Message, intentClassification, out var digestReply, out var digestCitations))
            {
                reply = digestReply;
                citations.AddRange(digestCitations);
                toolsUsed.Add("mega.digest.fallback");
                toolsUsed.Add($"intent:{intentClassification.PrimaryIntent}:{intentClassification.Confidence:F2}");
                if (intentClassification.SecondaryIntent.HasValue)
                    toolsUsed.Add($"intent2:{intentClassification.SecondaryIntent}:{intentClassification.SecondaryConfidence:F2}");
                if (intentClassification.IsAmbiguous)
                    toolsUsed.Add("intent:ambiguous");
            }
            else if (QueryAggregationMode.IsAggregationQuery(request.Message))
            {
                reply = QueryAggregationMode.BuildMisrouteMessage(request.Message, "customer.openitems")
                        + "\n\n" + Guardrail;
                toolsUsed.Add("aggregation.blocked");
            }
            else if (RankingQueryPolicy.IsRankingClassification(intentClassification))
            {
                reply = RankingQueryPolicy.BuildBlockedMessage(request.Message, "customer.list", intentClassification)
                        + "\n\n" + Guardrail;
                toolsUsed.Add("ranking.blocked");
            }
            else
            {
                reply = SageChatDomain.TryBuildEducationalReply(request.Message)
                        ?? FormatRecognizedIntentFallback(site.SiteName, request.Message, intentClassification);
            }
        }
        else if (!InsightReadOnlyTools.IsAllowed(operation))
        {
            reply = "That action is not in the read-only allowlist. " + Guardrail;
        }
        else
        {
            try
            {
                var job = await jobs.RunAndWaitAsync(new CreateJobRequest
                {
                    SiteId = request.SiteId,
                    Operation = operation,
                    Parameters = parameters,
                    RequestedBy = "insight-chat"
                }, 90, ct);

                dataAsOf = job.UpdatedAtUtc ?? job.CreatedAtUtc;
                if (job.Status == JobStatus.Failed)
                {
                    reply = $"Sage read failed: {job.Error}";
                    if (job.Error?.Contains("Unsupported operation: inventory.gl.reconcile", StringComparison.OrdinalIgnoreCase) == true)
                        reply += "\n\nThe connector on this PC is out of date. Close WizConnector, run WizPilot → Build pilot apps, then Start service + tray.";
                    reply += " " + Guardrail;
                }
                else
                {
                    var queryDesc = FormatQueryDescription(operation, parameters, request.Message);
                    (reply, citations) = FormatJobResult(operation, job.ResultJson, request.Message);
                    if (!string.IsNullOrEmpty(queryDesc))
                        reply = queryDesc + "\n\n" + reply;
                    var layerNote = SageChatDomain.LayerFootnote(operation);
                    if (!string.IsNullOrEmpty(layerNote))
                        reply += "\n\n" + layerNote;
                    reply += $"\n\n(Data as of {dataAsOf:u})";

                    grid = QueryAggregationMode.ShouldSuppressGrid(request.Message, operation, job.ResultJson)
                        ? null
                        : ChatResultGridBuilder.TryBuild(
                            operation,
                            job.ResultJson,
                            intentClassification,
                            RankingQueryPolicy.ResolveMaxGridRows(operation, parameters));
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Chat tool {Operation} failed", operation);
                reply = $"Could not complete read ({ex.Message}). Is the connector online? " + Guardrail;
            }
        }

        db.ChatMessages.Add(new ChatMessageRecord
        {
            MessageId = Guid.NewGuid(),
            ConversationId = conversation.ConversationId,
            Role = "assistant",
            Content = reply,
            ToolsUsedJson = JsonSerializer.Serialize(toolsUsed),
            TimestampUtc = DateTimeOffset.UtcNow
        });
        conversation.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return new ChatMessageResponse
        {
            ConversationId = conversation.ConversationId,
            Reply = reply,
            Explanation = reply,
            Grid = grid,
            ToolsUsed = toolsUsed,
            Citations = citations,
            DataAsOfUtc = dataAsOf,
            GuardrailNotice = Guardrail,
            InsightChatVersion = InsightChatInfo.Version
        };
    }

    public async Task<List<ConversationDto>> ListConversationsAsync(string tenantId, Guid siteId, CancellationToken ct)
    {
        var rows = await db.ChatConversations.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.SiteId == siteId)
            .ToListAsync(ct);
        return rows
            .OrderByDescending(c => c.UpdatedAtUtc)
            .Take(50)
            .Select(c => new ConversationDto
            {
                ConversationId = c.ConversationId,
                Title = c.Title,
                UpdatedAtUtc = c.UpdatedAtUtc
            })
            .ToList();
    }

    private static string FormatRecognizedIntentFallback(
        string siteName,
        string message,
        SageIntentEngine.Classification classification)
    {
        if (MegaDigestFallbackMatcher.TryBuildReply(message, classification, out var reply, out _))
            return reply;

        return SageChatDomain.BuildUnmatchedReply(siteName, message);
    }

    private static (string? operation, Dictionary<string, string> parameters, List<string> tools) PlanToolCall(
        string message,
        SageIntentEngine.Classification classification)
    {
        var m = message.ToLowerInvariant();
        var tools = new List<string> { $"intent:{classification.PrimaryIntent}" };
        var parameters = new Dictionary<string, string> { ["top"] = "500" };
        RankingQueryPolicy.ApplyRowLimits(message, classification, parameters);

        if ((m.Contains("dashboard") || m.Contains("kpi")) && !m.Contains("treasury") &&
            !m.Contains("vat") && !m.Contains("expense trend"))
        {
            tools.Add("dashboard.summary");
            return ("dashboard.summary", parameters, tools);
        }

        if (m.StartsWith("search ") || m.Contains("find "))
        {
            var query = Regex.Replace(message, "(?i)^(search|find)\\s+", "").Trim();
            parameters["query"] = query;
            tools.Add("search.global");
            return ("search.global", parameters, tools);
        }

        if (ChatIntentMatcher.TryInventoryBsNegativeLedgers(m, parameters, tools, out var negStockOp))
            return (negStockOp, parameters, tools);

        if (ReconciliationChatMatcher.TryRoute(message, m, parameters, tools, out var reconOp))
            return (reconOp, parameters, tools);

        if (ArPaymentBehaviorChatMatcher.TryRoute(message, m, parameters, tools, out var payBehOp))
            return (payBehOp, parameters, tools);

        if (ArSalesChatMatcher.TryRoute(message, m, parameters, tools, out var arSalesOp))
            return (arSalesOp, parameters, tools);

        if (ApPurchaseInvChatMatcher.TryRoute(message, m, parameters, tools, out var apOp))
            return (apOp, parameters, tools);

        if (InvWarehouseChatMatcher.TryRoute(message, m, parameters, tools, out var invOp))
            return (invOp, parameters, tools);

        if (GlFinanceChatMatcher.TryRoute(message, m, parameters, tools, out var finOp))
            return (finOp, parameters, tools);

        if (ChatIntentMatcher.TryCustomerAgedTop(m, parameters, tools, out var agedTopOp))
            return (agedTopOp, parameters, tools);

        if (ChatIntentMatcher.TryCustomerUnpaidSummary(m, parameters, tools, out var summaryOp))
            return (summaryOp, parameters, tools);

        if (SageChatDomain.TryGlTransactionList(m, parameters, tools, out var glOp) &&
            !m.Contains("expense") && !m.Contains("vat") && !m.Contains("treasury"))
            return (glOp, parameters, tools);

        if (ChatIntentMatcher.TryUnpaidSalesInvoices(m, parameters, tools, out var unpaidOp))
            return (unpaidOp, parameters, tools);

        if (MegaDigestRouter.TryPlan(message, m, parameters, tools, out var digestOp))
            return (digestOp, parameters, tools);

        if (m.Contains("open item") && m.Contains("supplier"))
        {
            ExtractAccount(m, parameters);
            tools.Add("supplier.openitems");
            return ("supplier.openitems", parameters, tools);
        }

        if (m.Contains("open item") || m.Contains("outstanding"))
        {
            ExtractAccount(m, parameters);
            tools.Add("customer.openitems");
            return ("customer.openitems", parameters, tools);
        }

        if (m.Contains("supplier") && m.Contains("list") &&
            !RankingQueryPolicy.IsRankingClassification(classification))
        {
            tools.Add("supplier.list");
            return ("supplier.list", parameters, tools);
        }

        if (TryExtractThreshold(message, "valuation", out var minVal) &&
            (m.Contains("inventory") || m.Contains("stock") || m.Contains("item")))
        {
            parameters["minValuation"] = minVal.ToString(System.Globalization.CultureInfo.InvariantCulture);
            tools.Add("inventoryitem.list");
            return ("inventoryitem.list", parameters, tools);
        }

        if (TryExtractThreshold(message, "balance", out var minSupplierBal) && m.Contains("supplier"))
        {
            parameters["minBalance"] = minSupplierBal.ToString(System.Globalization.CultureInfo.InvariantCulture);
            tools.Add("supplier.list");
            return ("supplier.list", parameters, tools);
        }

        if (TryExtractThreshold(message, "balance", out var minBalance) && m.Contains("customer"))
        {
            parameters["minBalance"] = minBalance.ToString(System.Globalization.CultureInfo.InvariantCulture);
            tools.Add("customer.list");
            return ("customer.list", parameters, tools);
        }

        if (m.Contains("supplier") && m.Contains("list"))
        {
            tools.Add("supplier.list");
            return ("supplier.list", parameters, tools);
        }

        if (m.Contains("customer") && m.Contains("list") && !ChatIntentMatcher.IsCustomerAgedTopQuery(m) &&
            !RankingQueryPolicy.IsRankingClassification(classification))
        {
            tools.Add("customer.list");
            return ("customer.list", parameters, tools);
        }

        if (m.Contains("customer") && !ChatIntentMatcher.IsCustomerAgedTopQuery(m) &&
            !RankingQueryPolicy.IsRankingClassification(classification))
        {
            tools.Add("customer.list");
            return ("customer.list", parameters, tools);
        }

        if (m.Contains("supplier") && !RankingQueryPolicy.IsRankingClassification(classification))
        {
            tools.Add("supplier.list");
            return ("supplier.list", parameters, tools);
        }

        return (null, parameters, tools);
    }

    private static bool TryExtractThreshold(string message, string kind, out decimal amount)
    {
        amount = 0;
        var keywordPattern = kind switch
        {
            "valuation" => @"(?:valuation|stock\s*value|inventory\s*value|item\s*value)",
            "balance" => @"(?:balance|owing|outstanding)",
            _ => kind
        };

        var match = Regex.Match(message,
            keywordPattern + @"\s*(?:>|greater\s+than|over|above)\s*([\d][\d,\.]*)\s*(k|m|million|thousand)?",
            RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            match = Regex.Match(message, @">\s*([\d][\d,\.]*)\s*(k|m|million|thousand)?", RegexOptions.IgnoreCase);
            if (!match.Success) return false;
            if (kind == "balance" && !message.Contains("balance", StringComparison.OrdinalIgnoreCase))
                return false;
            if (kind == "valuation" &&
                !message.Contains("valuation", StringComparison.OrdinalIgnoreCase) &&
                !message.Contains("stock", StringComparison.OrdinalIgnoreCase) &&
                !message.Contains("inventory", StringComparison.OrdinalIgnoreCase))
                return false;
        }

        var raw = match.Groups[1].Value.Replace(",", "");
        if (!decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed) &&
            !decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.CurrentCulture, out parsed))
            return false;

        var suffix = match.Groups.Count > 2 && match.Groups[2].Success
            ? match.Groups[2].Value.ToLowerInvariant()
            : "";
        amount = suffix switch
        {
            "k" or "thousand" => parsed * 1000,
            "m" or "million" => parsed * 1_000_000,
            _ => parsed
        };
        return true;
    }

    private static string FormatQueryDescription(string operation, Dictionary<string, string> parameters, string? userMessage = null)
    {
        if (operation == "supplier.list")
        {
            var parts = new List<string> { "Sage: Supplier.List", "criteria: DCLink > 0" };
            if (parameters.TryGetValue("minBalance", out var mb))
                parts.Add($"filter: balance >= {mb}");
            if (parameters.TryGetValue("top", out var top))
                parts.Add($"max rows: {top}");
            return "Query run: " + string.Join(", ", parts) + ".";
        }

        if (operation == "inventoryitem.list")
        {
            var parts = new List<string> { "Sage: InventoryItem.List", "criteria: 1=1" };
            if (parameters.TryGetValue("minValuation", out var mv))
                parts.Add($"filter: valuation >= {mv}");
            if (parameters.TryGetValue("top", out var top))
                parts.Add($"max rows: {top}");
            return "Query run: " + string.Join(", ", parts) + ".";
        }

        if (operation == "customer.list")
        {
            var parts = new List<string> { "Sage: Customer.List", "criteria: DCLink > 0" };
            if (parameters.TryGetValue("minBalance", out var mb))
                parts.Add($"filter: balance >= {mb}");
            if (parameters.TryGetValue("top", out var top))
                parts.Add($"max rows: {top}");
            return "Query run: " + string.Join(", ", parts) + ".";
        }

        if (operation == "inventory.bs.negative_ledgers")
            return "Query run: SAGE-BS-STOCK-NEGATIVE-001 — inventory stock GL accounts (GrpTbl.StockAccLink) where PostGL net balance is credit/negative.";

        if (operation == "inventory.gl.reconcile")
        {
            if (InventoryFixWorkflow.WantsFixWorkflow(userMessage))
                return $"Query run: SAGE-INVVAL-RECON-CANONICAL-001 — {InventoryFixWorkflow.IntentName} (read-only diagnostic + rollback datafix preview). Fresh SQL execution.";
            return "Query run: SAGE-INVVAL-RECON-CANONICAL-001 — distinct PostGL inventory GL accounts vs Sage valuation SQL (not SDK sum).";
        }
        if (operation == "gltransaction.list")
            return "Query run: GLTransaction.List — sample general ledger postings (PostGL layer). Not inventory valuation.";
        if (operation == "salesinvoice.discount.count")
            return QueryWithDigest("SAGE-SALES-INV-DISC-COUNT-001 — count distinct InvNum sales invoices with discounts (not open AR lines).", parameters);
        if (operation == "salesinvoice.discount.top")
            return QueryWithDigest("SAGE-SALES-INV-DISC-TOP-001 — top invoices by discount value (InvNum).", parameters);
        if (operation == "ar.invoice.overdue.buckets")
            return "Query run: SAGE-AR-OVERDUE-BUCKETS-001 — count overdue invoice lines by aging bucket.";
        if (operation == "customer.over.creditlimit")
            return "Query run: SAGE-AR-CREDIT-LIMIT-001 — customers exceeding credit limit.";
        if (operation == "salesinvoice.partially.paid")
            return "Query run: SAGE-SALES-INV-PARTIAL-001 — partially paid open invoice lines.";
        if (operation == "customer.invoice.unpaid.olderthan")
            return QueryWithDigest("SAGE-AR-UNPAID-OLDER-001 — unpaid invoices older than N days (limited TOP).", parameters);
        if (operation == "customer.outstanding.debit.top")
            return "Query run: SAGE-AR-OUTSTANDING-DEBIT-TOP-001 — top customers by total outstanding debit.";
        if (operation == "customer.aged.credit.top")
            return "Query run: SAGE-AR-AGED-CREDIT-TOP-001 — top customers by oldest aged credit balance lines.";
        if (operation == "customer.sales.top")
            return QueryWithDigest("SAGE-AR-SALES-TOP-001 — top customers by sales value for period.", parameters);

        if (operation == "customer.aged.top")
            return QueryWithDigest("SAGE-AR-AGED-TOP-001 — top customers by oldest open AR debit (not Customer.List).", parameters);

        if (operation == "customer.credit.balances")
            return QueryWithDigest("SAGE-AR-CREDIT-BAL-001 — customers with credit (negative) AR balances.", parameters);

        if (operation == "supplier.aged.top")
            return QueryWithDigest("SAGE-AP-AGED-TOP-001 — top suppliers by oldest unpaid AP balance.", parameters);

        if (operation == "customer.unpaid.summary")
            return "Query run: CustomerTransaction.List (Outstanding <> 0), grouped by customer — excludes payment lines, sums open invoice/order amounts.";
        if (operation == "customer.payment.prompt.top")
            return "Query run: SAGE-AR-PAYMENT-BEHAVIOR-001 — top prompt-paying customers by payment discipline score (InvNum due dates vs PostAR payment dates). Not unpaid balance ranking.";
        if (operation == "customer.payment.late.top")
            return "Query run: SAGE-AR-PAYMENT-BEHAVIOR-001 — slow/late payers by payment discipline score.";
        if (operation == "customer.payment.behavior.summary")
            return "Query run: SAGE-AR-PAYMENT-BEHAVIOR-001 — customer payment behaviour summary.";
        if (operation == "customer.payment.detail")
            return "Query run: SAGE-AR-PAYMENT-BEHAVIOR-001 — single-customer payment discipline detail.";
        if (operation == "customer.openitems")
            return "Query run: CustomerTransaction.List (Outstanding <> 0) — open/unpaid AR including sales invoices.";
        if (operation == "dashboard.summary")
            return "Query run: dashboard KPI counts from Sage lists.";
        if (operation == "search.global" && parameters.TryGetValue("query", out var q))
            return $"Query run: search.global for \"{q}\".";
        return "Query run: " + operation + ".";
    }

    private static string QueryWithDigest(string queryRun, Dictionary<string, string> parameters)
    {
        if (parameters.TryGetValue("digestId", out var id) && parameters.TryGetValue("digestTitle", out var title))
            return $"Query run: {queryRun} [Mega digest #{id}: {title}]";
        return $"Query run: {queryRun}";
    }

    private static void ExtractAccount(string message, Dictionary<string, string> parameters)
    {
        var match = Regex.Match(message, @"\b([A-Z0-9]{2,12})\b");
        if (match.Success)
            parameters["account"] = match.Groups[1].Value;
    }

    private static (string reply, List<string> citations) FormatJobResult(string operation, string? resultJson, string? userMessage = null)
    {
        var citations = new List<string>();
        if (string.IsNullOrWhiteSpace(resultJson))
            return ("No data returned.", citations);

        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;

            if (operation == "inventory.bs.negative_ledgers")
            {
                citations.Add(InventoryBsNegativeLedgersFormat.QuerySerial);
                return (InventoryBsNegativeLedgersFormat.BuildReply(root), citations);
            }

            if (operation == "inventory.gl.reconcile")
            {
                citations.Add(InventoryGlReconcileFormat.QuerySerial);
                return (InventoryGlReconcileFormat.BuildReply(root, userMessage), citations);
            }

            if (ReconciliationReplyFormat.TryFormat(operation, root, out var reconReply))
            {
                if (root.TryGetProperty("querySerial", out var qs))
                    citations.Add(qs.GetString() ?? operation);
                return (reconReply + Environment.NewLine + Environment.NewLine + Guardrail, citations);
            }

            if (ArPaymentBehaviorReplyFormat.TryFormat(operation, root, out var payReply))
            {
                citations.Add("SAGE-AR-PAYMENT-BEHAVIOR-001");
                return (payReply + Environment.NewLine + Environment.NewLine + Guardrail, citations);
            }

            if (operation == "salesinvoice.discount.count")
            {
                citations.Add("SAGE-SALES-INV-DISC-COUNT-001");
                var body = AggregationReplyFormat.FormatSalesInvoiceDiscountCount(root);
                return (ArSalesReplyFormat.Wrap("SAGE-SALES-INV-DISC-COUNT-001", body), citations);
            }

            if (operation == "ar.invoice.overdue.buckets")
            {
                citations.Add("SAGE-AR-OVERDUE-BUCKETS-001");
                var body = ArSalesReplyFormat.FormatOverdueBuckets(root);
                return (ArSalesReplyFormat.Wrap("SAGE-AR-OVERDUE-BUCKETS-001", body), citations);
            }

            if (operation == "salesinvoice.discount.top" && root.TryGetProperty("topInvoices", out _))
            {
                citations.Add("SAGE-SALES-INV-DISC-TOP-001");
                var body = ArSalesReplyFormat.FormatInvoiceList(root, "topInvoices");
                return (ArSalesReplyFormat.Wrap("SAGE-SALES-INV-DISC-TOP-001", body), citations);
            }

            if (operation is "customer.outstanding.debit.top" or "customer.aged.credit.top" or "customer.sales.top")
            {
                citations.Add(root.TryGetProperty("querySerial", out var qs) ? qs.GetString() ?? operation : operation);
                var body = ArSalesReplyFormat.FormatTopCustomers(root, "topCustomers",
                    operation == "customer.sales.top" ? "salesValue" : "totalOutstanding");
                return (ArSalesReplyFormat.Wrap(citations[0], body), citations);
            }

            if (operation == "customer.over.creditlimit" && root.TryGetProperty("customers", out var overLimit) &&
                overLimit.ValueKind == JsonValueKind.Array)
            {
                citations.Add("SAGE-AR-CREDIT-LIMIT-001");
                var table = string.Join("\n", overLimit.EnumerateArray().Select(r =>
                {
                    var rank = r.TryGetProperty("rank", out var rk) ? rk.GetInt32() : 0;
                    var code = r.TryGetProperty("code", out var c) ? c.GetString() : "";
                    var name = r.TryGetProperty("name", out var n) ? n.GetString() : "";
                    var over = r.TryGetProperty("overBy", out var o) && o.ValueKind == JsonValueKind.Number
                        ? o.GetDecimal().ToString("N2") : "?";
                    return $"  {rank}. {name} ({code}) — over limit by {over}";
                }));
                return (ArSalesReplyFormat.Wrap("SAGE-AR-CREDIT-LIMIT-001", table), citations);
            }

            if (operation is "salesinvoice.partially.paid" or "customer.invoice.unpaid.olderthan")
            {
                var serial = root.TryGetProperty("querySerial", out var qs) ? qs.GetString() ?? operation : operation;
                citations.Add(serial ?? operation);
                var body = FormatArInvoiceRows(root);
                return (ArSalesReplyFormat.Wrap(serial ?? operation, body), citations);
            }

            if (operation == "customer.credit.balances" && root.TryGetProperty("customers", out var creditCustomers) &&
                creditCustomers.ValueKind == JsonValueKind.Array)
            {
                citations.Add("SAGE-AR-CREDIT-BAL-001");
                var rows = creditCustomers.EnumerateArray().ToList();
                var has = root.TryGetProperty("hasCreditBalances", out var h) && h.GetBoolean();
                if (!has || rows.Count == 0)
                    return ("No customers with credit balances on their AR account.", citations);

                var table = string.Join("\n", rows.Select(r =>
                {
                    var rank = r.TryGetProperty("rank", out var rk) ? rk.GetInt32() : 0;
                    var code = r.TryGetProperty("code", out var c) ? c.GetString() : "";
                    var name = r.TryGetProperty("name", out var n) ? n.GetString() : "";
                    var bal = r.TryGetProperty("balance", out var b) && b.ValueKind == JsonValueKind.Number
                        ? b.GetDecimal().ToString("N2") : "?";
                    return $"  {rank}. {name} ({code}) — balance {bal}";
                }));
                return ($"Customers with credit balances:\n\n{table}", citations);
            }

            if (operation == "supplier.aged.top" && root.TryGetProperty("topSuppliers", out var agedSuppliers) &&
                agedSuppliers.ValueKind == JsonValueKind.Array)
            {
                citations.Add("SAGE-AP-AGED-TOP-001");
                var supRows = agedSuppliers.EnumerateArray().ToList();
                if (supRows.Count == 0)
                    return ("No suppliers with aged open debit balances.", citations);

                var requestedTop = root.TryGetProperty("requestedTop", out var srt) ? srt.GetInt32() : 5;
                var table = string.Join("\n", supRows.Select(r =>
                {
                    var rank = r.TryGetProperty("rank", out var rk) ? rk.GetInt32() : 0;
                    var code = r.TryGetProperty("code", out var c) ? c.GetString() : "";
                    var name = r.TryGetProperty("name", out var n) ? n.GetString() : "";
                    var total = r.TryGetProperty("totalOutstanding", out var t) && t.ValueKind == JsonValueKind.Number
                        ? t.GetDecimal().ToString("N2") : "?";
                    var days = r.TryGetProperty("daysOutstanding", out var d) ? d.GetInt32() : 0;
                    return $"  {rank}. {name} ({code}) — {total} — oldest {days} days";
                }));
                return ($"Top {supRows.Count} supplier(s) with oldest aged balances (requested {requestedTop}):\n\n{table}", citations);
            }

            if (operation == "customer.aged.top" && root.TryGetProperty("topCustomers", out var agedCustomers) &&
                agedCustomers.ValueKind == JsonValueKind.Array)
            {
                var requestedTop = root.TryGetProperty("requestedTop", out var rt) ? rt.GetInt32() : 5;
                citations.Add("SAGE-AR-AGED-TOP-001");

                var agedRows = agedCustomers.EnumerateArray().ToList();
                if (agedRows.Count == 0)
                    return ("No customers with aged open debit balances (after excluding payments, zero balances, and CASH).", citations);

                var header = $"Top {agedRows.Count} customer(s) with oldest aged debit balances (requested {requestedTop}):\n\n";
                var table = string.Join("\n", agedRows.Select(r =>
                {
                    var rank = r.TryGetProperty("rank", out var rk) ? rk.GetInt32() : 0;
                    var code = r.TryGetProperty("code", out var c) ? c.GetString() : "";
                    var name = r.TryGetProperty("name", out var n) ? n.GetString() : "";
                    var total = r.TryGetProperty("totalOutstanding", out var t) && t.ValueKind == JsonValueKind.Number
                        ? t.GetDecimal().ToString("N2") : "?";
                    var days = r.TryGetProperty("daysOutstanding", out var d) ? d.GetInt32() : 0;
                    var oldest = r.TryGetProperty("oldestInvoiceDate", out var o) ? o.GetString() : "";
                    return $"  {rank}. {name} ({code}) — Balance: {total} — Oldest invoice: {days} days ({oldest})";
                }));

                return (header + table, citations);
            }

            if (operation == "customer.unpaid.summary" && root.TryGetProperty("topCustomers", out var topCustomers) &&
                topCustomers.ValueKind == JsonValueKind.Array)
            {
                var totalLines = root.TryGetProperty("totalOpenLines", out var tl) ? tl.GetInt32() : 0;
                var customerCount = root.TryGetProperty("customersWithUnpaidInvoices", out var cc) ? cc.GetInt32() : 0;
                var unalloc = root.TryGetProperty("unallocatedLines", out var ua) ? ua.GetInt32() : 0;
                var skipped = root.TryGetProperty("skippedNonInvoiceLines", out var sk) ? sk.GetInt32() : 0;
                citations.Add($"customer.unpaid.summary: {customerCount} customer(s)");

                var rows = topCustomers.EnumerateArray().ToList();
                if (rows.Count == 0)
                    return ("No customers with open unpaid invoice/order lines (after excluding payments).", citations);

                var leader = rows[0];
                var leaderName = leader.TryGetProperty("name", out var ln) ? ln.GetString() : "";
                var leaderCode = leader.TryGetProperty("code", out var lc) ? lc.GetString() : "";
                var leaderCount = leader.TryGetProperty("invoiceCount", out var lcnt) ? lcnt.GetInt32() : 0;
                var leaderTotal = leader.TryGetProperty("totalOutstanding", out var lt) && lt.ValueKind == JsonValueKind.Number
                    ? lt.GetDecimal().ToString("N2")
                    : "?";

                var table = string.Join("\n", rows.Select((r, i) =>
                {
                    var code = r.TryGetProperty("code", out var c) ? c.GetString() : "";
                    var name = r.TryGetProperty("name", out var n) ? n.GetString() : "";
                    var count = r.TryGetProperty("invoiceCount", out var cnt) ? cnt.GetInt32() : 0;
                    var total = r.TryGetProperty("totalOutstanding", out var t) && t.ValueKind == JsonValueKind.Number
                        ? t.GetDecimal().ToString("N2")
                        : "?";
                    return $"  {i + 1}. {name} ({code}) — {count} open invoice/order line(s), total outstanding {total}";
                }));

                var unallocNote = unalloc > 0
                    ? $"\n({unalloc} invoice line(s) had no customer code — check Sage AR allocation.)"
                    : "";
                var skipNote = skipped > 0
                    ? $"\n({skipped} other open AR lines such as payments were excluded from this ranking.)"
                    : "";

                return ($"Highest unpaid: {leaderName} ({leaderCode}) — {leaderCount} open line(s), total outstanding {leaderTotal}.\n\n" +
                        $"Top {rows.Count} customer(s) by unpaid invoice total (from {totalLines} open AR lines):{unallocNote}{skipNote}\n{table}",
                    citations);
            }

            if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                var total = root.TryGetProperty("total", out var totalEl) ? totalEl.GetInt32() : items.GetArrayLength();
                var shown = items.GetArrayLength();
                citations.Add($"{operation}: {total} row(s)");

                var msgLower = userMessage?.ToLowerInvariant() ?? "";
                if (operation is "customer.openitems" &&
                    ChatIntentMatcher.IsUnpaidSalesInvoiceQuery(msgLower))
                {
                    var pageSuffix = shown < total ? $" (showing {shown} of {total})" : "";
                    if (!ChatIntentMatcher.WantsRowPreview(userMessage))
                    {
                        return ($"Unpaid/open AR transactions (sales invoice lines): {total} record(s){pageSuffix}. " +
                                "Counts outstanding customer transactions (Outstanding <> 0).",
                            citations);
                    }

                    var openLines = items.EnumerateArray().Take(15)
                        .Select(FormatOpenItemLine).Where(x => x is not null).ToList();
                    var openPreview = openLines.Count > 0
                        ? string.Join("\n", openLines)
                        : "(no rows in this page)";
                    var scopeNote = total > 500
                        ? "\n\nNote: This is all open AR lines (Outstanding <> 0), not a separate Invoices screen. Large counts are normal on busy ledgers."
                        : "";
                    return ($"Unpaid/open AR — {total} record(s){pageSuffix}:\n{openPreview}{scopeNote}",
                        citations);
                }

                var note = root.TryGetProperty("note", out var noteEl) ? noteEl.GetString() : null;

                var lines = items.EnumerateArray().Take(10).Select(FormatItemLine).Where(x => x is not null).ToList();
                var preview = lines.Count > 0
                    ? string.Join("; ", lines)
                    : string.Join(", ", items.EnumerateArray().Take(5)
                        .Select(e => e.TryGetProperty("code", out var c) ? c.GetString()
                            : e.TryGetProperty("account", out var a) ? a.GetString() : "?")
                        .Where(x => x is not null));

                var paging = shown < total ? $" (showing {shown} of {total})" : "";
                var filterNote = string.IsNullOrWhiteSpace(note) ? "" : $" {note}";
                if (root.TryGetProperty("minBalance", out var mb) && mb.ValueKind == JsonValueKind.Number)
                    filterNote = $" (filter minBalance >= {mb.GetDecimal()})" + filterNote;
                if (root.TryGetProperty("minValuation", out var mv) && mv.ValueKind == JsonValueKind.Number)
                    filterNote = $" (filter minValuation >= {mv.GetDecimal()})" + filterNote;
                return ($"Found {total} record(s){paging}.{filterNote}\n{preview}".Trim(), citations);
            }

            if (root.TryGetProperty("hits", out var hits) && hits.ValueKind == JsonValueKind.Array)
            {
                var count = hits.GetArrayLength();
                citations.Add($"search: {count} hit(s)");
                return ($"Search returned {count} match(es).", citations);
            }

            if (root.TryGetProperty("kpis", out _))
            {
                citations.Add("dashboard.summary");
                return (resultJson, citations);
            }

            return (Truncate(resultJson, 800), citations);
        }
        catch
        {
            return (Truncate(resultJson, 800), citations);
        }
    }

    private static string? FormatItemLine(JsonElement e)
    {
        if (e.TryGetProperty("account", out _) || e.TryGetProperty("reference", out _))
            return FormatOpenItemLine(e);

        if (!e.TryGetProperty("code", out var codeEl)) return null;
        var code = codeEl.GetString();
        var name = e.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : "";
        if (e.TryGetProperty("balance", out var balEl) && balEl.ValueKind == JsonValueKind.Number)
            return $"{code} ({name}) balance {balEl.GetDecimal():N2}";
        if (e.TryGetProperty("valuation", out var valEl) && valEl.ValueKind == JsonValueKind.Number)
            return $"{code} ({name}) valuation {valEl.GetDecimal():N2}";
        return $"{code} ({name})";
    }

    private static string? FormatOpenItemLine(JsonElement e)
    {
        if (!e.TryGetProperty("reference", out _) && !e.TryGetProperty("account", out _))
            return null;

        var account = e.TryGetProperty("account", out var accountEl) ? accountEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(account))
            account = "—";
        var reference = e.TryGetProperty("reference", out var refEl) ? refEl.GetString() : "";
        var description = e.TryGetProperty("description", out var descEl) ? descEl.GetString() : "";
        var txType = e.TryGetProperty("txType", out var typeEl) ? typeEl.GetString() : "";
        var outstanding = FormatJsonAmount(e, "outstanding");
        var date = e.TryGetProperty("txDate", out var dEl) ? FormatShortDate(dEl) : "";
        var descBit = string.IsNullOrWhiteSpace(description) ? "" : $" — {description}";
        var typeBit = string.IsNullOrWhiteSpace(txType) ? "" : $" [{txType}]";
        return $"  {account} | ref {reference}{typeBit}{descBit} | outstanding {outstanding} | {date}";
    }

    private static string FormatJsonAmount(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out var el))
            return "?";
        if (el.ValueKind == JsonValueKind.Number)
            return el.GetDecimal().ToString("N2");
        if (el.ValueKind == JsonValueKind.String &&
            decimal.TryParse(el.GetString(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var inv))
            return inv.ToString("N2");
        if (el.ValueKind == JsonValueKind.String &&
            decimal.TryParse(el.GetString(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.CurrentCulture, out var loc))
            return loc.ToString("N2");
        return el.ToString() ?? "?";
    }

    private static string FormatShortDate(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.String &&
            DateTime.TryParse(el.GetString(), out var dt))
            return dt.ToString("dd MMM yyyy");
        return el.ToString() ?? "";
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    private static string FormatArInvoiceRows(JsonElement root)
    {
        if (!root.TryGetProperty("invoices", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return "No matching invoices.";

        var total = root.TryGetProperty("totalMatching", out var tm) ? tm.GetInt32() : arr.GetArrayLength();
        var header = total > arr.GetArrayLength()
            ? $"Showing {arr.GetArrayLength()} of {total} matching line(s):\n"
            : "";

        return header + ArSalesReplyFormat.FormatInvoiceList(root);
    }
}
