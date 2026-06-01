# SQL Handler Architecture

## Query Pipeline

User Query
→ Normalize
→ Intent Match
→ Domain Match
→ SQL Handler Match
→ Execute
→ Format Result

## SQL Categories

- AR
- AP
- Inventory
- GL
- Manufacturing
- Fixed Assets
- Audit
- Payroll

## Important Rule

Use SDK for:

- Posting
- Object creation
- Safe transactions

Use SQL for:

- Analytics
- Reconciliation
- Cross-domain reporting