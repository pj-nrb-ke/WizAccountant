# WizCRM Enterprise QA Enforcement Instructions

## Review of Current QA Cycle

The current QA cycle showed technical capability but insufficient testing depth.

The agent successfully demonstrated:
- Playwright execution
- frontend synchronization checks
- basic assertion validation
- structured Excel reporting
- API/UI verification

However, the QA execution remains far too conservative for enterprise-grade validation.

The agent is still:
- minimizing scope
- skipping expensive/destructive tests
- avoiding concurrency-heavy testing
- avoiding long-duration testing
- avoiding operational chaos testing

This is NOT acceptable for enterprise production readiness.

## Biggest Current Problem

The QA agent is still behaving like:
"an automated smoke test runner"

instead of:
"a destructive enterprise chaos QA engineer"

Empty QA sheets are considered QA failure.

## Mandatory Test Counts

### Duplicate Prevention Tests
Minimum: 25 tests

Test:
- double-click save
- triple-click save
- refresh during save
- duplicate uploads
- retry after timeout
- multi-tab saves

Validate:
- no duplicate records
- no duplicate counters
- no duplicate processing

### Race Condition Tests
Minimum: 20 tests

Test:
- simultaneous saves
- delayed APIs
- rapid navigation
- modal spam
- slow network

Validate:
- no stale state
- no phantom records
- no spinner deadlocks

### Session Recovery Tests
Minimum: 20 tests

Test:
- token expiry
- refresh during save
- disconnect/reconnect
- logout during workflow

Validate:
- graceful recovery
- no corruption
- clear messaging

### Multi-Tab Tests
Minimum: 15 tests

Test:
- same record multiple tabs
- stale submit
- logout in one tab/use another

Validate:
- conflict handling
- no silent overwrite

### Long-Duration Stability Tests
Minimum: 10 tests

Run:
- 60-minute simulation
OR
- 500+ user actions

Validate:
- memory stability
- no UI slowdown
- no stale state buildup

## Mandatory Evidence Rules

Every failed test MUST include:
- screenshot
- trace
- console log
- network log
- reproduction steps

Missing evidence = invalid test.

## Mandatory Assertion Validation

BAD TEST:
- button clicked
- browser survived
- PASS

GOOD TEST:
- verify backend updated
- verify frontend updated
- verify counters updated
- verify no duplicates
- verify no stale cache
- verify no console errors
- THEN PASS

## Failure Expectation Rule

If destructive testing finds very few failures:
ASSUME TESTING DEPTH IS INSUFFICIENT.

Continue deeper testing.

## Anti-Stuck Rules

Apply:
- single test timeout: 30s
- workflow timeout: 2m
- batch timeout: 15m

If stuck:
1. capture evidence
2. mark FAIL/BLOCKED
3. continue immediately

Spinner >15s:
mark Spinner Deadlock.

API pending >20s:
mark Hanging API.

## Required QA Report

Create:

QA-Test-003.xlsx

Required sheets:
- Test Summary
- Duplicate Prevention Tests
- Race Condition Tests
- Session Recovery Tests
- Multi-Tab Tests
- Long-Duration Stability
- Frontend Sync Tests
- UX Findings
- Evidence Index
- Critical Issues
- Recommended Fixes

## Final Philosophy

The objective is NOT:
"Does WizCRM basically work?"

The objective IS:
"Can WizCRM survive real-world enterprise abuse without corruption, stale state, duplicate data, or operational instability?"

Play a chime sound once QA completes.
