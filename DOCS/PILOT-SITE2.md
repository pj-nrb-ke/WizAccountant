# Pilot site #2 (P1-29)

Validates read handlers on a second Evolution deployment or a second paired site.

## Version matrix (fill in when second site is available)

| Site | Company DB | Evolution build | Connector device |
|------|------------|-----------------|------------------|
| Pilot #1 | BlankVer11 | 11.x (pilot) | PILOT-PC-01 |
| Pilot #2 | _TBD_ | _TBD_ | PILOT-SITE2-01 |

## Automated script

From repo root (API + connector running):

```powershell
.\scripts\run-pilot-e2e-site2.ps1
```

Runs `site.health`, `customer.list`, `customertransaction.list`, `supplier.list`, and `suppliertransaction.list` via `POST /api/jobs/run-wait`.

## Auth stub (P1-22)

Dev login: `admin@pilot.local` / `pilot` → `POST /api/auth/login`
