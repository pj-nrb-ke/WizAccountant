# Sage AI Agent Patch — Fix Intent + Inventory Reconciliation Execution Guardrails

## 1. Purpose

This patch fixes a second failure in the Sage AI Agent.

The agent handled this query:

```text
is inventory valuation matching balance sheet stock value
```

better after the earlier patch, but failed again when the user changed the wording to:

```text
inventory valuation is not matching balance sheet stock value. Can you fix it?
```

The agent still returned the same incorrect reconciliation result:

```text
Balance Sheet Stock Value: 83,796,752.11
Inventory Valuation: 4,079,383.72
Difference: -79,717,368.39
```

This is still wrong because the valuation side is not a valid Sage Inventory Valuation total.

This patch teaches the agent three things:

1. “Can you fix it?” is not just a reconciliation query.
2. It must trigger an investigation + datafix preview workflow.
3. It must reject obviously invalid valuation results and not present them as final.

## 2. Critical Issue

The agent says it ran:

```text
SAGE-INVVAL-RECON-CANONICAL-001
```

but the returned valuation of `4,079,383.72` is not credible as a full Sage Inventory Valuation total.

This indicates one of these problems:

- The code still used SDK item valuation somewhere.
- The SQL canonical valuation query was not actually executed.
- The query failed internally and fell back to SDK valuation.
- The query filtered too narrowly.
- The query excluded most stock groups/items.
- The query calculated only a subset of warehouses/items.
- The response used cached/old values from the previous wrong logic.

The agent must not present such result as final.

## 3. Mandatory No-Fallback Rule

For Inventory Valuation vs Balance Sheet reconciliation:

```text
Never fallback to SDK item valuation.
Never fallback to cached result.
Never fallback to partial valuation.
Never present a valuation result unless the Sage SQL valuation query executed successfully.
```

If the Sage SQL valuation query fails, the response must be:

```text
I could not complete the reconciliation because the Sage SQL valuation query failed.
I will not use SDK item valuation as a substitute.
Please check the SQL error/log.
```

## 4. Mandatory Valuation Sanity Check

After running the valuation query, the agent must perform sanity checks before giving the answer.

### Required checks

| Check | Rule |
|---|---|
| Valuation row count | Must be greater than 0 and broadly credible |
| Valuation account count | Must cover expected inventory accounts/groups |
| Valuation total | Must not be suspiciously tiny compared with GL unless confirmed by detail |
| GL duplication | GL must be summed once per distinct GL account |
| Query source | Must confirm SQL valuation source, not SDK |
| Result timestamp | Must not reuse cached old result |
| Detail totals | Detail rows must sum to grand total |

### Hard stop rule

If Balance Sheet inventory is large but valuation is extremely small, e.g.:

```text
Balance Sheet = 83,796,752.11
Valuation = 4,079,383.72
```

the agent must not say “not matching” as final.

It must say:

```text
The reconciliation result failed sanity validation. The valuation side appears incomplete.
I will not treat this as a valid mismatch until the valuation SQL detail is corrected.
```

## 5. “Can You Fix It?” Intent

When the user says:

```text
Can you fix it?
Please fix it.
Resolve it.
Correct it.
Datafix it.
Make it match.
```

the agent must classify intent as:

```json
{
  "domain": "Inventory + General Ledger",
  "intent": "inventory_reconciliation_fix_workflow",
  "risk_level": "diagnostic_then_datafix_preview",
  "requires_confirmation": true
}
```

The agent must not jump directly to posting a fix.

It must follow the same investigation workflow we used successfully.

## 6. Correct Fix Workflow

For:

```text
inventory valuation is not matching balance sheet stock value. Can you fix it?
```

the agent must follow this sequence.

### Step 1 — Run valid reconciliation

Run the canonical SQL valuation-vs-GL query.

If valuation SQL fails or looks incomplete, stop and report query failure/sanity failure.

### Step 2 — Find variance by account/stock group

