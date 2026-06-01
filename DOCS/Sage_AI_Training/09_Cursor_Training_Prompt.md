# Cursor Training Prompt

Use this prompt in Cursor.

```text
Read all MD training files carefully.

Your role is to upgrade the Sage AI Agent into a finance-aware business analytical assistant.

Important:
1. Do not behave like a generic SDK wrapper.
2. Always classify intent first.
3. Detect:
   - count queries
   - top/ranking queries
   - reconciliation queries
   - datafix queries
   - listing queries
4. Respect response formatting rules.
5. Never dump rows for aggregation queries.
6. Never auto-post fixes.
7. Use Mega Digest fallback matching if SQL handler is missing.
8. Use SQL for analytics/reconciliation.
9. Use SDK for posting transactions only.

Implement the training incrementally without breaking existing handlers.

After implementation:
- run regression tests
- validate golden test cases
- report unsupported handlers clearly

When task is done, play a chime sound.
```