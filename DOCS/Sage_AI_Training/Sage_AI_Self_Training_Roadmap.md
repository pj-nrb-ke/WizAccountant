# Sage AI Agent — Self-Training Roadmap (merged)

This document merges:

- Cursor self-analysis (curriculum + production learning loop)
- Latest architecture plan (contracts + capability enforcement)
- `Merge_Review_Cursor_Self_Analysis_vs_Roadmap.md` (executive decision)

**Philosophy:** Self-learning means `query → log → triage → candidate tests → human approval → deploy` — not unsupervised model retraining.

**Durable memory:** `tests/intents/*.json`, `handler_registry.json`, confusion guards, capability contracts — not chat history alone.

---

## Combined architecture (9 layers)

| Layer | Status | Purpose |
|-------|--------|---------|
| 1 BusinessProcessClassifier | Implemented | Semantic process before SQL |
| 2 QueryIntentContract | **Foundation** | Parsed domain, metrics, groupings, date filter, output shape |
| 3 HandlerCapability matrix | **Foundation** | What each handler can return |
| 4 Compatibility gate | **Foundation** | Reject route/handler mismatches before Sage job |
| 5 Safe runtime boundary | Partial | Safe chat errors; SQLite DateTimeOffset fixes |
| 6 InsightQueryLog + triage | **Phase 1 (this sprint)** | Production learning loop |
| 7 Candidate test promotion | **Phase 1** | Export JSON test stubs from logs |
| 8 Canonical promotion workflow | Process | Human PR approval only |
| 9 Explainability + investigation depth | Ongoing | WHY, contributors, follow-up context |

---

## Curriculum phases (depth, not random handlers)

### A — Foundations (largely done)
Payment behaviour vs outstanding; VAT vs sales; inventory lifecycle vs lists; reconciliation vs listing; read-only safety.

### B — Investigation chains (partial)
Multi-turn warehouse/item drilldown; `InvestigationContext`; needs workflow tests.

### C — Analytical depth (ongoing)
Product monthly orders (PATCH-009), supplier discipline, close checklist, treasury explain — add per domain with tests.

### D — Explainability standard
Shared envelope: finding, contributors, confidence, drilldown path (`ExplainabilityEnvelope`).

### E — Production learning loop (**immediate**)
1. `InsightQueryLogs` table  
2. Log every Insight ask (route, job outcome, contract snapshot)  
3. User feedback (`helpful` / `wrong`)  
4. Weekly triage report + `scripts/export-insight-triage.ps1`  
5. Promote candidates to `tests/intents/candidates/*.json` after review  

---

## Implementation priority (approved order)

### Immediate
1. InsightQueryLog  
2. Triage loop + export script  
3. Feedback capture API  
4. Candidate test export  

### Next
5. QueryIntentContract (full parser)  
6. HandlerCapability matrix (all canonical handlers)  
7. Compatibility gate (enforce before job)  
8. Output shape validation (post-job JSON check)  
9. Runtime safety hardening  

### Then
10. Deeper explainability  
11. Investigation memory  
12. Month-end close intelligence  
13. Treasury / forecast reasoning  

---

## Metrics to track

- % queries with canonical operation (not mega-digest fallback)  
- % job failures by operation  
- % compatibility blocks (misroute prevented)  
- Feedback: helpful vs wrong by operation  
- Top unmatched queries (triage input for new handlers)  

---

## Code map (self-training)

| Component | Location |
|-----------|----------|
| Query intent contract | `Insight/QueryIntentContract.cs` |
| Handler capabilities | `Insight/HandlerCapabilityRegistry.cs` |
| Compatibility gate | `Insight/CompatibilityGate.cs` |
| Query log | `Insight/InsightQueryLogService.cs`, `InsightQueryLogRecord` |
| Triage + export | `Insight/InsightTriageService.cs`, `scripts/export-insight-triage.ps1` |
| Feedback API | `POST /api/insight/feedback` |
| Triage API | `GET /api/insight/triage` |

---

## Human approval gate (required)

Never auto-deploy:

- New handlers  
- Registry `is_canonical` changes  
- Confusion guard changes  
- Promoted regression tests  

CI must pass; pilot smoke on staging; then production deploy.
