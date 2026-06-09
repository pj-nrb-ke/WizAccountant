# WizAccountant Load Tests (B5-E)

k6-based performance baseline scripts.

## Prerequisites

Install k6: https://k6.io/docs/get-started/installation/

```bash
# Windows (winget)
winget install k6 --source winget

# macOS
brew install k6
```

## Scripts

| Script | Target | SLO |
|--------|--------|-----|
| `insight-chat-baseline.js` | 50 VUs, Insight `/ask` | p95 < 2 000 ms |
| `auth-and-api-baseline.js` | 30 VUs, auth + RBAC | p95 < 500 ms |

## Run

```bash
# Start the API locally first
dotnet run --project src/WizAccountant.Api/

# Run Insight chat baseline
k6 run tests/load/insight-chat-baseline.js \
  -e WIZ_BASE_URL=http://localhost:5000 \
  -e WIZ_JWT=<paste-bearer-token> \
  -e WIZ_SITE_ID=<paste-site-guid>

# Run auth/API baseline (no JWT needed — tests 401 paths)
k6 run tests/load/auth-and-api-baseline.js \
  -e WIZ_BASE_URL=http://localhost:5000
```

Results are written to `tests/load/results/` as JSON.

## CI Integration

The GitHub Actions CI workflow can run these as part of a nightly performance job.
Add to `.github/workflows/ci.yml` under a `perf` job (requires the API to be running).
