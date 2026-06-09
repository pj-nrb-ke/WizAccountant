# Real Insight Query Bank

Permanent training fuel for the Sage AI Agent pilot. **Real failed queries beat synthetic examples.**

## How to use

1. Add every meaningful pilot question here (good or bad).
2. Link to `tests/intents/*.json` when promoted to regression.
3. Update status as handlers mature: `Candidate` → `Implemented` → `UAT Approved` → `Production Ready`.
4. Never add duplicate canonical meanings — strengthen existing routes instead.

## Entry template

```text
Category:
Query:
Expected Meaning:
Expected Operation:
Expected Output:
Must Not Route To:
Required Metrics:
Required Shape:
Priority:
Status:
Notes:
Test ID:
```

---

## Payment Behaviour

### RQ-PAY-001 — Prompt payers (real user wording)

**Category:** Payment Behaviour / AR  
**Query:** which customer has been paying promptly i.e. clearing all outstanding balances within the respective terms  
**Expected Meaning:** customer payment discipline analysis (prompt payers)  
**Expected Operation:** `customer.payment.prompt.top`  
**Expected Output:** ranking of customers who clear invoices within terms  
**Must Not Route To:** `customer.outstanding.debit.top`, `customer.unpaid.summary`, `customer.aged.top`  
**Required Metrics:** average payment days, paid within terms %, average days late  
**Required Shape:** customer + score + payment metrics  
**Priority:** High  
**Status:** Implemented  
**Notes:** Use allocated payment dates vs invoice due dates — not current outstanding balance alone.  
**Test ID:** `pay-01-user`, `con-pay-01`

---

### RQ-PAY-002 — Slow payers

**Category:** Payment Behaviour / AR  
**Query:** which customers pay late  
**Expected Meaning:** chronic late payer ranking  
**Expected Operation:** `customer.payment.late.top`  
**Expected Output:** customers ranked by lateness / overdue ratio  
**Must Not Route To:** `customer.unpaid.summary`, `customer.aged.top`  
**Required Metrics:** average days late, overdue ratio, invoice count  
**Required Shape:** customer + lateness score + metrics  
**Priority:** High  
**Status:** Implemented  
**Test ID:** `pay-09-late`, `con-pay-03`

---

### RQ-PAY-003 — Payment behaviour summary

**Category:** Payment Behaviour / AR  
**Query:** show customer payment behaviour summary  
**Expected Meaning:** portfolio-level payment discipline overview  
**Expected Operation:** `customer.payment.behavior.summary`  
**Expected Output:** aggregate stats across customer base  
**Must Not Route To:** `customer.payment.prompt.top`, `customer.list`  
**Required Shape:** summary metrics + counts  
**Priority:** Medium  
**Status:** Implemented  
**Test ID:** `pay-15-summary`

---

## Inventory Lifecycle

### RQ-INV-001 — Slow moving stock (pilot anchor)

**Category:** Inventory Lifecycle  
**Query:** Top 20 slow moving stock items  
**Expected Meaning:** items with low turnover / stale movement  
**Expected Operation:** `inventory.slow.moving.top`  
**Expected Output:** ranked slow movers with movement evidence  
**Must Not Route To:** `inventoryitem.list`, `inventory.movement.top`  
**Required Metrics:** days since last movement, qty on hand, value  
**Required Shape:** product + movement metrics + rank  
**Priority:** High  
**Status:** Implemented (UAT pending)  
**Test ID:** `inv-01-slow`, `con-inv-02`

---

### RQ-INV-002 — Dead / non-moving stock

**Category:** Inventory Lifecycle  
**Query:** which items are dead stock  
**Expected Meaning:** zero or negligible movement over long period  
**Expected Operation:** `inventory.nonmoving`  
**Expected Output:** list of non-moving SKUs with last movement date  
**Must Not Route To:** `inventoryitem.list`  
**Required Shape:** product + last movement + qty  
**Priority:** High  
**Status:** Implemented  
**Test ID:** `con-inv-01`, `inv-02-nonmove`

---

### RQ-INV-003 — Negative stock on balance sheet

**Category:** Inventory Lifecycle / Reconciliation  
**Query:** show negative stock on balance sheet  
**Expected Meaning:** inventory GL accounts with credit balances on BS  
**Expected Operation:** `inventory.bs.negative_ledgers`  
**Expected Output:** GL accounts with abnormal inventory balances  
**Must Not Route To:** `inventory.negative.qty` (physical qty ≠ GL credit)  
**Required Shape:** GL account + balance + variance hint  
**Priority:** High  
**Status:** Implemented  
**Test ID:** `con-inv-03`, `guard-inv-bs-neg`

---

## Product / CFO Analytics

### RQ-PROD-001 — Monthly product orders (production failure → promoted)

