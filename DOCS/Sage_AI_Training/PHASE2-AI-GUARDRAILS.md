# Phase 2 AI guardrails

## Allowlist

Only operations in `InsightReadOnlyTools.Allowed` may be invoked from the Insight chat assistant. No write/post handlers.

## System behaviour

- The assistant **never claims** to have posted to Sage.
- For write requests, reply: use Phase 3 approval workflow.
- Every answer includes **data as of** job completion time when a tool runs.

## Logging policy

- Conversation text is stored in SQLite per tenant/site for UX history.
- Do not send Sage PII to external LLM providers until a DPA is in place.
- Phase 2 ships a **local intent router** (no external API key required). Swap in OpenAI/Azure with the same tool allowlist later.

## Tools endpoint

`GET /api/v1/insight/tools` — discoverable list for clients and future LLM tool definitions.
