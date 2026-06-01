# Sage AI Agent — Training index (Cursor)

This index links the **Cursor Training Framework** (imported from `Sage_AI_Agent_Cursor_Training_Framework.zip`) to the WizAccountant codebase.

## Training modules (`DOCS/Sage_AI_Training/`)

| File | Topic |
|------|--------|
| `Sage_AI_Agent_Master_Cursor_Prompt.md` | Paste into Cursor; core rules summary |
| `01_Sage_AI_Agent_Training_Framework.md` | Architecture: intent → domain → handler → response |
| `02_Intent_Classification_Rules.md` | Aggregation, ranking, reconciliation, datafix, listing |
| `03_Response_Formatting_Rules.md` | Good vs bad response shapes |
| `04_SQL_Handler_Architecture.md` | SQL vs SDK; handler pipeline |
| `05_Mega_Digest_Fallback_System.md` | Catalog match when SQL missing |
| `06_Sage_Business_Domain_Training.md` | AR, AP, Inventory, GL, MFG, FA |
| `07_Common_AI_Failure_Patterns.md` | Anti-patterns to avoid |
| `08_Golden_Test_Cases.md` | Minimum regression queries |
| `09_Cursor_Training_Prompt.md` | Short Cursor task prompt |
| `10_Implementation_Roadmap.md` | Phases 1–4 |

## Domain knowledge (Sage Evolution 200)

| Document | Use for |
|----------|---------|
| `SAGE-200-DATABASE-LAYERS.md` | Which layer answers which question |
| `Sage_200_Evolution_Database_Handover.md` | PostGL, PostST, costing, FA, manufacturing |
| `Sage_AI_Agent_Knowledge_Pack.md` | Agent rules + table glossary |
| `Sage_AI_Agent_Combined_Knowledge_Pack.md` | Extended pack |

## Feature patches (implemented or spec)

| Patch | Operation / behaviour |
|-------|------------------------|
| Inventory reconciliation | `inventory.gl.reconcile` — SAGE-INVVAL-RECON-CANONICAL-001 |
| Inventory fix workflow | `InventoryFixWorkflow` + sanity checks |
| Negative stock on BS | `inventory.bs.negative_ledgers` |
| Top aged debtors | `customer.aged.top` |
| Count + discounts | `salesinvoice.discount.count` + `QueryAggregationMode` |
| Mega digest 500 | `mega-digest-catalog.json` + `MegaDigestRouter` |
| Aggregation mode | `DOCS/Sage_AI_Agent_Count_Query_Aggregation_Patch.md` |

## Code map (Insight pilot)

| Concern | Primary files |
|---------|----------------|
| Intent engine (confidence + secondary intent) | `src/WizAccountant.Api/Insight/SageIntentEngine.cs` |
| Domain signal scorer | `src/WizAccountant.Api/Insight/SageDomainScorer.cs` |
| Full resolve (handler / mega digest) | `src/WizAccountant.Api/Insight/SageIntentResolver.cs` |
| Ambiguous query tests (35) | `tests/intents/ambiguous-intents.json` |
| Handler registry | `src/WizAccountant.Api/Insight/Data/handler_registry.json` + `HandlerRegistry.cs` |
| Intent classification (facade) | `src/WizAccountant.Api/Insight/SageQueryIntentClassifier.cs` |
| Ranking / TOP policy | `src/WizAccountant.Api/Insight/RankingQueryPolicy.cs` |
| Intent regression tests | `tests/intents/golden-intents.json` + `tests/WizAccountant.Insight.Intents.Tests/` |
| Aggregation | `QueryAggregationMode.cs`, `AggregationReplyFormat.cs` |
| Chat routing | `ChatRoutePlanner.cs`, `BusinessProcessClassifier.cs`, `CompatibilityGate.cs` |
| Query logging / triage | `InsightQueryLogService.cs`, `InsightTriageService.cs` |
| Intent contract | `QueryIntentContract.cs`, `HandlerCapabilityRegistry.cs` |
| Mega digest | `MegaDigestCatalog.cs`, `MegaDigestRouter.cs`, `MegaDigestFallbackMatcher.cs` |
| Sage SQL handlers | `src/WizConnector.Service/Sage/*Handler.cs`, `SageSdkPhase2Handlers.cs` |
| Allowlist | `InsightReadOnlyTools.cs` |

## Self-training loop (merged architecture)

| Document | Use for |
|----------|---------|
| `Sage_AI_Self_Training_Roadmap.md` | Combined 9-layer plan + curriculum A–E + priorities |
| **`Pilot_Stabilization_Workflow.md`** | **SOP: telemetry → triage → promote → sign-off** |
| **`Real_Insight_Queries.md`** | **Permanent real query bank (training fuel)** |
| **`Pilot_Query_Signoff.md`** | **UAT / Production Ready tracking** |
| **`Query_Triage_Priority.md`** | **Critical / High / Medium priority matrix** |
| **`Capability_Gap_Register.md`** | **Open gaps — no random handler growth** |
| `Merge_Review_Cursor_Self_Analysis_vs_Roadmap.md` (Downloads) | Executive merge decision |
| `InsightQueryLogs` + triage API | Production learning from real queries |
| `scripts/export-insight-triage.ps1` | Weekly candidate test export |
| `scripts/weekly-pilot-review.ps1` | Export + weekly checklist |

**API:** `GET /api/insight/triage?days=7` · `POST /api/insight/feedback` · `QueryLogId` on chat response

## Training status (honest)

| Phase | Status |
|-------|--------|
| 1 — Intent + formatters | **Stabilized** — `SageIntentEngine` + confidence; aggregation/TOP guards; 28 regression tests |
| 2 — SQL registry + mega digest | **Partial** — ~10 dedicated handlers; 500 catalog titles matched |
| 3 — Reconciliation + datafix engines | **Partial** — inventory reconcile + fix preview text |
| 4 — NL transactions | **Not started** |

**All 500 digest queries are classified**; **most do not have dedicated SQL yet** — fallback explains intent instead of generic help.

## Regenerate mega digest JSON

```powershell
.\scripts\build-mega-digest-catalog.ps1
```
