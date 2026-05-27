# Phase 3 — Approvals and writes

## Roles (pilot)

| Email | Password | Role |
|-------|----------|------|
| preparer@pilot.local | pilot | Preparer |
| approver@pilot.local | pilot | Approver |
| admin@pilot.local | pilot | Admin |

## Flow

1. **Preparer** creates a proposal in [Act UI](http://localhost:5278/act/) or via `POST /api/act/proposals`.
2. **Approver** (different user) approves → cloud dispatches write job to connector.
3. Connector runs allowlisted write handler with **idempotency key**.
4. **Write audit** stores before/after JSON and Evolution reference.

## Enabling live posts on pilot PC

1. Set `Connector:WritesEnabled` = `true` in connector config (or user secrets).
2. Grant consent: Tray → **Allow cloud posts (1 hour)** (or disable `WriteConsentRequired`).
3. Ensure site is **Online** in admin.

## Rollback

SDK posts are **not auto-reversed** by WizAccountant. Reversals must be done in Sage Evolution.

## Write operations (allowlist)

- `gltransaction.post` — balanced journal (JSON payload)
- `customertransaction.post` / `suppliertransaction.post`
- `allocation.save`
- `customer.save` / `supplier.save`
