# WizAccountant PM Check — Master Prompt
> This file is read and executed by the scheduled PM task at 2PM and 8PM EAT every weekday.
> Keep PJ's standing instruction: responses must be very concise. Save tokens.

---

## CONTEXT

- **Project:** WizAccountant / WizGate
- **Developer:** Varun (varun@wizag.biz)
- **Client:** PJ (pj@wizag.biz)
- **Repo:** `pj-nrb-ke/WizAccountant` (push via `git push git@github.com-pj-nrb-ke:pj-nrb-ke/WizAccountant.git main`)
- **Brevo API key:** read from `C:\Users\pj\.pm-secrets.env` — line `BREVO_API_KEY=...`
- **Verified sender:** `info@emailnotifications.co.ke`
- **Brevo endpoint:** `POST https://api.brevo.com/v3/smtp/email`

---

## STEP 1 — DETERMINE WINDOW

Get current local time. EAT = UTC+3.

| Time range (EAT) | Window | Stop time |
|-----------------|--------|-----------|
| 12:00 – 18:00 | AFTERNOON | 18:00 EAT |
| 20:00 – 22:00 | EVENING | 22:00 EAT |
| Any other time | OUT OF WINDOW | Stop — do nothing |

If outside both windows → stop immediately.

---

## STEP 2 — READ STATE

Read `C:\Users\pj\WizAccountant\progress\.pm-state.json`

If the file does not exist, create it:
```json
{"window_id":"","emails_sent":0,"last_email_at":null,"comment_received":false}
```

Build today's `window_id` = `YYYY-MM-DD-[AFTERNOON|EVENING]`

If state file shows `comment_received: true` AND `window_id` matches today → stop (already processed this window).

---

## STEP 3 — FIND TODAY'S GITHUB ISSUE

```
gh issue list --repo pj-nrb-ke/WizAccountant --label progress-report --state open --limit 1 --json number,title
```

Save the issue number. If no open issue exists, create one:
```
gh issue create --repo pj-nrb-ke/WizAccountant --title "[Daily Report] {TODAY}" --label progress-report --body "..."
```
(Body: current task board snapshot from TRACKING.md + comment format instructions.)

---

## STEP 4 — CHECK FOR VARUN'S COMMENT

```
gh issue view {NUMBER} --repo pj-nrb-ke/WizAccountant --comments
```

A valid Varun comment contains at least one of: `TASKS WORKED ON:` / `COMPLETED:` / `IN PROGRESS:`

**Also check for QA results comments** (contain `INTEGRITY: sha256:`) — process these too (Step 6).

---

## STEP 5A — COMMENT FOUND → PROCESS IT

### 5A-1. Parse the comment
Extract: `TASKS WORKED ON`, `COMPLETED`, `IN PROGRESS`, `HOURS USED`, `BLOCKERS`, `NEXT SESSION`

### 5A-2. Update TRACKING.md
File: `C:\Users\pj\WizAccountant\progress\TRACKING.md`

- Set task statuses based on comment
- For each task mentioned: if first time mentioned → record today as `Clock Started`
- Add hours to `AI Hrs Used` running total per task
- Compute `Delay Status` per task (see working hours rules below)
- Update Progress Summary (counts + hours consumed/remaining)
- Add session log row

### 5A-3. Check billing overruns
For each active task: if `AI Hrs Used > (Est Hours × 1.2)` → billing overrun

**Send billing overrun email** (Varun + CC PJ):
```
Subject: [WizAccountant] Payment Notice — Task #{N} Hours Overrun

Task #{N} — {name}
  Approved estimate:  {est} AI hrs
  Hours logged:       {used} hrs
  Overrun:            {pct}% (threshold: 120%)

Payment for the overrun portion ({overrun_hrs} hrs) is subject to PJ's explicit approval.
Your base estimate ({est} hrs) is approved regardless.

Please comment on the GitHub issue explaining why extra hours were needed.

WizAccountant PM System
```

**Send approval request to PJ** (separate email):
```
Subject: [ACTION REQUIRED] Billing Overrun — Task #{N} needs your approval

Task #{N} has a {pct}% overrun. See Varun's explanation (if any) on GitHub Issue #{issue}.

To approve: comment on the issue: BILLING APPROVED #{N}
To reject:  comment on the issue: BILLING REJECTED #{N}

I will update the billing tracker and notify Varun automatically.

WizAccountant PM System
```

Update task `billing_status` in TRACKING.md and pm-data.json → `Pending PJ Approval`

