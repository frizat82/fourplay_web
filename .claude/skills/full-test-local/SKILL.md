---
name: full-test-local
description: Full local real-mode test — Docker Postgres + backend in real mode (DEMO_MODE unset), covering infra, auth lifecycle, league/commissioner, failure drills, and postseason config checks. Everything here runs against localhost, needs no shared/staging environment, and is safe to run any time.
---

# Full Local Real-Mode Test

Everything in this checklist is verifiable **locally**, against a Docker Postgres +
backend running with `DEMO_MODE` unset (real mode, not the seeded demo/UAT stack). It
uses real ESPN calls and real Gmail-API email sending (creds already in
`.env.backend`), but no shared infrastructure — nothing here depends on a hosted
staging environment, because **there currently is no such environment**: the `dev`
Railway environment was deliberately converted to a permanent `DEMO_MODE=true` UAT
stack (see `frizat-67l`), so it can't be used for real-mode testing anymore.

This is the gate for a `dev`→`main` cutover as far as anything agent-executable goes.
What's left over — genuine HTTPS-only checks and the live game-week cycle — lives in
`/prod-smoke-test` instead; that skill is much shorter and mostly human-executed.

**Setup:** run the backend locally via Docker Compose with `DEMO_MODE` unset (or
`false`) so it hits real ESPN/odds endpoints and sends real email through the Gmail
API credentials already configured in `.env.backend`. Use a real inbox (a Gmail alias
like `you+test@gmail.com` works) for any step that needs to receive an email.

---

## Phase A — Infra smoke

- [ ] `GET /api/version` responds with a SHA (endpoint present and wired)
- [ ] Boot log shows CORS fail-closed validation passed for the configured origins
- [ ] Admin job monitor lists all expected Quartz triggers with correct next-fire times **in CST** (DST check) — this validates the schedule logic itself; a real deployed environment's *actual* trigger times still need a separate check (see `/prod-smoke-test`)
- [ ] 2026 season config rows exist: `SELECT COUNT(*) FROM "NflSeasonWeekConfigs" WHERE "Season"=2026` → 22; CFB → 18

Not testable here (needs a real HTTPS host): `Secure`/`SameSite` cookie flags, real
cross-origin CORS enforcement — `Program.cs`'s `IsDevelopment()` branch deliberately
relaxes both locally. See `/prod-smoke-test`.

## Phase B — Auth lifecycle

- [ ] Register a brand-new account with a real email → confirmation email arrives (real send via Gmail API) → link confirms
- [ ] Login works; unknown username and wrong password return identical responses (enumeration check)
- [ ] 6 rapid failed logins → rate limited (429), then recovers
- [ ] Leave a tab idle past JWT expiry → refresh rotation silently re-auths (no logout, no loop)
- [ ] Forgot-password → email arrives → reset → **old session's next request is rejected** (token revocation, mon.7)
- [ ] Change-password from a second device → first device is logged out

## Phase C — League + commissioner

- [ ] Admin creates a 2026 league; sets Juice / JuiceDivisional / JuiceConference / WeeklyCost
- [ ] Owner invites a couple of test accounts by email → invite emails arrive → each joins via link
- [ ] Non-member probing a foreign leagueId via API gets 403 (membership guard, mon.6)
- [ ] League switcher chip works at a 390px viewport; a single-league user sees no confusion
- [ ] Commissioner portal accessible to owner, denied to member

## Phase D — Failure drills

Arguably *safer* to run locally than against any shared environment, since nothing
else depends on this Postgres/backend instance.

- [ ] Break the odds/ESPN call (point the base URL at something invalid, or block it) → spread/scores job fails cleanly (logged, no crash), no bad data written; restore. With `DISCORD_ALERT_WEBHOOK_URL` set locally, confirm a Discord alert actually arrives for the failure (`frizat-703.2` — `JobFailureAlertListener`/`DiscordJobFailureNotifier`); a second failure of the same job within 6h should NOT send a second alert.
- [ ] Restart the backend mid-run → Quartz recovers, missed triggers fire per misfire policy, no duplicate rows
- [ ] Stop the local Postgres container → app shows error states, recovers without a restart once Postgres is back, session init doesn't hang

## Phase E — Postseason spot-check

- [ ] `npm run test:e2e:demo` green (postseason pick counts 3/3/2/1 + CFB 18-slate covered by demo suite)
- [ ] 2026 config rows for Thanksgiving (Thu 11:30am CST first game), Christmas, and WC/Div/CC/SB have correct `FirstGameOfWeekStartDatetime` — these drive the spread scheduler (`frizat-pxy`)
- [ ] Juice switches to JuiceDivisional / JuiceConference on the right weeks (verify via demo stack)

---

## Results template

| Phase | Result | Notes / bead |
|---|---|---|
| A Infra | | |
| B Auth | | |
| C League | | |
| D Failure drills | | |
| E Postseason | | |

**Pass = every row ✓.** Once this passes, the remaining gate before a `dev`→`main`
cutover is whatever `/prod-smoke-test` still requires post-merge (deploy-freshness +
HTTPS security checks) — Stage 2 there (the live game-week cycle) is a human-executed
follow-up, not a cutover blocker.
