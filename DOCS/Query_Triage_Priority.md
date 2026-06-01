# Query Triage Priority Matrix

Classifies failed or weak Insight responses for weekly review. Used with `GET /api/insight/triage` and `scripts/export-insight-triage.ps1`.

## Priority levels

| Priority | Meaning | Response SLA | Typical action |
|----------|---------|--------------|----------------|
| **Critical** | Wrong financial meaning — could mislead decisions | Same day | Confusion guard + block route; hotfix if live |
| **High** | Wrong route — correct domain but wrong handler | 1–3 days | Add must-not-route test; fix ChatRoutePlanner |
| **Medium** | Incomplete analysis — right route, missing breakdown/metrics | 1 week | Extend handler or OutputContractValidator |
| **Low** | Cosmetic / formatting — numbers OK, presentation weak | Backlog | Reply formatter only |
| **Enhancement** | Future capability — valid ask, not yet in scope | Roadmap | Add to Capability Gap Register |

---

## Triage buckets

### 1. Semantic failures

**Definition:** Wrong business meaning.

| Example query | Wrong route | Correct meaning | Priority |
|---------------|-------------|-----------------|----------|
| prompt payer | `customer.outstanding.debit.top` | payment discipline | Critical |
| highest unpaid balances | `customer.payment.prompt.top` | unpaid exposure | Critical |
| why is cash low | `bank.cashbook` | treasury explainability | High |
| VAT increase why | `vat.summary` | contributor analysis | High |
| negative stock on BS | `inventory.negative.qty` | GL credit balance | High |

**Detection signals:** `CompatibilityBlocked`, user feedback `wrong` + note "wrong route", confusion guard test failure.

**Action:** Strengthen canonical route; add `mustNotRoute` in intent JSON; never add duplicate handler names.

---

### 2. Capability failures

**Definition:** Correct meaning identified but no handler or weak mega-digest fallback.

| Example query | Gap | Priority |
|---------------|-----|----------|
| per product per month qty/value | `product.monthly.orders.analysis` (was missing) | Critical → **closed** |
| is month-end ready to close | close intelligence handler | High |
| supplier payment discipline | AP payment behaviour (partial) | Medium |
| CFO dashboard composite | multi-domain analytics | Enhancement |

**Detection signals:** `RouteStatus = mega_digest`, `operation = null`, triage unmatched bucket.

**Action:** Real Query Bank entry → handler spec → implement → promote tests.

---

### 3. Runtime failures

**Definition:** Technical crash or Sage job failure.

| Example | Cause | Priority |
|---------|-------|----------|
| Chat history load crash | SQLite DateTimeOffset ORDER BY | Critical → **fixed** |
| Handler SQL error | Wrong column / table for company | High |
| Timeout on large scan | Missing date filter | Medium |

**Detection signals:** `JobStatus = Failed`, `ErrorSummary` in logs, user feedback reason `crashed`.

**Action:** SafeExecutionBoundary for users; fix SQL; schema proof in DOCS; regression test.

---

### 4. Output shape failures

**Definition:** Correct route and execution but JSON/reply does not match contract.

| Example | Required shape | Priority |
|---------|----------------|----------|
| monthly product analysis | product + month + qty + value | Critical |
| prompt payers | customer + payment score + days | High |
| VAT explainability | contributor + tax impact | High |
| flat total when user asked "by month" | monthly breakdown | Medium |

**Detection signals:** `RouteStatus = output_validation_failed`, OutputContractValidator missing fields.

**Action:** Do **not** return partial analysis; fix handler JSON; extend validator.

---

### 5. Explainability failures

**Definition:** Numbers may be correct but reasoning is shallow or generic.

| Bad | Good |
|-----|------|
| VAT increased because sales increased | Customer X and item Y drove 62% of VAT delta |
| Cash is low | Collections down 18% vs prior month; supplier payments up |

**Detection signals:** User feedback `needs_improvement`, note "not business aware" / "incomplete answer".

**Action:** ExplainabilityEnvelope; contributor SQL; investigation context for follow-ups.

---

## Mapping feedback → priority

| Feedback rating | Reason (UI) | Default priority |
|-----------------|-------------|------------------|
| wrong | wrong_route | High |
| wrong | wrong_numbers | Critical |
| wrong | missing_analysis | Medium |
| wrong | crashed | Critical (runtime) |
| wrong | too_many_rows | Low / Medium |
| wrong | not_business_aware | Medium (explainability) |
| wrong | incomplete_answer | Medium |
| needs_improvement | — | Low–Medium |
| helpful | — | Promote to sign-off candidate |

---

## Weekly triage order

1. **Critical** semantic + runtime (wrong numbers / crashes)  
2. **High** wrong route from feedback  
3. **Medium** output shape + incomplete analysis  
4. **Low** formatting  
5. **Enhancement** — only if repeated 3+ times in logs  

Export: `.\scripts\weekly-pilot-review.ps1`
