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
**Status:** **Closed** — all 96+ operations registered with full metadata (dateFilter, topN, explainability, shapes, evidenceSource). 20 tests. June 2026.

---

### GAP-011 — Month-end close intelligence

**Gap:** "Is month-end ready to close" lacks dedicated close checklist handler  
**Suggested Operation:** `gl.period.close.readiness` (new) or extend `gl.journal.periodend`  
**Priority:** High  
**Detected From:** `con-close-01` consolidation test  
**Status:** **Closed** — `GlPeriodCloseReadinessHandler` (SAGE-GL-PCLOSE-001): 5 SQL checks (backdated, manual journals, round journals, unreconciled bank, duplicate batches). Semantic routing, OutputContractValidator, 20 tests. June 2026.

---

### GAP-012 — Output validation coverage

**Gap:** OutputContractValidator only strict for `product.monthly.orders.analysis`  
**Priority:** High  
**Detected From:** SAGE-NEXT-001  
**Status:** **Closed** — explicit `ValidateShape` entries for all major handler shapes: payment behaviour (AR+AP), VAT reconcile/summary/contributors, GL period-close, treasury, aged debtors/creditors, collections. June 2026.

---

### GAP-013 — AP payment behaviour parity

**Gap:** AR has prompt/late/summary; AP supplier payment discipline is thin  
**Priority:** Medium  
**Detected From:** Curriculum Phase C  
**Status:** **Closed** — 4 new handlers: `supplier.payment.prompt.top`, `supplier.payment.late.top`, `supplier.payment.behavior.summary`, `supplier.payment.detail`. PaymentDisciplineScore (0-100). Uses InvNum+Vendor (NOT PostAP — no InvNumKey). 33 tests. June 2026.

---

### GAP-014 — Investigation memory (multi-turn)

**Gap:** Follow-up "show details for NEWRM01" depends on single-turn context  
**Priority:** Medium  
**Detected From:** Roadmap Layer 9  
**Status:** **Closed** — entity codes (`customerCode`, `supplierCode`, `stockCode`, `warehouseCode`) tagged into `ToolsUsedJson` as `entity:key:value`; `InvestigationContext.FromPriorAssistantMessage` recovers them; `ApplyFollowUp` applies as fallback (current-message regex wins). 17 tests. June 2026.

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
**Status:** **Closed** — `SiteSchemaProbeHandler` runs a single batched `INFORMATION_SCHEMA.COLUMNS` query for 13 core tables (Client, Vendor, PostAR, PostAP, PostGL, InvNum, Accounts, StkItem, WhseStock, StkMovement, _etblGLAccountTypes, GrpTbl, _btblInvoiceLines). Returns table presence + full column lists per table. Operation: `site.schema.probe`. 22 tests. June 2026.

---

### GAP-021 — Schema capability discovery

**Gap:** No automated probe of available tables/columns per connected site  
**Priority:** Medium  
**Detected From:** SAGE-OPS-001 recommended priorities  
**Status:** **Closed** — `SiteMetadataHandler` returns connector version, SDK version, key-table presence (8 tables), derived capability flags (arSupported, apSupported, glSupported, invoicingSupported, inventorySupported), and a `schemaProof` section confirming which tables are live. Operation: `site.metadata`. June 2026.

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
**Status:** **Closed** — `TreasuryDashboardHandler` now emits `topContributors` (top AR customers = inflow blockers, top AP suppliers = outflow pressure), `likelyCause` (ratio logic: AR>2×bank → "Collections lagging"; AP>bank → "Payables pressure"), `cashDrivers` section. `ExplainabilityEnvelope.IsExplainabilityOperation` includes treasury; drilldown hint added. 17 tests. June 2026.

---

### GAP-031 — VAT contributor analysis

**Gap:** `vat.anomalies` explainability envelope not fully standardized  
**Priority:** Medium  
**Detected From:** `con-vat-02`  
**Status:** **Closed** — `VatVarianceContributorsHandler` rewritten: split `outputVatTopContributors` (DocType 0/1/4) + `inputVatTopContributors` (DocType 5) + `vatByCategory` (standard-rated vs zero-rated count). OutputContractValidator updated. ReconcileEnvelope fields preserved for backward compat. June 2026.

---

### GAP-032 — Inventory + credit note write operations

**Gap:** No write handlers for inventory adjustments, warehouse transfers, or credit notes  
**Priority:** High  
**Detected From:** Phase 4 Block 3 write-op expansion plan  
**Status:** **Closed** — `InventoryAdjustmentPostHandler` (stock adjustment via StkMovement, SDK wire-up pending), `WarehouseTransferPostHandler` (cross-warehouse transfer, SDK wire-up pending), `SalesCreditNotePostHandler` (InvNum DocType=1), `SupplierCreditNotePostHandler` (InvNum DocType=3/RTS). All 4 added to `ConnectorWriteAllowlist` (total: 15). `ProposalTypeMap` + `HandlerCapabilityRegistry` updated. 52 tests. June 2026.

---

### GAP-034 — SSO / external identity provider support

**Gap:** No single sign-on — every user requires a WizAccountant username/password  
**Priority:** High  
**Detected From:** Phase 4 Block 4 enterprise plan requirement  
**Status:** **Closed** — `OidcTokenValidator` validates id_tokens against provider JWKS (Azure AD + Google; any OIDC-compliant provider supported). `OidcAuthService` auto-provisions users on first SSO login, links `ExternalIdentityRecord(Provider, Subject)` for repeat logins, inherits practice mode + firm context. New public endpoint `POST /api/auth/oidc/login`. JWKS cache TTL 1h. 21 tests. June 2026.

---

### GAP-035 — Billing, subscription gating, and compliance

**Gap:** No billing integration, feature gating, or POPIA/GDPR data subject rights tooling  
**Priority:** High  
**Detected From:** Phase 4 Block 4 app store + compliance requirements  
**Status:** **Closed** — `BillingService` handles Stripe/Paddle webhook events (subscription lifecycle, invoice payment); `SubscriptionRecord` per tenant (free/pro/enterprise/trialing/past_due). `IsFeatureEnabledAsync` gates features by plan. `ComplianceService` supports tenant data export (POPIA Right of Access) and user PII redaction (Right to be Forgotten). `version.json` app manifest for MSIX + direct download pipeline. RbacMiddleware extended: billing paths = Admin, user redaction = FirmAdmin. June 2026.

---

### GAP-033 — Sales and purchase order lifecycle

**Gap:** No state-transition handlers for SO/PO workflow (confirm → ship / approve → receive)  
**Priority:** High  
**Detected From:** Phase 4 Block 3 order lifecycle plan  
**Status:** **Closed** — `SalesOrderLifecycleHandler` (Confirm + Ship), `PurchaseOrderLifecycleHandler` (Approve + Receive). All 4 added to `ConnectorWriteAllowlist`. `ProposalTypeMap` covers all lifecycle types. SDK state-transition method wire-up marked TODO pending Sage Evolution method confirmation. June 2026.

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
