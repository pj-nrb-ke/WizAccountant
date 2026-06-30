# QA Checklist — Task #16: Inventory write handlers — warehouse transfer, credit note, RTS

> **Instructions for Varun's AI session (Claude Code / Cursor)**
>
> Run each test in sequence. For every item write `[PASS]` or `[FAIL: reason + error output]`.
> Include **full terminal output** for compile and test steps — do not summarise.
> - **CRITICAL** items: ALL must pass
> - Overall pass rate must be ≥ 80%
> - Post results directly to GitHub (see FINAL STEP below)

---

## Standard Tests (every task must pass these)

### Compilation
- [ ] **CRITICAL** `dotnet build src/WizAccountant.Api --no-incremental` — zero errors
  _Paste full build output summary line:_

- [ ] No new compiler warnings vs. baseline (compare warning count before and after your changes)

### Regression
- [ ] **CRITICAL** `dotnet test` — all pre-existing tests still pass
  _Paste summary line e.g. "Passed: 47, Failed: 0, Skipped: 0":_

### Security
- [ ] **CRITICAL** No secrets in diff: `git diff origin/main --name-only` — no `.env`, `*.key`, `*.pfx`, `appsettings.Production.json`
- [ ] No hardcoded credentials, API keys, or passwords in any new or modified file

---

## Task-Specific Tests

### Write Safety Gates
- [ ] **CRITICAL** All 3 write handlers return 403 when `WritesEnabled: false`
- [ ] **CRITICAL** All 3 write handlers return 403 when tray consent file absent
- [ ] Both gates required simultaneously — one gate alone insufficient
- [ ] Idempotency key prevents duplicate write on retry

### Warehouse Transfer
- [ ] **CRITICAL** `inventory.warehouse.transfer.post` compiled and registered
- [ ] Valid request posts to Sage, returns success
- [ ] Inventory levels updated in Sage after transfer

### Credit Note
- [ ] **CRITICAL** Sales credit note handler functional
- [ ] **CRITICAL** Supplier credit note handler functional

### Return to Supplier (RTS)
- [ ] **CRITICAL** `inventory.rts.post` handler functional
- [ ] Stock returned correctly, supplier balance adjusted

### Error Cases
- [ ] Insufficient stock quantity → clear error (not crash)
- [ ] Invalid warehouse ID → clear error

---

## FINAL STEP — DO NOT SKIP

Run every test above in your Claude / Cursor session. Then do ALL THREE steps below:

**Step 1** — Save your results to a local file named `qa-results.md`

**Step 2** — Post the file directly as a GitHub comment (bypasses manual copy-paste):
```
gh issue comment 1 --repo pj-nrb-ke/WizAccountant --body-file qa-results.md
```
_(Replace `1` with today's daily issue number if different.)_

**Step 3** — Compute SHA-256 of the file and append as the LAST LINE of your comment:
```
certutil -hashfile qa-results.md SHA256
```
Append to your comment (edit before submitting):
```
INTEGRITY: sha256:<paste-hash-here>
```

> **Do NOT edit the comment after posting.**
> The PM system verifies the hash independently.
> Any mismatch results in automatic rejection and a flag to PJ.

---
_QA Checklist — WizAccountant PM System_
