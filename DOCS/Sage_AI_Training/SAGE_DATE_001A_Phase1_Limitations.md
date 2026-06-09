# SAGE-DATE-001A — Phase 1 Period Parser Limitations

Implemented: centralized `InsightDateRangeParser` with calendar-year semantics and UTC-today relative anchors.

## Phase 1 includes

- Calendar quarters (Q1–Q4), contiguous quarter ranges (Q3–Q4)
- Non-contiguous quarters (Q1 & Q3) — **Model C**: only handlers with `SupportsSegmentedPeriods` execute; others return a safe message
- Half-year (H1 / H2)
- Month ranges, from-month-onward
- Relative: YTD, MTD, QTD, this quarter, last quarter, last N months
- Cross-domain parameter wiring via `InsightChatPeriodHelper` after route selection
- Snapshot handlers (aged debt, open items, bank recon) excluded from range parsing

## Phase 2 (not implemented)

- Sage / tenant **fiscal-year** calendars
- Fiscal YTD / fiscal quarters
- Site-local timezone for “today”
- Multi-segment execution on all ranking handlers (only product monthly supports segments in Phase 1)

## Reference date

All relative periods use **`DateTime.UtcNow.Date`**.

## SQL Query tab

Ad-hoc SQL remains the escape hatch for complex multi-period analysis on unsupported handlers.
