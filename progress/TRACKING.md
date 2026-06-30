# WizAccountant / WizGate — Project Tracking
**Project Manager:** PJ (pj@wizag.biz)  
**Developer:** Varun  
**Last Updated:** 2026-06-30 22:00 EAT  
**Update Method:** Auto-updated by Claude PM session via GitHub issue comments

---

## Progress Summary

| Metric | Value |
|--------|-------|
| Total pending tasks | 23 |
| Completed | 0 |
| In Progress | 2 (Tasks 8, 11) |
| Blocked | 1 (Task 11) |
| Not Started | 21 |
| Estimated total AI hours | 34.5 hrs |
| Hours consumed | 3.0 hrs |
| Hours remaining | 31.5 hrs |

---

## Task Status Board

| # | Module / Feature | Block | Hours | Status | Assigned | Last Updated | Notes |
|---|-----------------|-------|-------|--------|----------|-------------|-------|
| 8 | Schema probe queries + connector metadata (GAP-020/021) | BLOCK 1 | 0.5 | 🔵 In Progress | Varun | 2026-06-30 | WIP — continuing next session |
| 11 | RBAC v2 — fine-grained role model + middleware | BLOCK 1 | 1.0 | 🔴 Blocked | Varun | 2026-06-30 | Got stuck — troubleshooting. Prerequisite for Task 30 |
| 12 | Practice mode — FirmRecord + multi-site context | BLOCK 2 | 1.0 | ⚪ Not Started | Varun | — | |
| 13 | Monitoring — site SLA dashboard + failed-job alerts | BLOCK 2 | 1.0 | ⚪ Not Started | Varun | — | |
| 14 | Mobile Phase 4 — practice mode + inventory read + tablet layout | BLOCK 2 | 1.5 | ⚪ Not Started | Varun | — | |
| 15 | Multi-company connector support | BLOCK 3 | 1.0 | ⚪ Not Started | Varun | — | Prerequisite for Task 29 |
| 16 | Inventory write handlers — warehouse transfer, credit note, RTS | BLOCK 3 | 1.0 | ⚪ Not Started | Varun | — | GAP-032 |
| 17 | Order processing — SO/PO lifecycle handlers | BLOCK 3 | 1.0 | ⚪ Not Started | Varun | — | GAP-033 |
| 18 | SSO — Azure AD / Google OIDC integration | BLOCK 4 | 1.5 | ⚪ Not Started | Varun | — | |
| 19 | Billing hooks + compliance docs + app store pipeline | BLOCK 4 | 1.5 | ⚪ Not Started | Varun | — | |
| 20 | Stock Take — list sessions, post count adjustments | BLOCK 3 | 0.5 | ⚪ Not Started | Varun | — | Prerequisite for Task 26 |
| 21 | Fixed Assets (minimal) — list, get, post depreciation | BLOCK 4 | 1.0 | ⚪ Not Started | Varun | — | Minimal only — VizAsset handles full management |
| 22 | Bank Reconciliation — statements, unreconciled txns, reconcile | BLOCK 4 | 1.5 | ⚪ Not Started | Varun | — | |
| 23 | Reports — Customer (aged debtors, statement, ledger) | BLOCK 4 | 1.0 | ⚪ Not Started | Varun | — | |
| 24 | Reports — Vendor (aged creditors, statement, ledger) | BLOCK 4 | 1.0 | ⚪ Not Started | Varun | — | |
| 25 | Reports — Inventory (stock levels, movement, valuation) | BLOCK 4 | 1.0 | ⚪ Not Started | Varun | — | |
| 26 | Reports — Stock Take variance report | BLOCK 4 | 0.5 | ⚪ Not Started | Varun | — | Depends on Task 20 |
| 27 | Reports — Financial (Trial Balance, Balance Sheet, P&L) | BLOCK 4 | 2.0 | ⚪ Not Started | Varun | — | Most complex report task |
| 28 | SaaS module — subscription plans, onboarding, billing | BLOCK 5 | 3.0 | ⚪ Not Started | Varun | — | |
| 29 | Multi-tenancy (full) — data isolation, tenant admin portal | BLOCK 5 | 2.5 | ⚪ Not Started | Varun | — | Depends on Task 15 |
| 30 | User Rights — role management UI + permission matrix | BLOCK 5 | 1.5 | ⚪ Not Started | Varun | — | Depends on Task 11 |
| 31 | E-commerce integration — Shopify / WooCommerce | BLOCK 6 | 4.0 | ⚪ Not Started | Varun | — | Shopify first, then WooCommerce |
| 32 | Import — QuickBooks Online + Tally transactions | BLOCK 6 | 4.0 | ⚪ Not Started | Varun | — | QB first, then Tally |

**Status key:** ⚪ Not Started · 🔵 In Progress · ✅ Completed · 🔴 Blocked

---

## Session Log

| Date | Session | Tasks Worked | Completed | Hours Used | Blocker? | Report |
|------|---------|--------------|-----------|------------|----------|--------|
| 2026-06-30 | Demo PM | #8, #11 | None | 3.0 hrs | Task #11 — troubleshooting | [Issue #1](https://github.com/pj-nrb-ke/WizAccountant/issues/1) |

---

## Blockers / Escalations

| Date | Task | Blocker | Raised By | Status |
|------|------|---------|-----------|--------|
| 2026-06-30 | #11 — RBAC v2 | Got stuck troubleshooting — needs PJ review or clarification | Varun | 🔴 Open |

---

## WizGate P0 Checklist (must complete before first client)

- [ ] Task 0: Rename `DataGate.*` → `WizGate.*` source projects
- [ ] Task 1: Create `infra/caddy/Caddyfile`
- [ ] Task 2: Deploy to Linux VPS with HTTPS + real `Jwt__Key`
- [ ] Task 3: Full end-to-end pairing test on real Windows PC

---

_This file is maintained automatically by the Claude PM session. Do not edit manually._
