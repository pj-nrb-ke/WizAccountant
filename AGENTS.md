# WizAccountant — Agent instructions (Sage 200 Evolution)

When working on **Insight AI chat**, **WizConnector** read handlers, or Sage SQL, read the training pack first:

| Start here | Path |
|------------|------|
| **Remote Sage + Brevo + deploy (manufacturing handover)** | `DOCS/MANUFACTURING-AGENT-HANDOVER.md` |
| Training index | `DOCS/SAGE-AI-AGENT-TRAINING-INDEX.md` |
| Master Cursor prompt | `DOCS/Sage_AI_Training/Sage_AI_Agent_Master_Cursor_Prompt.md` |
| Framework (01–10) | `DOCS/Sage_AI_Training/` |
| Sage DB layers | `DOCS/SAGE-200-DATABASE-LAYERS.md` |
| Handover / tables | `DOCS/Sage_200_Evolution_Database_Handover.md` |
| 500-query catalog | `DOCS/Sage_AI_Agent_500_Common_Business_Queries_Mega_Digest.md` |
| Live intent routing | `DOCS/INSIGHT-CHAT-INTENTS.md` |

## Role

You are upgrading a **finance-aware Sage 200 Evolution assistant** — not a generic SDK wrapper or table dump utility.

**Business meaning > table meaning.** Example: “negative stock on balance sheet” = inventory **GL credit balances** (PostGL), not negative **quantity on hand**.

## Intent-first (always)

Classify before choosing an operation:

| Intent | User signals | Must do | Must not |
|--------|--------------|---------|----------|
| **Aggregation** | how many, count, total, number of | COUNT/SUM; **one number**; no grid | Dump 500+ AR lines |
| **Ranking** | top N, highest, oldest, lowest | TOP + sort; **only N rows** | Full customer master |
| **Reconciliation** | match, reconcile, variance, not matching | SQL both sides; totals first | SDK item sum for inv vs BS |
| **Datafix** | fix, resolve, correct | Diagnostic + **preview only** | Auto-post journals |
| **Listing** | list, show, display (explicit) | Filtered rows; respect limits | Use for “how many” |

Code entry points: `SageIntentEngine` (first stage), `HandlerRegistry`, `RankingQueryPolicy`, `QueryAggregationMode`, `ChatIntentMatcher`, `MegaDigestRouter`, `MegaDigestFallbackMatcher`.

Run intent regression: `dotnet test tests/WizAccountant.Insight.Intents.Tests`

## SQL vs SDK

| Use SQL (connector handlers) | Use SDK |
|------------------------------|---------|
| Analytics, COUNT, reconciliation, audit reads | Posting, saves, approvals |
| InvNum, PostGL, costing views | CustomerTransaction.List for **targeted** open items only |

## If no handler exists

Use **mega digest fallback** — return recognized catalog title + “SQL handler not implemented yet”. Never return only “Try: customer list…”.

## Golden regression queries

See `DOCS/Sage_AI_Training/08_Golden_Test_Cases.md` and implemented ops in `InsightReadOnlyTools.cs`.

## Implementation phases (roadmap)

1. Intent + formatters (**in progress**)
2. SQL handler registry + mega digest (**in progress**)
3. Reconciliation + datafix preview engines
4. NL transaction / voice (future)

Do not break existing handlers when adding new ones. Register ops in `InsightReadOnlyTools`, `SageSdkPhase2Handlers`, and `INSIGHT-CHAT-INTENTS.md`.
