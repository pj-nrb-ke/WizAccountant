# Sage AI Agent Patch — Mega Digest Fallback + Catalog Matching

## Problem

Cursor reported:

```text
Not all 500 have dedicated SQL yet; unmatched ones should show a mega digest hint with the closest catalog title instead of generic help only.
```

This is correct.

The 500-query mega digest is a training catalog, not a complete SQL implementation pack. The AI Agent should not require all 500 entries to have dedicated SQL before becoming useful.

## Required Behavior

When a user asks a Sage business question, the AI Agent must follow this sequence:

```text
User Query
→ Normalize query
→ Detect intent/domain
→ Match against implemented SQL catalog
→ If exact SQL exists, execute it
→ If exact SQL does not exist, match against Mega Digest catalog
→ Return closest known business intent + explain that SQL implementation is pending
→ Never return generic help only
```

## Fallback Rule

If no dedicated SQL handler exists, the Agent must respond like this:

```text
I understand this as: [Closest Mega Digest Catalog Title]

This query is recognized in the Sage AI Agent training catalog, but the dedicated SQL handler is not implemented yet.

Closest supported intent:
[Domain] → [Catalog Title]

Suggested next action:
Ask Cursor to implement the SQL handler for this catalog item.
```

## Do Not Do This

The Agent must not respond with:

```text
I can help with live Sage reads on Test 1...
Try: show GL transactions...
```

That is generic fallback and should be avoided.

## Required Catalog Matching Logic

The Agent should maintain three levels of matching:

### 1. Exact SQL Handler Match

Example:

```text
inventory valuation matching balance sheet stock value
```

Action:

```text
Run implemented reconciliation SQL.
```

### 2. Mega Digest Catalog Match

Example:

```text
who are my worst paying customers
```

Closest catalog title:

```text
Which customers pay late consistently?
```

Action:

```text
Return catalog hint if SQL handler is not implemented.
```

### 3. Domain-Level Fallback

Example:

```text
show me risky customer accounts
```

Closest domain:

```text
Accounts Receivable / Credit Risk
```

Action:

```text
Return AR risk analysis hint, not generic system help.
```

## Suggested Cursor Prompt

Use this prompt in Cursor:

```text
Implement a Sage AI Agent fallback matcher for the 500-query Mega Digest.

Requirement:
If a user query has no dedicated SQL handler, do not return generic help.
Instead:
1. Normalize the user query.
2. Search the Mega Digest catalog titles.
3. Return the closest catalog title, domain, and intent.
4. State that the SQL handler is pending.
5. Suggest the next implementation action.

Response format:
"Recognized business intent: [Catalog Title]
Domain: [Domain]
Status: SQL handler not yet implemented
Next action: Implement SQL handler for this catalog item."

Keep existing implemented SQL handlers unchanged.
This is a fallback improvement only.

When the task is done, play a chime sound.
```

## Example

User Query:

```text
show me customers with oldest credit balances
```

If no SQL exists, Agent should return:

```text
Recognized business intent: Customers with credit balances / oldest AR credit balances
Domain: Accounts Receivable
Status: SQL handler not yet implemented
Next action: Implement SQL handler using AR open transaction aging logic.
```

## Recommended Data Structure

Cursor should convert the Mega Digest into a simple searchable catalog:

```json
[
  {
    "id": "AR-001",
    "domain": "Accounts Receivable",
    "title": "Top 10 customers with oldest aged debit balances",
    "keywords": ["customers", "oldest", "aged", "debit", "balances", "overdue"],
    "handler": "ar_oldest_aged_debit_balances",
    "implemented": false
  }
]
```

## Matching Priority

The matcher should rank by:

1. Exact phrase match
2. Keyword overlap
3. Domain keywords
4. Semantic similarity
5. Fallback to closest domain

## Final Rule

The Mega Digest should act as the Agent's business-intent memory.

Even if SQL is missing, the Agent should sound like it understood the Sage business question.