### 5A-4. Check for completed tasks
For each task Varun declared `COMPLETED`:
- Set status → `Pending QA` in TRACKING.md
- Post reply on GitHub issue:
  ```
  Task #{N} declared complete. Next step:
  1. Open QA/checklists/QA-TASK-{N:02d}.md in your Claude/Cursor session
  2. Run every test and fill in PASS/FAIL
  3. Follow the FINAL STEP to post results directly to GitHub
  
  Task will be signed off only after PM review of QA results.
  ```

### 5A-5. Check for QA results (files or comments with INTEGRITY hash)
```
gh api repos/pj-nrb-ke/WizAccountant/contents/QA/results 2>/dev/null
```

For each QA result comment or file found for a task in `Pending QA` status:

**Verify hash:**
1. Extract `INTEGRITY: sha256:<hash>` line
2. Re-read the full comment/file content (minus the INTEGRITY line)
3. Compute SHA-256 of that content using PowerShell:
   `[System.BitConverter]::ToString([System.Security.Cryptography.SHA256]::Create().ComputeHash([System.Text.Encoding]::UTF8.GetBytes($content))).Replace("-","").ToLower()`
4. Compare computed hash to posted hash
5. If MISMATCH → **auto-reject** + email PJ flagging potential manipulation. Do NOT sign off.

**Score the results:**
- Count items marked `[PASS]` and `[FAIL...]`
- Count CRITICAL items (lines containing `**CRITICAL**`)
- Pass conditions: ALL critical = PASS **and** overall ≥ 80% pass rate

**If PASS:**
- Task status → `✅ Completed`
- Record completion date, stop billing clock
- Post on issue: "Task #{N} signed off ✅. Well done Varun."
- Email PJ: "Task #{N} — {name} — signed off. {used} AI hrs consumed. Billing: {billing_status}."

**If FAIL:**
- Task status → `🔴 Needs Rework`
- Post on issue listing specific failures and what to fix

