# Mega Digest Fallback System

## Purpose

If SQL handler is missing:

- Do NOT return generic help
- Match against business catalog
- Return closest intent

## Required Response

Recognized business intent:
[Catalog Title]

Domain:
[Domain]

Status:
SQL handler not implemented yet

## Matching Priority

1. Exact match
2. Keyword overlap
3. Semantic similarity
4. Domain fallback