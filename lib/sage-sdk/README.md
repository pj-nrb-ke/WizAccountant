# Sage Evolution SDK (local reference)

WizConnector references the SDK from your **Sage Evolution installation** (not committed to git).

## Installed on this machine

- Path: `C:\Program Files (x86)\Sage Evolution`
- Assemblies: `Pastel.Evolution.dll`, `Pastel.Evolution.Common.dll` (v11.x — match your Evolution build)

## Official downloads

If you need a different SDK version to match a customer site, use the [Sage Evolution SDK Downloads](https://developerzone.pastel.co.za/index.php?title=Downloads) page.

> From Evolution 9+, reference **both** `Pastel.Evolution.dll` and `Pastel.Evolution.Common.dll`.  
> SDK major.minor must match the installed Evolution version (e.g. 11.0.x with Sage 200 11.x).

## Configure connector

Set in `src/WizConnector.Service/appsettings.Development.json` or User Secrets:

```json
"Sage": {
  "CompanyConnectionString": "Data Source=SERVER;Initial Catalog=CompanyDB;User ID=...;Password=...",
  "CommonConnectionString": "Data Source=SERVER;Initial Catalog=EvolutionCommon;...",
  "LicenseSerial": "YOUR-SERIAL",
  "LicenseKey": "YOUR-KEY",
  "AgentUser": "Admin",
  "AgentPassword": "..."
}
```

Override install path in `WizConnector.Service.csproj` via MSBuild property `SageSdkPath` if needed.

## Full connection runbook

See **`DOCS/SAGE-Connection-Process.md`** — setup UI, regasm unregister/register, x86 build, SqlClient, troubleshooting, and pilot checklist.
