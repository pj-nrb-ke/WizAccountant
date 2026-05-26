# Sage Evolution SDK — Connection Process (WizAccountant)

This document records how **WizAccountant / WizConnector** connects to **Sage 200 Evolution** on Windows, including setup tooling, SDK registration, runtime dependencies, and troubleshooting discovered during the Phase 1 pilot on a local Sage 11 site.

Use this as the runbook for new developers, new machines, or customer sites.

---

## 1. What we are connecting to

| Layer | Technology |
|--------|------------|
| ERP | Sage 200 Evolution (e.g. v11.x) |
| API | Sage Evolution SDK — `Pastel.Evolution.dll` + `Pastel.Evolution.Common.dll` |
| Database | SQL Server — **common** DB (e.g. `SageCommon11`, `EvolutionCommon`) + **company** DB (e.g. `BlankVer11`) |
| WizAccountant apps | `WizConnector.Setup.exe` (configuration UI), `WizConnector.Service` (Windows worker / connector) |

The SDK is a **.NET Framework, 32-bit** assembly set installed with Evolution, typically under:

`C:\Program Files (x86)\Sage Evolution`

Official SDK downloads and version notes: [Sage Evolution SDK Downloads](https://developerzone.pastel.co.za/index.php?title=Downloads)

**Version rule:** SDK major.minor must match the installed Evolution build (e.g. 11.0.x DLLs with Evolution 11).

---

## 2. Prerequisites

Before connecting:

1. **Sage Evolution** installed and licensed on the machine (or reachable SQL Server with Evolution databases restored).
2. **SQL Server** running; you know server name, SQL login (or Windows auth), company DB name, and common DB name.
3. **SDK developer licence** — serial + authorisation code passed to `DatabaseContext.SetLicense(...)`. Confirm in Evolution: **Help → About** that **SDK Connector** is registered for the site where required.
4. **.NET 8 SDK** to build WizAccountant (Setup app targets `net8.0-windows`, **x86**).
5. **.NET Framework 4.x** on the machine (for `regasm.exe` — see §4).
6. **Administrator PowerShell** once per machine to register/unregister SDK DLLs (see §4).

Optional internal PDFs (in `DOCS/`, not in git):

- `10 - Evolution - Sage Evolution SDK.pdf`
- `11 - Trouble Shooting Connection Problems.pdf`
- `SAGE SDK Help.pdf`

---

## 3. Sage SDK connection sequence (code)

This matches Sage samples and prior working VB integrations:

```vb
' VB example (reference)
DatabaseContext.CreateCommonDBConnection(ConnCOMM)
DatabaseContext.SetLicense("SERIAL", "AUTH_CODE")
DatabaseContext.CreateConnection(ConnSAGE)
```

```csharp
// C# equivalent (WizConnector.Setup / WizConnector.Service)
DatabaseContext.CreateCommonDBConnection(commonConnectionString);
DatabaseContext.SetLicense(licenseSerial, licenseKey);
DatabaseContext.CreateConnection(companyConnectionString);

// Optional — only if Sage agent login is required for reads/writes
if (!string.IsNullOrWhiteSpace(agentPassword))
{
    if (!Agent.Authenticate(agentUser, agentPassword))
        throw new InvalidOperationException("Sage agent authentication failed.");
}
DatabaseContext.CurrentAgent = new Agent(agentUser);
```

**Order matters:** common DB → licence → company DB → (optional) agent.

**Smoke test** used in Setup: `Customer.List("DCLink > 0")` and report row count (pilot site returned e.g. *“Sage OK — 1 customer(s) found.”*).

---

## 4. Register / unregister SDK DLLs (critical on each machine)

### Why registration matters

- **.NET 8 apps** can reference the SDK at compile time, but at runtime Sage validates the **developer licence** against the SDK loaded from the **official install path**, especially after `SetLicense`.
- Copying `Pastel.Evolution.dll` into `bin\Release\...` and using that copy caused errors such as:
  - *Could not load file or assembly 'Pastel.Evolution...'*
  - *The serial number and authorisation code supplied are invalid* (DLL path pointed at the build folder copy).

**Correct approach:** register DLLs under `Program Files (x86)\Sage Evolution` with **32-bit `regasm`**, and load the SDK from that folder at runtime (not from a copied build artefact).

### Script (recommended)

Run **PowerShell as Administrator**:

```powershell
cd C:\Users\pj\WizAccountant\scripts
.\register-sage-sdk.ps1
```

What `scripts\register-sage-sdk.ps1` does:

1. **Unregister** `Pastel.Evolution.dll` / `Pastel.Evolution.Common.dll` from:
   - Old WizConnector build output folders (if anything was registered there during dev)
   - The official Sage Evolution install folder
2. **Register** (only from the install folder), in order:
   - `Pastel.Evolution.Common.dll`
   - `Pastel.Evolution.dll`  
   using: `%WINDIR%\Microsoft.NET\Framework\v4.0.30319\regasm.exe` **`/codebase` `/tlb`**

Use **32-bit** `regasm` (under `Framework\`, not `Framework64\`) — the Sage SDK DLLs are **32-bit**.

Unregister only:

```powershell
.\register-sage-sdk.ps1 -UnregisterOnly
```

Custom Evolution path:

```powershell
.\register-sage-sdk.ps1 -SageSdkPath "D:\Sage Evolution"
```

### COM vs .NET

| Scenario | Registration |
|----------|----------------|
| Delphi / VB6 / Python COM | **regasm required** (type library) |
| WizConnector (.NET 8, direct reference) | Still use **regasm on install folder** on each connector machine for reliable `SetLicense` + consistent DLL path |
| Manual alternative | Sage SDK zip sometimes includes `Install.bat` — same idea as regasm |

---

## 5. WizConnector.Setup.exe — operator workflow

**Executable (Release build):**

`src\WizConnector.Setup\bin\Release\net8.0-windows\WizConnector.Setup.exe`

### UI flow

| Step | Field / action | Purpose |
|------|----------------|--------|
| 1 | **Server host name** | SQL Server instance, e.g. `(local)`, `MYPC\SQLEXPRESS` |
| 2 | **SQL Server user** / **password** | SQL authentication (or tick **Use Windows login**) |
| 3 | **Check** | Tests SQL credentials; lists databases into dropdowns |
| 4 | **Company database** | Sage company DB (e.g. `BlankVer11`) |
| 5 | **Common database** | Sage common DB (e.g. `SageCommon11`; often `EvolutionCommon`) |
| 6 | **Licence serial** / **Licence key** | SDK developer licence for `SetLicense` |
| 7 | **Sage user** / **Sage password** | Optional Evolution agent (password may be blank) |
| 8 | **Test Sage connection** | Runs SDK sequence + sample `Customer.List` |
| 9 | **Save (encrypted)** | Writes config for the connector service |

### Saved configuration

| Store | Path |
|--------|------|
| Encrypted (production) | `%ProgramData%\WizConnector\sage.config` (DPAPI, LocalMachine) |
| Developer secrets | `%APPDATA%\Microsoft\UserSecrets\dotnet-WizConnector.Service-...\secrets.json` |

`WizConnector.Service` loads the encrypted file on startup when present.

---

## 6. .NET 8 / project technical requirements

These were required for a working connection on the pilot machine:

| Requirement | Reason |
|-------------|--------|
| **`PlatformTarget` = `x86`** | Sage SDK assemblies are 32-bit |
| **NuGet `System.Data.SqlClient` (4.8.x)** | SDK uses classic SQL client, not `Microsoft.Data.SqlClient` alone |
| **`System.Configuration.ConfigurationManager`** | Often pulled in by Evolution-related code paths |
| **Do not deploy copied `Pastel.Evolution*.dll`** to app output | Use `Private=false` + post-build delete; load from install path via `SageSdkBootstrap` |
| **`SageSdkBootstrap`** | Adds install folder to PATH / `SetDllDirectory`; `AssemblyResolve` prefers `C:\Program Files (x86)\Sage Evolution` over app directory |
| **Env `WIZ_SAGE_SDK_PATH`** | Override when Evolution is not in the default folder |

**SQL “Check” button** uses `Microsoft.Data.SqlClient` only to list `sys.databases` — separate from the SDK’s SQL client.

**Build:**

```powershell
dotnet build src\WizConnector.Setup\WizConnector.Setup.csproj -c Release
```

After build, confirm **no** `Pastel.Evolution.dll` beside the Setup EXE (only `System.Data.SqlClient.dll` and app DLLs).

---

## 7. Connection strings (SQL)

Built by `SageConnectorConfig` in `WizAccountant.Contracts`:

**SQL authentication:**

```
Data Source={Server};Initial Catalog={Database};User ID={SqlUser};Password={SqlPassword};TrustServerCertificate=True;Encrypt=False
```

**Windows authentication:**

```
Data Source={Server};Initial Catalog={Database};Integrated Security=True;TrustServerCertificate=True;Encrypt=False
```

- **Common** string → `CreateCommonDBConnection`
- **Company** string → `CreateConnection`

---

## 8. Troubleshooting guide (errors we hit)

| Symptom | Cause | Fix |
|---------|--------|-----|
| `Could not load file or assembly 'Pastel.Evolution, Version=11.0.0.0...'` | SDK DLL not beside EXE and not resolved | Run `register-sage-sdk.ps1`; ensure bootstrap loads from Program Files; build x86 |
| `Could not load file or assembly 'System.Data.SqlClient...'` | Missing compatibility package on .NET 8 | Add NuGet `System.Data.SqlClient`; build x86 |
| `serial number and authorisation code supplied are invalid` + DLL path under `bin\Release\...` | Licence checked against **unregistered copy** of SDK | Unregister all copies; register only install folder; enter correct serial/key; verify SDK Connector in Evolution About |
| SQL **Check** fails | Wrong server/login/firewall | Fix SQL fields; confirm SSMS can connect |
| **Test Sage** fails after SQL OK | Wrong common/company DB names; wrong licence; SDK version mismatch | Re-run **Check**; verify DB names in SSMS; match SDK version to Evolution |
| `BadImageFormatException` | 64-bit process loading 32-bit SDK | Set project **x86** |
| Setup UI missing textboxes | WinForms layout bug (empty hint rows) | Fixed in `SetupForm` — use latest `WizConnector.Setup.exe` |

Sage troubleshooting URL (from SDK error text):  
http://dev.pastel.co.za/index.php?title=Registration

---

## 9. Pilot site checklist (successful path)

Use this order on a **new machine**:

- [ ] Evolution installed; SQL databases exist (common + company)
- [ ] Run `.\scripts\register-sage-sdk.ps1` **as Administrator**
- [ ] Build Release `WizConnector.Setup.exe` (x86)
- [ ] Run Setup → **Check** → select company + common DBs
- [ ] Enter licence serial + key (exact values from Sage SDK licence / working sample)
- [ ] **Test Sage connection** → expect green *Sage OK — N customer(s) found*
- [ ] **Save (encrypted)**
- [ ] Start `WizConnector.Service` (loads `%ProgramData%\WizConnector\sage.config`)

---

## 10. Repository map (implementation)

| Item | Location |
|------|----------|
| Setup UI | `src/WizConnector.Setup/SetupForm.cs` |
| SDK bootstrap (Setup) | `src/WizConnector.Setup/SageSdkBootstrap.cs` |
| SQL database lister | `src/WizConnector.Setup/SqlDatabaseLister.cs` |
| Register script | `scripts/register-sage-sdk.ps1` |
| Config model + DPAPI storage | `src/WizAccountant.Contracts/SageConnectorConfig.cs` |
| Connector runtime session | `src/WizConnector.Service/Sage/SageSession.cs` |
| Service startup (load encrypted config) | `src/WizConnector.Service/Program.cs` |
| Short SDK pointer | `lib/sage-sdk/README.md` |

---

## 11. What comes next (after Sage connects)

1. **Save** settings from Setup (if not already done).
2. Run **WizAccountant.Api** and **WizConnector.Service** with pairing configured.
3. End-to-end test: site online → job `Customer.List` from cloud (Phase 1 pilot).

---

## 12. Document history

| Date | Notes |
|------|--------|
| 2026-05-26 | Initial runbook from WizAccountant Phase 1 pilot: Setup app, regasm cycle, x86 + SqlClient, licence validation, successful test on `(local)` / `BlankVer11` / `SageCommon11`. |

---

*For Plan 1 architecture and phased features, see `DOCS/Plan1-Phased-Features.md`.*
