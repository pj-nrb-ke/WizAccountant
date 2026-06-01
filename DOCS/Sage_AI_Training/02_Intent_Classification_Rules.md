# Intent Classification Rules

## Aggregation Queries

Keywords:

- how many
- total
- count
- number of
- total invoices

Action:

- Use COUNT/SUM/AVG/MIN/MAX
- Return summarized answer only
- Never dump rows

## Ranking Queries

Keywords:

- top 5
- highest
- lowest
- oldest
- newest
- biggest

Action:

- Use TOP/LIMIT
- Sort appropriately
- Return limited rows only

## Reconciliation Queries

Keywords:

- matching
- not matching
- reconcile
- variance
- difference
- aligned

Action:

- Compare two business datasets
- Return variance analysis
- Suggest drilldown/fix

## Datafix Queries

Keywords:

- fix it
- resolve
- adjust
- correct
- repair

Action:

- Diagnostic mode only
- Preview proposed fix
- Never auto-post

## Listing Queries

Keywords:

- show
- list
- display
- give me

Action:

- Return filtered rows
- Respect row limits