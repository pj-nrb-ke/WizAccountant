# QA Checklist — Task #29: Multi-tenancy (full) — data isolation, tenant admin portal

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

### Prerequisite
- [ ] **CRITICAL** Task #15 (Multi-company connector) confirmed complete

### Data Isolation
- [ ] **CRITICAL** Tenant A cannot see Tenant B sites via API (test with 2 tenants)
- [ ] **CRITICAL** Tenant A cannot see Tenant B jobs or reports
- [ ] **CRITICAL** Tenant A cannot see Tenant B users
- [ ] All queries auto-scoped by tenant ID in middleware (no per-handler filter needed)

### Tenant Admin Portal
- [ ] **CRITICAL** Tenant admin lists own users
- [ ] Tenant admin views own sites and connection status
- [ ] Tenant admin downloads own reports
- [ ] Tenant admin CANNOT access another tenant's data

### Super-Admin
- [ ] Super-admin lists all tenants
- [ ] Super-admin views per-tenant usage stats
- [ ] Super-admin cannot be created via public API (seed-only)

### Security
- [ ] Cross-tenant resource access returns 403 — NOT 404 (404 leaks existence)

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
