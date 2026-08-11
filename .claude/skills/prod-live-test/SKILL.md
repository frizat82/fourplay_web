---
name: prod-live-test
description: Full live E2E prod-readiness test — real users, real ESPN data, real emails, real job schedule on staging. The gate for dev→main cutover. Run before each season and after any major release.
---

# Live E2E Prod-Readiness Test

This is NOT the mocked Playwright suite or the demo stack. It proves a complete real
game week works on production-like infrastructure with zero admin intervention.
Playwright e2e proves the code works against seeded data; this proves the **system**
works against the real world: ESPN's actual feed, Google's actual SMTP, Railway's
actual runtime, Quartz's actual clock.

**When — two-stage schedule, because most of this does NOT need live games:**

- **Stage 1 (any day, off-season): Phases 0–3, 9, 10.** Infra, auth, league setup,
  failure drills, and postseason spot-checks are date-independent. This stage is the
  gate for the dev→main cutover and can run in early August.
- **Stage 2 (first live week): Phases 4–8.** The app only models regular-season +
  postseason weeks (no NFL preseason support), so the first real spread→pick→score
  cycle can only run against real games. **CFB week 1 (~Aug 29) is the rehearsal**:
  it exercises the full cycle on staging a week+ before NFL kickoff (~Sept 10),
  leaving time to fix what it finds. NFL week 1 then gets a lighter watch-and-verify
  pass on prod itself.

**Where:** Railway staging (frizat-67l) running `DEMO_MODE=false` with real ESPN +
odds APIs, real SMTP, and the Neon dev database. The UAT demo config
(`DEMO_MODE=true`) is a different mode — this test must run in real mode, since
seeded data bypasses exactly the integrations we're proving.

**Recording results:** copy the checklist table (bottom) into the execution bead.
Every ✗ gets its own bead; retest after fix. The test passes only when every row is ✓.

---

## Phase 0 — Prerequisites

- [ ] Staging live at a stable URL, auto-deploying from `dev`, `DEMO_MODE=false`
- [ ] Both hosts resolve: apex (NFL) and `cfb.` subdomain (CFB)
- [ ] 2026 season config rows exist: `SELECT COUNT(*) FROM "NflSeasonWeekConfigs" WHERE "Season"=2026` → 22; CFB → 18
- [ ] `ALLOWED_ORIGINS`, SMTP creds, odds API key set in staging env
- [ ] 5 real test people recruited (real inboxes — Gmail aliases like `you+alice@gmail.com` work)

## Phase 1 — Infra smoke (any day, ~15 min)

- [ ] `GET /api/version` returns the SHA of dev tip (proves deploy freshness — frizat-066)
- [ ] Boot log shows CORS fail-closed validation passed (no `AllowAnyOrigin` outside Development)
- [ ] `curl -H "Origin: https://evil.example" -i <api>/api/auth/me` → no `Access-Control-Allow-Origin` echo
- [ ] Auth cookie is `HttpOnly; Secure; SameSite` on the staging domain
- [ ] Admin job monitor lists all expected Quartz triggers with correct next-fire times **in CST** (DST check)

## Phase 2 — Auth lifecycle (any day, ~30 min)

- [ ] Register a brand-new account with a real email → confirmation email arrives (check spam) → link confirms
- [ ] Login works; unknown username and wrong password return identical responses (enumeration check)
- [ ] 6 rapid failed logins → rate limited (429), then recovers
- [ ] Leave a tab idle past JWT expiry → refresh rotation silently re-auths (no logout, no loop)
- [ ] Forgot-password → email arrives → reset → **old session's next request is rejected** (token revocation, mon.7)
- [ ] Change-password from a second device → first device is logged out

## Phase 3 — League + commissioner (any day, ~30 min)

- [ ] Admin creates a 2026 league; sets Juice / JuiceDivisional / JuiceConference / WeeklyCost
- [ ] Owner invites the 5 testers by email → invite emails arrive → each joins via link
- [ ] Non-member probing a foreign leagueId via API gets 403 (membership guard, mon.6)
- [ ] League switcher chip works on a phone (390px); testers with one league see no confusion
- [ ] Commissioner portal accessible to owner, denied to member

## Phase 4 — Spread cycle (live week, Thu)

- [ ] Spread job fires at its scheduled time unattended (watch admin monitor — do NOT trigger manually)
- [ ] Spreads appear with the league's tease applied; verify one game by hand: ESPN line + Juice = displayed line
- [ ] SpreadRelease countdown before release matched the actual fire time
- [ ] O/U totals present and sane

## Phase 5 — Picks (live week, Thu–Sun)

- [ ] All 5 testers submit the required pick count from their phones
- [ ] Submitted picks show "Locked in"; server rejects unpick of a submitted pick
- [ ] A pick attempted after that game's kickoff is rejected **server-side** (not just disabled UI)
- [ ] Others' picks stay hidden until each game kicks off; own picks always visible
- [ ] Background poll during selection does NOT wipe unsubmitted picks (mon.5, on real latency)

## Phase 6 — Live game day (Sun)

- [ ] Scores jobs fire on schedule (12:30 / 4:30 / 7:40 CST); scores update without manual refresh
- [ ] No full-page spinner flashes during background updates
- [ ] Leaderboard reflects in-progress results mid-day
- [ ] CFB: SSE live push updates scores (cfb. host)

## Phase 7 — Scoring + settlement (Mon–Tue)

- [ ] Final scores match ESPN for every game
- [ ] Hand-verify 3 games against the teased spread: winner/loser/push all correct, including O/U
- [ ] Week winner(s) computed correctly under all-picks-must-win rule; juice totals match WeeklyCost math
- [ ] Prior weeks unchanged (no retroactive mutation)

## Phase 8 — Emails + reminders (live week)

- [ ] MissingPicksJob (once frizat-z5h ships): tester with missing picks gets reminded; tester with full picks does not
- [ ] Reminder deadline copy matches the actual first kickoff, not hardcoded noon
- [ ] All transactional emails render on a phone and don't land in spam

## Phase 9 — Failure drills (staging only, after Phase 7)

- [ ] Break the odds API key → trigger spread job → job fails cleanly (logged, no crash), no bad data written; restore key. (Admin alert email is out of scope until frizat-703.2 ships — add that assertion back once it exists.)
- [ ] Restart the backend service mid-day → Quartz recovers, missed triggers fire per misfire policy, no duplicate score rows
- [ ] Deploy a new build while a tester is mid-session → stale-build banner appears (frizat-066); session survives
- [ ] Brief DB outage (pause Neon compute) → app shows error states, recovers without restart, session init doesn't hang

## Phase 10 — Postseason spot-check (cannot be live-tested off-season)

- [ ] `npm run test:e2e:demo` green (postseason pick counts 3/3/2/1 + CFB 18-slate covered by demo suite)
- [ ] 2026 config rows for Thanksgiving (Thu 11:30am CST first game), Christmas, and WC/Div/CC/SB have correct `FirstGameOfWeekStartDatetime` — these drive the spread scheduler (frizat-pxy)
- [ ] Juice switches to JuiceDivisional / JuiceConference on the right weeks (verify via demo stack)

---

## Results template

| Phase | Result | Notes / bead |
|---|---|---|
| 0 Prereqs | | |
| 1 Infra | | |
| 2 Auth | | |
| 3 League | | |
| 4 Spreads | | |
| 5 Picks | | |
| 6 Game day | | |
| 7 Settlement | | |
| 8 Emails | | |
| 9 Failure drills | | |
| 10 Postseason | | |

**Pass = every row ✓.** Then execute the prod-cutover runbook.
