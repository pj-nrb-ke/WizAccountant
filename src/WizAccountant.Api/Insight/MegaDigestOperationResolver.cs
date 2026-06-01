namespace WizAccountant.Api.Insight;

/// <summary>Maps digest entries to allowlisted Sage operations (mega digest incorporation).</summary>
internal static class MegaDigestOperationResolver
{
    public sealed record ResolveResult(string? Operation, bool Implemented, string? DigestNote);

    public static ResolveResult Resolve(MegaDigestEntry entry, string messageLower)
    {
        var title = entry.Title.ToLowerInvariant();
        var domain = entry.Domain.ToLowerInvariant();
        var m = messageLower;

        // Explicit high-confidence ids from digest
        switch (entry.Id)
        {
            case 1:
                return Op("customer.aged.top", true, "Digest #1 — oldest aged debit balances");
            case 2:
                return Op("customer.credit.balances", true, "Digest #2 — customers with credit balances");
            case 3:
                return Op("customer.unpaid.summary", true, "Digest #3 — highest outstanding balance");
            case 51:
                return Op("inventory.gl.reconcile", true, "Digest #51 — inventory valuation vs balance sheet");
            case 52:
                return Op("inventory.bs.negative_ledgers", true, "Digest #52 — inventory GL credit balances");
        }

        if (title.Contains("inventory valuation") && title.Contains("balance sheet"))
            return Op("inventory.gl.reconcile", true, $"Digest #{entry.Id}");

        if (title.Contains("inventory gl") && title.Contains("negative"))
            return Op("inventory.bs.negative_ledgers", true, $"Digest #{entry.Id}");

        if (title.Contains("negative stock") && title.Contains("balance sheet"))
            return Op("inventory.bs.negative_ledgers", true, $"Digest #{entry.Id}");

        if (domain.Contains("receivable") || domain == "accounts receivable (ar)" || m.Contains("customer"))
        {
            if (title.Contains("credit balance"))
                return Op("customer.credit.balances", true, $"Digest #{entry.Id}");

            if (title.Contains("oldest") || title.Contains("aged") || (title.Contains("top") && title.Contains("debit")))
                return Op("customer.aged.top", true, $"Digest #{entry.Id}");

            if (title.Contains("highest") && (title.Contains("outstanding") || title.Contains("balance")))
                return Op("customer.unpaid.summary", true, $"Digest #{entry.Id}");

            if (title.Contains("unpaid") || title.Contains("overdue") || title.Contains("outstanding") ||
                title.Contains("invoice") || title.Contains("partially paid"))
                return Op("customer.openitems", false, $"Digest #{entry.Id} — use unpaid/open invoice intent or implement dedicated SQL");

            return Op("customer.openitems", false, $"Digest #{entry.Id} — AR analytical query; dedicated SQL not implemented");
        }

        if (domain.Contains("payable") || domain.Contains("supplier") || m.Contains("supplier"))
        {
            if (title.Contains("oldest") || title.Contains("aged") || title.Contains("unpaid"))
                return Op("supplier.aged.top", true, $"Digest #{entry.Id}");

            if (title.Contains("highest") && title.Contains("outstanding"))
                return Op("supplier.openitems", false, $"Digest #{entry.Id} — implement AP outstanding ranking SQL");

            if (title.Contains("credit balance"))
                return Op("supplier.list", false, $"Digest #{entry.Id} — supplier credit balances not dedicated yet");

            return Op("supplier.openitems", false, $"Digest #{entry.Id} — AP analytical query; dedicated SQL not implemented");
        }

        if (domain.Contains("inventory") || domain.Contains("stock"))
        {
            if (title.Contains("valuation") && title.Contains("top"))
                return Op("inventoryitem.list", false, $"Digest #{entry.Id} — implement ranked inventory valuation SQL");

            if (title.Contains("valuation") || title.Contains("stock value"))
                return Op("inventoryitem.list", false, $"Digest #{entry.Id} — dedicated valuation SQL not implemented");

            if (title.Contains("negative") && title.Contains("quantit"))
                return Op("inventoryitem.list", false, $"Digest #{entry.Id} — physical qty; not Balance Sheet GL");

            return Op("inventoryitem.list", false, $"Digest #{entry.Id} — inventory analytical query");
        }

        if (domain.Contains("general ledger") || domain.Contains("gl"))
            return Op("gltransaction.list", false, $"Digest #{entry.Id} — GL sample postings (limited)");

        if (domain.Contains("sales"))
        {
            if (title.Contains("discount") && title.Contains("invoice") &&
                (title.Contains("how many") || title.Contains("count") || m.Contains("how many")))
                return Op("salesinvoice.discount.count", true, $"Digest #{entry.Id} — invoice discount count");

            return Op("customer.openitems", false, $"Digest #{entry.Id} — sales/invoice proxy via AR open items");
        }

        if (domain.Contains("fixed asset") || domain.Contains("depreciation"))
            return Op(null, false, $"Digest #{entry.Id} — fixed assets not in Insight chat yet");

        if (domain.Contains("manufactur") || domain.Contains("bom"))
            return Op(null, false, $"Digest #{entry.Id} — manufacturing/BOM not in Insight chat yet");

        if (domain.Contains("audit") || domain.Contains("fraud"))
            return Op("gltransaction.list", false, $"Digest #{entry.Id} — audit patterns need dedicated SQL");

        return Op(null, false, $"Digest #{entry.Id} — matched catalog; no allowlisted operation yet");
    }

    private static ResolveResult Op(string operation, bool implemented, string note) =>
        new(operation, implemented, note);
}
