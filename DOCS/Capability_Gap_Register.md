# Capability Gap Register

Tracks unsupported query types, missing schema proof, weak explainability, and runtime risks. **New handlers must close a registered gap with triage evidence.**

## Status key

| Status | Meaning |
|--------|---------|
| **Open** | Not started |
| **In Progress** | Active development |
| **Closed** | Implemented + tested + sign-off path |
| **Deferred** | Valid but not pilot priority |

---

## Closed gaps (reference)

### GAP-001 — Monthly product quantity/value analysis

**Gap:** No grouped monthly product trend handler  
**Operation:** `product.monthly.orders.analysis`  
**Priority:** Critical  
**Detected From:** Real user query (production crash May 2026)  
**Next Action:** ~~implement handler + output validation~~ **Done**  
**Status:** Closed  
**Evidence:** `ProductMonthlyOrdersAnalysisHandler`, `OutputContractValidator`, `pmo-01-user`

---

### GAP-002 — Payment behaviour vs outstanding confusion

**Gap:** Prompt payer queries misrouted to outstanding/unpaid  
**Operation:** `customer.payment.prompt.top` / guards  
**Priority:** Critical  
**Detected From:** Pilot + consolidation tests  
**Status:** Closed (confusion guards + 36 payment tests)

---

### GAP-003 — SQLite DateTimeOffset in chat history

**Gap:** EF ORDER BY on DateTimeOffset crashed chat  
**Priority:** Critical  
**Detected From:** Production runtime failure  
**Status:** Closed (in-memory sort in ReadOnlyChatService)

---

## Open gaps — handler coverage

### GAP-010 — HandlerCapability matrix incomplete

**Gap:** ~96 registry operations; ~18 declare full capability metadata  
**Issue:** CompatibilityGate permissive for undeclared handlers  
**Priority:** High  
**Detected From:** SAGE-NEXT-001 audit  
**Next Action:** Expand `HandlerCapabilityRegistry` for top 10 canonical + reconciliation + explainability handlers  
**Status:** In Progress

---

### GAP-011 — Month-end close intelligence

**Gap:** "Is month-end ready to close" lacks dedicated close checklist handler  
**Suggested Operation:** `gl.period.close.readiness` (new) or extend `gl.journal.periodend`  
**Priority:** High  
**Detected From:** `con-close-01` consolidation test  
**Next Action:** Define close signals (unposted journals, TB imbalance, open periods)  
**Status:** Open

---

### GAP-012 — Output validation coverage

**Gap:** OutputContractValidator only strict for `product.monthly.orders.analysis`  
**Priority:** High  
**Detected From:** SAGE-NEXT-001  
**Next Action:** Add validators for payment behaviour, VAT explainability, reconciliation shapes  
**Status:** In Progress

---

### GAP-013 — AP payment behaviour parity

**Gap:** AR has prompt/late/summary; AP supplier payment discipline is thin  
**Priority:** Medium  
**Detected From:** Curriculum Phase C  
**Next Action:** Mirror AR payment handlers for suppliers if pilot asks  
**Status:** Open

---

### GAP-014 — Investigation memory (multi-turn)

**Gap:** Follow-up "show details for NEWRM01" depends on single-turn context  
**Priority:** Medium  
**Detected From:** Roadmap Layer 9  
**Next Action:** Persist InvestigationContext across conversation turns  
**Status:** Open

---

### GAP-015 — CFO composite analytics

**Gap:** Cross-domain questions (margin + cash + VAT) have no single handler  
**Priority:** Enhancement  
**Detected From:** Roadmap Phase C/D  
**Status:** Deferred

---

## Open gaps — schema & runtime

### GAP-020 — Company-specific SQL proof

**Gap:** Handlers assume Evolution 200 column names; not verified on every pilot DB  
**Priority:** High  
**Detected From:** Knowledge checklist  
**Next Action:** SSMS proof per failed handler; document in handler `EvidenceSource`  
**Status:** Open

---

### GAP-021 — Schema capability discovery

**Gap:** No automated probe of available tables/columns per connected site  
**Priority:** Medium  
**Detected From:** SAGE-OPS-001 recommended priorities  
**Next Action:** Connector metadata endpoint for handler design  
**Status:** Open

---

### GAP-022 — Mega-digest fallback rate

**Gap:** Most of 500 digest titles still fall back to explanation-only  
**Priority:** Medium  
**Detected From:** Training index honest status  
**Next Action:** Promote only repeated real queries — not bulk handler expansion  
**Status:** Open (by design in pilot phase)

---

## Open gaps — explainability

### GAP-030 — Treasury explainability depth

**Gap:** `treasury.dashboard` may not fully answer "why is cash low"  
**Priority:** High  
**Detected From:** `con-cash-01`, PS-007 Experimental  
**Next Action:** Contributor breakdown (collections, payments, timing)  
**Status:** Open

---

### GAP-031 — VAT contributor analysis

**Gap:** `vat.anomalies` explainability envelope not fully standardized  
**Priority:** Medium  
**Detected From:** `con-vat-02`  
**Next Action:** Align with ExplainabilityEnvelope schema  
**Status:** In Progress

---

## How to add a gap

```text
Gap ID:
Gap:
Operation (if known):
Issue:
Priority:
Detected From:
Next Action:
Status:
```

Link to Real Query Bank ID when available.

---

## Promotion rule

**No random handler growth.** A gap closes only when:

1. Repeated in triage OR marked wrong 2+ times  
2. Real Query Bank entry exists  
3. Intent tests with must-not-route added  
4. Regression green  
5. Pilot sign-off updated  
