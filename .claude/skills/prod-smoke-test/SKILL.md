---
name: prod-smoke-test
description: Post-deploy checks against a real hosted (HTTPS) environment — the handful of things that genuinely can't be verified locally (Secure/SameSite cookies, real CORS enforcement, deploy-freshness) — plus the live game-week cycle, which is a human-executed checklist bound by real ESPN games and real testers, not something this skill can run for you.
---

# Prod Smoke Test

Almost everything in the old combined checklist is now covered locally by
`/full-test-local` — business logic, auth lifecycle, league/commissioner, failure
drills, and postseason config are all reachable against a local Docker Postgres in
real mode. What's left here is only what genuinely requires a real deployed HTTPS
environment, plus the live game-week cycle (which needs real games and real humans
regardless of where it's hosted, so an agent can check the mechanics but not run it).

**Note on infra:** there is currently no separate "staging" service — `dev` on Railway
is a permanent `DEMO_MODE=true` UAT stack (see `frizat-67l`), and `main`/production is
the only real-mode hosted environment. Run this against production shortly after a
`dev`→`main` merge deploys.

---

## Agent-executable: deploy + HTTPS security checks (~10 min)

- [ ] Both hosts resolve: apex (NFL) and `cfb.` subdomain (CFB)
- [ ] `GET /api/version` returns the SHA of the commit that was just merged/deployed (deployment freshness — `frizat-066`)
- [ ] Boot log shows CORS fail-closed validation passed (no `AllowAnyOrigin`)
- [ ] `curl -H "Origin: https://evil.example" -i <api>/api/auth/me` → no `Access-Control-Allow-Origin` echoed back
- [ ] Auth cookie is `HttpOnly; Secure; SameSite` on the real domain — `Program.cs`'s `IsDevelopment()` branch relaxes both of these locally, so this is the one place they can actually be verified
- [ ] `ALLOWED_ORIGINS` is actually set in this environment's variables (the fail-closed CORS guard depends on it)
- [ ] Admin job monitor lists all expected Quartz triggers with correct next-fire times **in CST** on the real deployed clock/schedule

## Human-only: live game-week cycle

These need real ESPN games happening in real time and real people submitting picks
from their phones — an agent can watch logs and verify data after the fact, but can't
drive this end-to-end on its own. This is the Stage 2 checklist; run it during the
first live week of each season (CFB week ~Aug 29 as the rehearsal, since it exercises
the cycle a week+ before NFL kickoff and leaves time to fix what it finds; NFL week 1
then gets a lighter watch-and-verify pass).

### Spread cycle (Thu)
- [ ] Spread job fires at its scheduled time unattended (watch admin monitor — do NOT trigger manually)
- [ ] Spreads appear with the league's tease applied; verify one game by hand: ESPN line + Juice = displayed line
- [ ] SpreadRelease countdown before release matched the actual fire time
- [ ] O/U totals present and sane

### Picks (Thu–Sun)
- [ ] Testers submit the required pick count from their phones
- [ ] Submitted picks show "Locked in"; server rejects unpick of a submitted pick
- [ ] A pick attempted after that game's kickoff is rejected **server-side** (not just disabled UI)
- [ ] Others' picks stay hidden until each game kicks off; own picks always visible
- [ ] Background poll during selection does NOT wipe unsubmitted picks (mon.5, on real latency)

### Live game day (Sun)
- [ ] Scores jobs fire on schedule (12:30 / 4:30 / 7:40 CST); scores update without manual refresh
- [ ] No full-page spinner flashes during background updates
- [ ] Leaderboard reflects in-progress results mid-day
- [ ] CFB: SSE live push updates scores (`cfb.` host)

### Scoring + settlement (Mon–Tue)
- [ ] Final scores match ESPN for every game
- [ ] Hand-verify 3 games against the teased spread: winner/loser/push all correct, including O/U
- [ ] Week winner(s) computed correctly under all-picks-must-win rule; juice totals match WeeklyCost math
- [ ] Prior weeks unchanged (no retroactive mutation)

### Emails + reminders (live week)
- [ ] MissingPicksJob (once `frizat-z5h` ships): tester with missing picks gets reminded; tester with full picks does not
- [ ] Reminder deadline copy matches the actual first kickoff, not hardcoded noon
- [ ] All transactional emails render on a phone and don't land in spam

---

## Results template

| Check | Result | Notes / bead |
|---|---|---|
| Deploy + HTTPS security | | |
| Spread cycle | | |
| Picks | | |
| Live game day | | |
| Settlement | | |
| Emails | | |

**Pass = every row ✓.**
