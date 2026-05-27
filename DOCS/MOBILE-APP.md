# WizAccountant mobile app (Phases 1–3)

Expo/React Native app at `mobile/wiz-accountant/`. It uses the same pilot API as the web UIs.

## What it covers

| Phase | Mobile feature |
|-------|----------------|
| **P1** | Login (JWT stub), site list, online/offline, Sage reads via `run-wait` |
| **P2** | Dashboard KPIs, AR/AP lists, read-only AI chat |
| **P3** | Approval inbox — approve/reject (Approver/Admin roles) |

## Prerequisites

1. **WizAccountant.Api** running at `http://localhost:5278` (Admin → **Start connector on this PC** or your usual pilot start).
2. Connector paired and site **online** for Sage jobs.
3. For **writes**: `Connector:WritesEnabled=true`, tray **Allow cloud posts**, and proposals created in Act web (or API).

## Start the app (development)

```powershell
cd mobile\wiz-accountant
npm install
npx expo start
```

- Press **a** for Android emulator, **i** for iOS simulator, or scan the QR code with **Expo Go** on a physical phone.

### API URL by device

| Device | Default API URL in app |
|--------|-------------------------|
| Android emulator | `http://10.0.2.2:5278` |
| iOS simulator | `http://localhost:5278` |
| Physical phone | Your PC’s LAN IP, e.g. `http://192.168.1.10:5278` |

Set the URL on the login screen or under **More → Settings**. The API must listen on all interfaces for phones (`launchSettings` / `applicationUrl` includes `0.0.0.0` or your LAN IP).

CORS for mobile is enabled in **Development** only (`MobileDev` policy in `Program.cs`).

## Pilot accounts

| Email | Role | Mobile use |
|-------|------|------------|
| `preparer@pilot.local` | Preparer | Reads + chat; propose on web Act |
| `approver@pilot.local` | Approver | Approve/reject in **Approve** tab |
| `admin@pilot.local` | Admin | Full access |

Password: `pilot`

## Flow

1. Sign in → **Sites** → pick a site.
2. **Home** — refresh dashboard KPIs.
3. **AR** / **AP** — customer/supplier lists and open items.
4. **Approve** — pending proposals (status `Pending`); confirm to post to Sage.
5. **AI** — read-only assistant (same guardrails as Insight web).

## Out of scope (later phases)

- Push notifications (FCM/APNs)
- Offline mode
- Production JWT / refresh tokens
- App Store / Play Store builds (use EAS Build when ready)

## Troubleshooting

- **Cannot reach API** — check URL, firewall, and that the API is running.
- **Job timeout** — connector offline or Sage not configured; use Admin test connection.
- **Approve fails** — writes disabled, no tray consent, or preparer role (switch to approver account).