**Category:** CFO Analytics / Inventory  
**Query:** which product get ordered most. Give me analysis per product per month by Quantity and Value starting from Jan 2026  
**Expected Meaning:** monthly product order analysis by quantity and value  
**Expected Operation:** `product.monthly.orders.analysis`  
**Expected Output:** monthly breakdown per product + top product summary  
**Must Not Route To:** `customer.unpaid.summary`, `customer.sales.top`, `inventory.movement.top`  
**Required Metrics:** quantity (`fQtyChange`), value (`fLineTotExcl`), month grouping  
**Required Shape:** product + month + quantity + value; `topProductByQuantity`  
**Priority:** Critical (was production crash)  
**Status:** Production Ready (output validation enforced)  
**Notes:** First telemetry-promoted handler. OutputContractValidator required.  
**Test ID:** `pmo-01-user`

---

## VAT

### RQ-VAT-001 — VAT payable estimate

**Category:** VAT  
**Query:** estimate VAT payable this month  
**Expected Meaning:** estimated net VAT liability for period  
**Expected Operation:** `vat.payable.estimate`  
**Expected Output:** output VAT − input VAT estimate  
**Must Not Route To:** `customer.sales.top`, `vat.by.account.top`  
**Required Shape:** aggregation with VAT components  
**Priority:** High  
**Status:** Implemented  
**Test ID:** `con-vat-01`, `vat-04-payable`

---

### RQ-VAT-002 — VAT increase explainability

**Category:** VAT / Explainability  
**Query:** why did VAT increase  
**Expected Meaning:** contributor analysis for VAT movement  
**Expected Operation:** `vat.anomalies`  
**Expected Output:** contributors with tax impact and likely cause  
**Must Not Route To:** `vat.summary` (flat totals insufficient)  
**Required Metrics:** contributor, tax impact, period comparison  
**Required Shape:** explainability envelope (finding + contributors)  
**Priority:** High  
**Status:** Implemented (explainability depth ongoing)  
**Test ID:** `con-vat-02`

---

### RQ-VAT-003 — VAT control reconciliation

**Category:** VAT / Reconciliation  
**Query:** does VAT control match transactions  
**Expected Meaning:** VAT sub-ledger vs GL control reconciliation  
**Expected Operation:** `vat.reconcile`  
**Expected Output:** match status + variance amount  
**Must Not Route To:** `vat.summary`  
**Required Shape:** reconciliation result + variance  
**Priority:** High  
**Status:** Implemented  
**Test ID:** `con-vat-rec-01`, `vat-09-recon`

---

## Treasury / Forecasting

### RQ-TR-001 — Why is cash low

**Category:** Treasury / Explainability  
**Query:** why is cash low  
**Expected Meaning:** treasury dashboard with cash drivers  
**Expected Operation:** `treasury.dashboard`  
**Expected Output:** cash position summary with inflows/outflows context  
**Must Not Route To:** `bank.cashbook` (listing ≠ explainability)  
**Required Shape:** dashboard + narrative drivers  
**Priority:** High  
**Status:** Implemented (UAT pending)  
**Test ID:** `con-cash-01`

---

### RQ-TR-002 — Cash forecast

**Category:** Forecasting / Treasury  
**Query:** Forecast cash position for next 30 days  
**Expected Meaning:** forward cash projection  
**Expected Operation:** `treasury.cash.forecast`  
**Expected Output:** projected cash by period  
**Must Not Route To:** `dashboard.summary`, `treasury.dashboard`  
**Required Shape:** forecast series + assumptions note  
**Priority:** Medium  
**Status:** Implemented  
**Test ID:** `tr-01-forecast`, `guard-forecast-not-balance`

---

## Reconciliation

### RQ-REC-001 — AR vs GL

**Category:** Reconciliation  
**Query:** does AR match GL  
**Expected Meaning:** receivables sub-ledger vs debtors control  
**Expected Operation:** `ar.gl.reconcile`  
**Expected Output:** match / variance with control account context  
**Must Not Route To:** `customer.outstanding.debit.top`  
**Required Shape:** reconciliation + variance amount  
**Priority:** High  
**Status:** Implemented  
**Test ID:** `con-recon-01`, `ar-01-recon`

---

### RQ-REC-002 — Inventory valuation vs GL

**Category:** Reconciliation  
**Query:** inventory valuation vs GL  
**Expected Meaning:** stock valuation vs inventory GL control  
**Expected Operation:** `inventory.gl.reconcile`  
**Expected Output:** valuation match status + variance  
**Must Not Route To:** `inventoryitem.list`  
**Required Shape:** reconciliation + contributor hint  
**Priority:** High  
**Status:** Implemented  
**Test ID:** `con-recon-02`, `inv-01-gl-recon`