### 5A-6. Update pm-data.json
Update all task statuses, hours, billing, burndown (append today's data point if not already present).

### 5A-7. Regenerate Excel dashboard
```
py -3 C:\Users\pj\WizAccountant\progress\generate_dashboard.py
```

### 5A-8. Commit and push
```
git -C C:\Users\pj\WizAccountant add progress/TRACKING.md progress/pm-data.json progress/WizAccountant-Dashboard.xlsx
git -C C:\Users\pj\WizAccountant commit -m "pm: tracking update {DATE} {WINDOW}"
git -C C:\Users\pj\WizAccountant push git@github.com-pj-nrb-ke:pj-nrb-ke/WizAccountant.git main
```

### 5A-9. Post confirmation on GitHub issue
```
gh issue comment {NUMBER} --repo pj-nrb-ke/WizAccountant --body "**PM ✅ {TIME} EAT**
Received | Dashboard updated | [View TRACKING.md](https://github.com/pj-nrb-ke/WizAccountant/blob/main/progress/TRACKING.md)"
```

### 5A-10. Update state and notify PJ
Update `.pm-state.json` → `comment_received: true`

Report to PJ (3 lines max):
```
DONE: {what was completed or "Nothing completed — tasks in progress"}
NEXT: {Varun's next session plan}
BLOCKERS: {any blockers or "None"}
```

---

## STEP 5B — NO COMMENT → CHASE

### Determine escalation level
Read `emails_sent` from `.pm-state.json`.

| emails_sent | Tone | Subject |
|-------------|------|---------|
| 0 | Friendly | `[WizAccountant] Progress update due — please respond` |
| 1–2 | Direct | `[REMINDER #{n}] Progress update overdue — WizAccountant` |
| 3+ | Urgent | `[URGENT] No update received — PJ is watching — WizAccountant` |

### Compose email body
```
Hi Varun,

{greeting based on tone}

Please add your progress comment to today's GitHub issue NOW:
https://github.com/pj-nrb-ke/WizAccountant/issues/{NUMBER}

Use this format:
  TASKS WORKED ON: #X, #Y
  COMPLETED: #X — description
  IN PROGRESS: #Y — what you did, what remains
  HOURS USED: X hrs
  BLOCKERS: None / description
  NEXT SESSION: plan

────────────────────────────────
BILLING & TIME TRACKING
────────────────────────────────
{For each active task (status = In Progress or Blocked):}

  Task #{id} — {name}
    Clock started:    {clock_started} EAT
    Estimate:         {est_hours} AI hrs → due by {clock_started + est_hours working hrs}
    AI hrs logged:    {hours_used} hrs
    Working hrs elapsed: {compute from clock_started to now, Mon–Sat 8AM–6PM EAT}
    Overrun:          {pct}% {⚠️ if > 120%}
    Billing status:   {billing_status}
────────────────────────────────

{urgent escalation text if emails_sent >= 3}

WizAccountant PM System
Reply-To: pj@wizag.biz
```

### Send via Brevo API (PowerShell)
```powershell
# Read API key from local secrets file (never commit this file)
$brevoKey = (Get-Content "C:\Users\pj\.pm-secrets.env" | Where-Object { $_ -match "^BREVO_API_KEY=" }) -replace "^BREVO_API_KEY=",""

$payload = @{
    sender   = @{ name = "WizAccountant PM"; email = "info@emailnotifications.co.ke" }
    to       = @(@{ email = "varun@wizag.biz"; name = "Varun" })
    replyTo  = @{ email = "pj@wizag.biz"; name = "PJ" }
    cc       = @(@{ email = "pj@wizag.biz"; name = "PJ" })
    subject  = "{subject}"
    htmlContent = "{body_html}"
} | ConvertTo-Json -Depth 5

Invoke-RestMethod -Uri "https://api.brevo.com/v3/smtp/email" -Method POST `
    -Headers @{ "api-key" = $brevoKey; "Content-Type" = "application/json" } `
    -Body $payload
```

### Update state
```json
{ "emails_sent": {n+1}, "last_email_at": "{now}", "comment_received": false, "window_id": "{today-window}" }
```

### Self-reschedule (if before stop time)
Check current time vs stop time for this window.

If BEFORE stop time → create new scheduled task 10 minutes from now:
```
mcp__scheduled-tasks__create_scheduled_task:
  taskId: wizaccountant-pm-chase-{timestamp}
  description: PM chase loop — check for Varun comment
  fireAt: {now + 10 minutes in ISO format with +03:00}
  prompt: "Read the file C:\Users\pj\WizAccountant\progress\PM-CHECK-PROMPT.md and follow its instructions exactly. This is an automated PM check."
```

If AT or PAST stop time → stop. Report to PJ:
"PM {WINDOW} check window closed at {stop_time}. No comment received from Varun today. Chase resumes at next scheduled window."

---

## WORKING HOURS CALCULATOR

**Working hours:** Mon–Sat, 08:00–18:00 EAT (10 hrs/day)
**Sunday:** Off
**Kenya Public Holidays 2026:**
- 2026-01-01 (New Year), 2026-04-03 (Good Friday), 2026-04-06 (Easter Mon)
- 2026-05-01 (Labour Day), 2026-06-01 (Madaraka), 2026-06-06 (Eid al-Adha est.)
- 2026-10-10 (Huduma), 2026-10-20 (Mashujaa)
- 2026-12-12 (Jamhuri), 2026-12-25 (Christmas), 2026-12-26 (Boxing Day)

**To compute working hours elapsed between date A and now:**
1. List all calendar days from A to today
2. Remove Sundays and public holidays
3. For each remaining day: count overlap with 08:00–18:00 window
4. Sum total hours

**Delay Status rules:**
- `On Track` — hours used ≤ estimated hours
- `At Risk` — hours used > 80% of estimate but ≤ estimate
- `Overdue` — hours used > estimated hours

---

## BILLING APPROVAL CHECKER

On every PM check, also scan the current issue comments for:
- `BILLING APPROVED #N` — set task N billing_status → `Approved` in TRACKING.md + pm-data.json. Email Varun: "PJ has approved the overrun hours on Task #{N}. Full hours will be billed."
- `BILLING REJECTED #N` — set task N billing_status → `Rejected`. Email Varun: "PJ has rejected the overrun hours on Task #{N}. Only the base estimate ({est} hrs) will be billed."

---

## IMPORTANT RULES

1. Always send from `info@emailnotifications.co.ke` — never from `noreply@ascendbooks.biz`
2. PJ is always CC'd on chase emails and billing notices
3. Git push always uses: `git push git@github.com-pj-nrb-ke:pj-nrb-ke/WizAccountant.git main`
4. Keep all responses and reports concise — PJ's standing instruction: save tokens
5. Task billing clock starts when Varun FIRST mentions that task number in any comment
6. QA sign-off required before any task counts as Completed for billing