If mismatch is real:

```text
Drill down by GL account
Drill down by stock group
Drill down by item/warehouse
```

### Step 3 — Identify root cause category

Classify the mismatch into one of these:

| Cause Type | Evidence |
|---|---|
| Opening migration mismatch | CostTracking opening differs from PostST/GL opening |
| Wrong GL account posting | PostST.iGLAccountID not equal expected stock GL |
| Stock group changed | Current GrpTbl mapping differs from historical posting |
| Service item mapped to inventory | ServiceItem = 1 using inventory stock account |
| Manual/direct GL journal | PostGL exists without stock/cost source |
| Cost tracking issue | Valuation/cost tracking wrong |

### Step 4 — Produce only a preview datafix

Datafix must be preview only:

```sql
BEGIN TRANSACTION;
-- proposed fix
ROLLBACK TRANSACTION;
```

No `COMMIT` until user explicitly asks.

### Step 5 — Ask for explicit confirmation

Use:

```text
Type APPROVE INVENTORY DATAFIX to proceed with live script.
```

## 7. Correct Response for Fix Intent

When user says:

```text
inventory valuation is not matching balance sheet stock value. Can you fix it?
```

The agent should respond like this:

```text
Finding:
I will not post a fix yet. I will first run the reconciliation and identify the exact mismatch source.

Action:
Running inventory valuation vs Balance Sheet reconciliation using Sage SQL valuation logic, not SDK.

Next:
If a real mismatch exists, I will drill down by GL account, stock group, and item/warehouse, then prepare a rollback-only datafix preview.
```

If query result is valid and mismatch exists:

```text
Finding:
Inventory valuation does not match Balance Sheet stock value.

Balance Sheet Stock Value:
[amount]

Inventory Valuation:
[amount]

Difference:
[amount]

Main Variance:
[account/group/item]

Fix Plan:
[one clear fix plan]

Datafix Status:
Preview only. No changes committed.
```

If valuation result fails sanity:

```text
Finding:
The reconciliation result is not reliable because the valuation side appears incomplete.

Balance Sheet Stock Value:
83,796,752.11

Returned Valuation:
4,079,383.72

Issue:
This valuation is suspiciously low and likely came from SDK fallback, cached data, or partial SQL execution.

Action:
I will not prepare a datafix until the valuation SQL returns a valid Sage Inventory Valuation total.
```

## 8. Forbidden Response

The agent must not respond like this:

```text
Finding:
Inventory valuation is not matching Balance Sheet stock value.

Balance Sheet Stock Value:
83,796,752.11

Inventory Valuation:
4,079,383.72

Difference:
-79,717,368.39

Next step:
Run Sage reports manually.
```

This is bad because:

1. It repeats a suspicious invalid valuation.
2. It does not attempt a fix workflow.
3. It tells the user to run Sage manually instead of acting as the AI agent.
4. It does not investigate root cause.
5. It ignores “Can you fix it?”

## 9. Canonical Execution Contract

When the agent claims it has run:

```text
SAGE-INVVAL-RECON-CANONICAL-001
```

it must return technical execution metadata:

| Field | Meaning |
|---|---|
| Query Name | Exact query/template |
| Source Used | SQL valuation / SDK / fallback |
| Fallback Used | Must be No |
| Valuation Row Count | Number of valuation lines |
| Valuation Account Count | Number of GL accounts |
| GL Account Count | Number of distinct inventory GL accounts |
| Grand Total Detail Check | Pass/Fail |
| Sanity Check | Pass/Fail |

Example:

```text
Execution Check:
Query: SAGE-INVVAL-RECON-CANONICAL-001
Source: SQL valuation logic
SDK fallback used: No
Valuation lines: 486
Inventory GL accounts: 11
Sanity check: Pass
```

If it cannot provide this, it should not claim the query is valid.

## 10. Query Result Validation Logic

Implement this pseudo-code:

```ts
function validateInventoryReconResult(result) {
  if (!result.executedSqlValuation) {
    return fail("Sage SQL valuation was not executed.");
  }

  if (result.usedSdkFallback) {
    return fail("SDK valuation fallback is forbidden for this reconciliation.");
  }

  if (result.valuationLineCount <= 0) {
    return fail("Valuation returned no lines.");
  }

  if (result.inventoryValuationValue === 0 && result.balanceSheetValue !== 0) {
    return fail("Valuation is zero while GL inventory is non-zero.");
  }

  const ratio = Math.abs(result.inventoryValuationValue / result.balanceSheetValue);

  if (result.balanceSheetValue > 1000000 && ratio < 0.25) {
    return fail("Valuation is suspiciously low compared with GL. Treat as incomplete result.");
  }

  if (!result.detailTotalsMatchGrandTotal) {
    return fail("Detail rows do not reconcile to grand total.");
  }

  return pass();
}
```

## 11. No Cache Rule

For reconciliation and fix requests:

```text
Always execute fresh query.
Do not reuse previous result.
Do not reuse cached SDK values.
Do not reuse old explanation text.
```

## 12. Routing Rule

Add these trigger phrases to the reconciliation fix workflow:

```text
can you fix it
fix it
resolve it
correct it
datafix
make it match
adjust it
repair mismatch
```

Combined with inventory terms:

```text
inventory valuation
balance sheet stock
stock value
stock valuation
GL inventory
```

must route to:

```text
inventory_reconciliation_fix_workflow
```

## 13. Cursor Implementation Prompt

Use this prompt in Cursor.

```md
Prompt SAGE-INVVAL-FIX-WORKFLOW-001

Fix the Sage AI Agent behavior for inventory reconciliation fix requests.

Current problem:
When the user asks “inventory valuation is not matching balance sheet stock value. Can you fix it?”, the agent repeats the same reconciliation response and still uses or returns a suspicious valuation total of 4,079,383.72 against GL 83,796,752.11.

Required behavior:
1. Treat “can you fix it”, “fix it”, “resolve it”, “datafix it”, “make it match” as fix-workflow triggers.
2. Classify this as inventory_reconciliation_fix_workflow.
3. Do not prepare a live fix immediately.
4. First run a valid reconciliation using Sage SQL valuation logic only.
5. Never use SDK item valuation or cached result for valuation reconciliation.
6. Add execution metadata:
   - SQL valuation executed: Yes/No
   - SDK fallback used: Yes/No
   - valuation line count
   - valuation account count
   - distinct GL account count
   - sanity check pass/fail
7. If valuation result is suspiciously low compared to GL, stop and say the reconciliation result is invalid/incomplete.
8. If reconciliation is valid and mismatch exists, run drilldown by account, stock group, item/warehouse.
9. Identify root cause type:
   - opening migration mismatch
   - wrong GL posting
   - stock group mapping issue
   - service item mapped to inventory
   - manual GL journal
   - cost tracking issue
10. Generate only a rollback datafix preview.
11. Never say “run Sage reports manually” as the next step when the agent has database access.
12. The final answer must give totals first, then root cause, then one action.

Also remove any fallback code that uses summed SDK item valuations for inventory valuation reconciliation.

When this task is complete, play a chime sound.
```

## 14. Stronger System Instruction for the Agent

Add this to the agent system/developer instructions:

```text
For Sage Inventory Valuation vs Balance Sheet reconciliation, SDK valuation is prohibited.
If SQL valuation cannot run, report failure.
Do not substitute SDK results.
When user asks to fix a mismatch, execute diagnostic workflow first and produce rollback-only datafix preview.
Do not give generic advice.
```

## 15. Final Rule

The agent must be a Sage reconciliation and datafix assistant.

For “Can you fix it?” the correct behavior is:

```text
Validate → Reconcile → Drilldown → Root Cause → Preview Fix → Await Approval
```

Not:

```text
Repeat mismatch → Recommend manual Sage reports
```