---

### RQ-REC-003 — Bank reconciliation variance

**Category:** Reconciliation  
**Query:** why is bank reconciliation not balancing  
**Expected Meaning:** bank rec variance explainability  
**Expected Operation:** `bank.reconcile.variance`  
**Expected Output:** unmatched items / variance drivers  
**Must Not Route To:** `bank.cashbook`  
**Required Shape:** explainability + variance breakdown  
**Priority:** High  
**Status:** Implemented  
**Test ID:** `con-bank-01`, `bank-01-var`

---

## AR (classic)

### RQ-AR-001 — Top outstanding debit balances

**Category:** AR  
**Query:** top customers by outstanding balance  
**Expected Meaning:** ranked open debit balances  
**Expected Operation:** `customer.outstanding.debit.top`  
**Expected Output:** customer ranking by outstanding debit  
**Must Not Route To:** `customer.payment.prompt.top`  
**Required Shape:** customer + balance + rank  
**Priority:** High  
**Status:** Implemented  
**Test ID:** `con-guard-01`

---

### RQ-AR-002 — Highest unpaid / most owing

**Category:** AR  
**Query:** highest unpaid customer balances  
**Expected Meaning:** customers with largest unpaid invoice exposure  
**Expected Operation:** `customer.unpaid.summary`  
**Expected Output:** unpaid summary ranking  
**Must Not Route To:** `customer.payment.prompt.top`  
**Required Shape:** customer + unpaid total + invoice count  
**Priority:** High  
**Status:** Implemented  
**Test ID:** `guard-06`

---

## AP

### RQ-AP-001 — Top aged payables

**Category:** AP  
**Query:** Top 5 suppliers with oldest aged payable balances  
**Expected Meaning:** supplier aging ranking  
**Expected Operation:** `supplier.aged.top`  
**Expected Output:** suppliers ranked by age of payable  
**Must Not Route To:** `supplier.list`  
**Required Shape:** supplier + aged balance + buckets  
**Priority:** Medium  
**Status:** Implemented  
**Test ID:** `ap-01-aged`

---

## Month-End

### RQ-CLOSE-001 — Month-end readiness

**Category:** Month-End  
**Query:** is month-end ready to close  
**Expected Meaning:** period-end close checklist / blocking items  
**Expected Operation:** `gl.journal.periodend` (preferred)  
**Expected Output:** period-end journals + close readiness signals  
**Must Not Route To:** `gltransaction.list`  
**Required Shape:** checklist-style summary  
**Priority:** High  
**Status:** Candidate (handler partial — capability gap)  
**Notes:** Needs dedicated close intelligence handler per roadmap Phase C.  
**Test ID:** `con-close-01`

---

## Discount Governance

### RQ-DISC-001 — GP decline / discount drivers

**Category:** Discount Governance  
**Query:** why is GP declining  
**Expected Meaning:** discount / margin erosion analysis  
**Expected Operation:** `salesinvoice.discount.top` (preferred)  
**Expected Output:** top discounted invoices / customers affecting GP  
**Must Not Route To:** `customer.sales.top`  
**Required Shape:** ranking + discount metrics  
**Priority:** Medium  
**Status:** Candidate  
**Test ID:** `con-disc-01`

---

## Audit

### RQ-AUDIT-001 — Suspicious round journals

**Category:** Audit  
**Query:** Suspicious round value journal postings  
**Expected Meaning:** audit anomaly detection on round amounts  
**Expected Operation:** `gl.journal.round`  
**Expected Output:** suspicious journal list  
**Must Not Route To:** `gltransaction.list`  
**Required Shape:** journal + amount + user + date  
**Priority:** Medium  
**Status:** Implemented  
**Test ID:** `audit-01-round`

---

## Explainability

### RQ-EXPL-001 — Inventory GL mismatch why

**Category:** Explainability / Reconciliation  
**Query:** why is inventory not matching GL  
**Expected Meaning:** explain variance between stock and GL  
**Expected Operation:** `inventory.gl.explain`  
**Expected Output:** finding + contributors + drilldown path  
**Must Not Route To:** `inventory.gl.reconcile` alone if user asks "why"  
**Required Shape:** explainability envelope  
**Priority:** High  
**Status:** Implemented  
**Test ID:** `con-inv-expl-01`, `inv-05-explain`

---

## Adding new entries

When a pilot user asks something new:

1. Copy the template at the top.
2. Paste **exact user wording** (including typos).
3. Record what went wrong if it failed.
4. Set **Priority** using `DOCS/Query_Triage_Priority.md`.
5. After fix + regression, move to `DOCS/Pilot_Query_Signoff.md`.

**Rule:** No new handler without a Real Query Bank entry + triage evidence.
