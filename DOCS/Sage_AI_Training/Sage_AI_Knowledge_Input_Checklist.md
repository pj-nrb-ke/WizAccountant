# What to provide to enhance Sage AI Agent knowledge

Use this checklist to feed the self-training loop (`InsightQueryLogs` + triage) and handler development.

## Highest leverage (do first)

| Input | Format | Used for |
|-------|--------|----------|
| **Real Insight questions** | Copy/paste 15–30 queries (good or bad) | Candidate tests, routing gaps |
| **“Wrong” feedback** | `POST /api/insight/feedback` with `QueryLogId` + rating `wrong` + one-line note | Triage priority |
| **Expected operation** | e.g. “should be product.monthly.orders.analysis” | Promoting tests to `tests/intents/` |

## Schema & Sage (reduces SQL failures)

| Input | Format | Used for |
|-------|--------|----------|
| **SSMS proof for one failed handler** | Screenshot or working SELECT for your company DB | Column names (`fQtyChange`, `iStockCodeID`, etc.) |
| **Doc type rules** | “Sales = DocType 4 only” | InvNum filters |
| **Order vs invoice policy** | “Ordered means sales order” vs “posted invoice” | Handler evidence notes |

## Business rules (ground truth)

| Input | Format | Used for |
|-------|--------|----------|
| **Definition sheets** | 1 paragraph per term (prompt payer, dead stock, VAT payable) | Confusion guards + MD packs |
| **Priority list** | Top 5 pain areas this month | Roadmap phase C ordering |
| **Sign-off samples** | “This reply is acceptable for pilot” on 3–5 live runs | Acceptance before deploy |

## Optional external content (accelerates, not required)

| Input | Format | Used for |
|-------|--------|----------|
| Training ZIPs / Downloads prompts | MD files | Structured implementation specs |
| `Sage_200_Evolution_Database_Handover.md` updates | PR to `DOCS/` | SQL handler design |
| Consultant workshop notes | Bullet list | Domain curriculum |
| Mega digest gaps | “Users keep asking X but catalog says Y” | New handlers vs digest tuning |

## What we generate without external content

- Curriculum MD packs (phases A–E)
- Intent JSON tests (paraphrases + must-not-route)
- Handler specs, registry entries, Cursor prompts
- Confusion guard matrices from known bugs
- Triage reports from logs (after pilot usage)

## Weekly rhythm (recommended)

1. Run pilot; use Insight normally.  
2. `.\scripts\weekly-pilot-review.ps1` (or `export-insight-triage.ps1`)  
3. Review `tests/intents/candidates/candidate-tests-*.json`  
4. Classify with `DOCS/Query_Triage_Priority.md`; add to `DOCS/Real_Insight_Queries.md`  
5. Approve 3–5 cases → move into `tests/intents/*.json`  
6. Implement 0–1 handler if triage shows repeated gap (see `DOCS/Capability_Gap_Register.md`)  
7. `dotnet test` + deploy with version bump  
8. Update `DOCS/Pilot_Query_Signoff.md` for validated queries  

Full SOP: `DOCS/Pilot_Stabilization_Workflow.md`

No auto-deploy of routes or handlers without human review.
