# Local pilot — single source of truth

Use this guide only when testing **on your PC** with Sage. Ignore production (`app.ascendbooks.biz`) until local works.

**Tool:** `WizPilot.exe`  
**Path:** `C:\Users\pj\WizAccountant\src\WizAccountant.Manager\bin\Release\net8.0-windows\WizPilot.exe`

---

## Before you start (once per PC)

- Sage Evolution installed and licensed on this machine
- .NET 8 SDK installed (`dotnet --version` works)
- Repo at `C:\Users\pj\WizAccountant`

---

## Every local test session (follow in order)

### Step 1 — Open WizPilot

Double-click **WizPilot.exe** (pin a shortcut if you like).

### Step 2 — Set URLs for local only

| Field | Value |
|-------|--------|
| **Connector API URL** | `http://localhost:5278` |
| **Production web URL** | leave as-is (you will not use production buttons today) |

Click **Save URLs**.

### Step 3 — Build (first time only, or after code updates)

Click **Build pilot apps**.

- A **new PowerShell window** opens and builds Service, Tray, and Setup (1–2 minutes).
- When it says **“Build complete”**, press Enter to close that window and continue here.
- If WizPilot says **“Pilot apps already built”**, you can **skip** this step and go to step 4.

### Step 4 — Configure Sage (first time only)

Click **Open Sage setup** → complete database / SDK settings → save.

### Step 5 — Start the local cloud API (do this before Admin)

Click **Restart local API** (stops any old API still on port 5278, then starts fresh).

- A **new console window** opens — **leave it open** (this is the API on port 5278).
- Wait until you see **“Now listening on: http://localhost:5278”** (about 30–60 seconds).
- In Insight, the header should show **Chat 2026-06-09-consolidation** (yellow = stale API — run Restart again).
- Consolidation smoke queries: `DOCS/TEST-CONSOLIDATION-LOCAL.md`
- Alternative: run `scripts\restart-local-api.ps1` from the repo if WizPilot is old.
- If that window closes immediately or shows red errors, tell support — Admin will not work until this step succeeds.

### Step 6 — Start connector + tray

Click **Start service + tray**.

- Look for the **WizConnector** icon near the clock (system tray).
- If no icon: click **Start system tray** again.

### Step 7 — Create a pairing code

Under **Open in browser — local**, click **Admin**.

In Admin:

1. **Create pairing code** — enter a site name (e.g. `My Sage PC`) → **Generate code**
2. Copy the code (e.g. `WZ123456`)

### Step 8 — Pair from the tray

Right-click the **tray icon** → **Pair with code…** → paste the code → OK.

### Step 9 — Confirm site is online

In **Admin** (local), click **Refresh sites**.

- Your site should show **online** (●).

### Step 10 — Use Insight

Under **Open in browser — local**, click **Insight**.

1. Pick your site in the **Site** dropdown (not “No online sites”)
2. Click **Refresh KPIs** on the Dashboard tab

---

## You are done when

- Admin shows your site **online**
- Insight dashboard returns data (or a clear Sage error you can fix in setup)

---

## If something fails

| Problem | Fix |
|---------|-----|
| Browser “can't reach localhost:5278” | Click **Start local API** first and wait for “Now listening on http://localhost:5278”. Leave that window open, then click **Admin** again. |
| Many black PowerShell windows | Each **Start local API** / **Start service + tray** click opens a new window. Close all black PowerShell windows, then start **once** from WizPilot. Only **one** API window and **one** connector window should stay open. |
| WizPilot frozen on Build | Close WizPilot (End Task). Reopen from the path above — Build now opens a separate window and should not freeze. If apps were built before, skip Build. |
| “No online sites” in Insight | Repeat steps 6–9; connector must be running and paired |
| Insight shows `ADP_ConnectionRequired_Fill` | Open **Open Sage setup** → confirm **common** and **company** DB are selected → **Test Sage connection** (must say “Sage OK”) → **Save** → restart **Start service + tray** once |
| Tray icon missing | **Start system tray** again |
| API errors | Ensure **Start local API** window is still open |
| Port in use | Close old API windows; restart from step 5 |

---

## Do not use for local testing

- Do **not** change Connector API URL to `https://app.ascendbooks.biz` yet
- Do **not** use **Open in browser — production** yet
- Do **not** click **Deploy to cloud** or **QA cycle 003** for daily work

---

## When local works — later (production)

Only after steps 1–10 succeed locally:

1. Change Connector API URL to `https://app.ascendbooks.biz` → **Save URLs**
2. **Admin** (production) → new pairing code
3. **Start service + tray** → tray **Pair with code**
4. **Insight** (production)

That is a **separate** checklist — not part of local testing.

---

*Last updated: local pilot via WizPilot — WizAccountant*
