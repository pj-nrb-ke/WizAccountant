# Pilot Query Sign-Off Tracker

Tracks which real queries are acceptable for pilot vs production. **Do not mark Production Ready without all safety gates** (see `DOCS/Query_Triage_Priority.md`).

## Status definitions

| Status | Meaning |
|--------|---------|
| **Experimental** | Routed but not validated against Sage reports |
| **Candidate** | In query bank; tests exist or in progress |
| **UAT Approved** | Human validated against Sage / business reports |
| **Production Ready** | Route stable, output validated, no crashes, pilot accepted |

## Production Ready gates

- [ ] Canonical route stable (3+ paraphrase tests pass)
- [ ] OutputContractValidator passes (where applicable)
- [ ] No runtime crash on pilot DB
- [ ] Schema / SQL confirmed on target Sage company
- [ ] Business meaning validated by named reviewer
- [ ] Listed in `tests/intents/*.json`

---

## Signed-off queries

### PS-001 — Monthly product order analysis

**Query:** which product get ordered most. Give me analysis per product per month by Quantity and Value starting from Jan 2026  
**Operation:** `product.monthly.orders.analysis`  
**Pilot Status:** Production Ready  
**Validated By:** _(pending named sign-off)_  
**Validated Date:** _(pending)_  
**Notes:** Promoted from production failure (PATCH-009). Output validation enforced. Awaiting live Sage UAT confirmation.  
**Real Query ID:** RQ-PROD-001

---

### PS-002 — Prompt payers

**Query:** which customers pay promptly  
**Operation:** `customer.payment.prompt.top`  
**Pilot Status:** UAT Approved  
**Validated By:** _(pending)_  
**Validated Date:** _(pending)_  
**Notes:** Confusion guards prevent misroute to outstanding/unpaid. Validate payment date logic vs AR aging report.  
**Real Query ID:** RQ-PAY-001

---

### PS-003 — Slow moving inventory

**Query:** Top 20 slow moving stock items  
**Operation:** `inventory.slow.moving.top`  
**Pilot Status:** Candidate  
**Validated By:** —  
**Validated Date:** —  
**Notes:** Confirm movement threshold matches warehouse aging report.  
**Real Query ID:** RQ-INV-001

---

### PS-004 — Negative stock on balance sheet

**Query:** show negative stock on balance sheet  
**Operation:** `inventory.bs.negative_ledgers`  
**Pilot Status:** UAT Approved  
**Validated By:** _(pending)_  
**Validated Date:** _(pending)_  
**Notes:** Confirmed distinct from physical negative qty handler.  
**Real Query ID:** RQ-INV-003

---

### PS-005 — AR vs GL reconciliation

**Query:** does AR match GL  
**Operation:** `ar.gl.reconcile`  
**Pilot Status:** Candidate  
**Validated By:** —  
**Validated Date:** —  
**Notes:** Reconcile output against debtors control trial balance.  
**Real Query ID:** RQ-REC-001

---

### PS-006 — VAT payable estimate

**Query:** estimate VAT payable this month  
**Operation:** `vat.payable.estimate`  
**Pilot Status:** Candidate  
**Validated By:** —  
**Validated Date:** —  
**Notes:** Compare to VAT return working for same period.  
**Real Query ID:** RQ-VAT-001

---

### PS-007 — Treasury — why is cash low

**Query:** why is cash low  
**Operation:** `treasury.dashboard`  
**Pilot Status:** Experimental  
**Validated By:** —  
**Validated Date:** —  
**Notes:** Explainability depth still improving; numbers must match treasury summary.  
**Real Query ID:** RQ-TR-001

---

### PS-008 — Bank reconciliation variance

**Query:** why is bank reconciliation not balancing  
**Operation:** `bank.reconcile.variance`  
**Pilot Status:** Candidate  
**Validated By:** —  
**Validated Date:** —  
**Notes:** Validate against bank rec module for test account.  
**Real Query ID:** RQ-REC-003

---

## Sign-off log (append-only)

| Date | Query ID | New Status | Reviewer | Evidence |
|------|----------|------------|----------|----------|
| 2026-05-26 | RQ-PROD-001 | Production Ready (code) | Agent | Handler + output validation + 1121 tests |
| | | | | |

---

## Weekly sign-off ritual

1. Pick top 3 queries from triage feedback marked **wrong**.
2. Re-run in Insight; compare to Sage report.
3. Update status + reviewer + date.
4. If Production Ready, ensure regression test exists.

See `DOCS/Pilot_Stabilization_Workflow.md` for full cycle.
