# Master Cursor Prompt — Sage AI Agent Training

## Purpose

This is the master prompt to be pasted into Cursor after importing the Sage AI Agent training MD files.

---

```text
You are upgrading the Sage 200 Evolution AI Agent.

Read and fully understand ALL imported MD training files before making any code changes.

Your objective is to transform the Sage AI Agent into:

- Finance-aware business analytical assistant
- Sage reconciliation assistant
- SQL semantic query engine
- Safe transaction assistant

NOT:

- Generic chatbot
- SDK wrapper
- Table dump utility

--------------------------------------------------
CORE RULES
--------------------------------------------------

1. Always classify INTENT first before executing any query.

Detect:
- Aggregation queries
- Ranking queries
- Listing queries
- Reconciliation queries
- Datafix queries
- Audit/investigation queries

--------------------------------------------------
AGGREGATION RULES
--------------------------------------------------

If user says:
- how many
- count
- total
- number of

Then:
- Use COUNT/SUM/AVG aggregation
- Return summarized numeric result only
- NEVER dump transaction rows

GOOD:
Total Invoices with Discounts: 214

BAD:
Showing 500 of 9373 rows

--------------------------------------------------
RANKING RULES
--------------------------------------------------

If user says:
- top 5
- highest
- lowest
- oldest
- newest

Then:
- Use TOP/LIMIT
- Sort appropriately
- Return only requested number of rows

--------------------------------------------------
RECONCILIATION RULES
--------------------------------------------------

If user says:
- matching
- reconcile
- variance
- not matching
- difference

Then:
- Enter reconciliation workflow
- Compare datasets
- Show variances
- Suggest drilldown

--------------------------------------------------
DATAFIX RULES
--------------------------------------------------

If user says:
- fix it
- resolve
- adjust
- correct

Then:
- Enter diagnostic mode
- Generate preview only
- NEVER auto-post transactions
- Require confirmation before posting

--------------------------------------------------
MEGA DIGEST FALLBACK RULE
--------------------------------------------------

If no SQL handler exists:
- Match query against Mega Digest catalog
- Return closest business intent
- DO NOT return generic help

Response format:

Recognized business intent:
[Catalog Title]

Domain:
[Domain]

Status:
SQL handler not implemented yet

Next action:
Implement SQL handler for this catalog item

--------------------------------------------------
RESPONSE FORMATTING RULES
--------------------------------------------------

Count Query:
→ Single summarized value

Top Query:
→ Limited ranked rows only

Listing Query:
→ Filtered rows only

Reconciliation Query:
→ Comparison summary + variance

Fix Query:
→ Diagnostic preview only

--------------------------------------------------
SQL vs SDK RULES
--------------------------------------------------

Use SQL for:
- analytics
- reconciliation
- reporting
- variance analysis
- audit investigation

Use SDK for:
- posting
- transactions
- object creation
- approvals

--------------------------------------------------
IMPORTANT BEHAVIOR RULES
--------------------------------------------------

1. Never dump excessive rows unless explicitly requested.
2. Never ignore row limits requested by user.
3. Never confuse COUNT query with LIST query.
4. Never confuse Balance Sheet stock credit balances with negative stock quantities.
5. Never auto-post fixes.
6. Always prioritize business meaning over table meaning.
7. Always produce finance-aware responses.

--------------------------------------------------
IMPLEMENTATION APPROACH
--------------------------------------------------

Phase 1:
- Intent classifier
- Response formatter

Phase 2:
- SQL handler registry
- Mega Digest matcher

Phase 3:
- Reconciliation engine
- Datafix preview engine

Phase 4:
- Natural language transaction engine

--------------------------------------------------
VALIDATION
--------------------------------------------------

After every implementation:
- Run regression tests
- Validate golden test cases
- Report unsupported handlers clearly
- Ensure existing handlers are not broken

When task is complete, play a chime sound.
```
