---
name: perf-check
description: Answer "is the app slower?" with measurements, not guesses — server vs client split, the deterministic perf baseline, and a before/after wall-clock A/B of two builds on fast and slow networks. Invoke when someone reports the app feels slow, and before shipping anything on the launch path (index.html, main.tsx, fonts, service worker, bundle splitting, API call chains on cold load).
---

# Perf Check — measure first

The app's perf approach is "measure first": prefer deterministic numbers (bytes, request counts,
request-chain depth, layout shift) that can gate CI, and treat wall-clock time as too noisy to gate
on — use it only for a controlled A/B. `e2e/perf/perf.spec.ts` encodes the deterministic half;
`e2e/perf/compareBuilds.spec.ts` encodes the A/B. This skill strings them together, in order.

A report of "it feels slow" is a hypothesis. Don't ship a fix — or revert one — until a step below
has shown where the time actually went.

## 1. Server or client? (2 min, production data)

Compare Railway's API latency before and after the suspect deploy. If the API didn't move, the
slowdown (if any) is in the client or the user's network.

```
mcp__Railway__http-response-time  projectId=989b2756-1fc0-41c3-8f97-874e58beb81a
                                  serviceId=6ee88fe2-3168-49bb-a982-699424f06c07
                                  hoursBack=72  maxDataPoints=36   [path=/api/auth/me]
```
- Defaults to the **production** environment. `mcp__Railway__list-deployments` gives the deploy time
  to split the series on.
- **p99 = 30000ms buckets are not slow requests** — they're the long-lived SSE streams
  (`/api/cfb/live-stream`). Look at p50/p90, or filter by `path`.
- Normal: overall p50 ≈ 5–17ms; `/api/auth/me` p50 ≈ 1–5ms.

**Testing on `dev.ivleague.xyz`?** The Railway `development` service sleeps when idle — the first
request after a nap is a cold start of several seconds. That is not a regression. Check
`mcp__Railway__list-deployments` for a `SLEEPING` status, or warm it with one request first.

## 2. Deterministic baseline (CI-gated)

```bash
cd Client.React
npm run test:perf          # vite build + perf project via vite preview on :4173
```
Gates, per route (dashboard/picks/scores), against `e2e/perf/baseline.json`:
`jsBytes`/`cssBytes` (≤ +5%), `thirdPartyRequests`, `apiRequests`, `apiChainDepth`, `cls`,
`shiftsAfterReady`. `timeToReadyMs` is printed but never gated (wall clock is noisy).

- Every `/api` call gets 150ms artificial latency, so `apiChainDepth × 150ms` ≈ how long the user
  stares at a skeleton. A deeper chain is the most common real regression.
- Only update the baseline after an **intentional** change, and say why in the PR:
  `PERF_UPDATE_BASELINE=1 npm run test:perf`. Example: self-hosting fonts moved ~7KB of
  `@font-face` CSS into the bundle (cssBytes up) while dropping the only third-party request.
- `PERF_DEBUG=1` prints each API call's start–end, to see the waterfall.

## 3. Wall-clock A/B: before vs after (the "did this make it slower?" answer)

Build the old commit in a throwaway worktree and race it against the current build: same mocked
APIs, iPhone viewport, fast and slow (300ms RTT, 1.5 Mbps) networks, first visit vs repeat visit.

```bash
cd /path/to/repo
git worktree add /tmp/perf-before <old-sha>           # e.g. the commit before the suspect PR
ln -s "$PWD/Client.React/node_modules" /tmp/perf-before/Client.React/node_modules
(cd /tmp/perf-before/Client.React && npx vite build)
(cd Client.React && npx vite build)

cd Client.React
PERF=1 PERF_COMPARE_BEFORE=/tmp/perf-before/Client.React \
  npx playwright test e2e/perf/compareBuilds.spec.ts --project=perf --reporter=line | grep '\[compare\]'

git worktree remove --force /tmp/perf-before
```
Prints the median of 5 runs (`PERF_COMPARE_RUNS` to change) per network and build. Read it as:
- **first visit**: cold launch, no service worker yet. This is a brand-new install.
- **repeat visit**: the service worker is in control. This is every launch of the installed iOS app.

Reference result (2026-10-11, white-screen fix #478 vs the build before it):

| | first visit | repeat visit |
|---|---|---|
| fast, before / after | 1037 / 1043ms | 962 / 954ms |
| slow, before / after | 2725 / 2753ms | 2689 / **1298ms** |

Differences under ~3% are noise. The app-shell service worker halves repeat launches on a weak signal.

## 4. Real device

Simulators can't reproduce iOS's network stack. On the phone: Settings → Developer → Network Link
Conditioner ("Very Bad Network" / "100% Loss"). Connect it to a Mac and open Safari → Develop → the
PWA for the Network tab and the service worker's caches (`ivl-shell-v1`, `ivl-assets-v1`).

## Gotchas this has already cost time on

- **Playwright `setOffline()` doesn't reliably cover a service worker.** A worker that restarts
  mid-test can come back without the emulated-offline flag and quietly fetch from the still-running
  server — `e2e/perf/offlineLaunch.spec.ts` once passed while every cache lookup was missing. To
  test offline, kill the server (that spec runs its own `vite preview` and stops it).
- **Cache lookups vs `Vary: Origin`.** The worker's own `fetch(url)` has no `Origin` header; the
  page's `<script crossorigin>` requests do. `cache.match()` without `{ ignoreVary: true }` misses
  every time. Found by running the offline spec with `--repeat-each=15 --workers=6` — always stress
  a flaky-looking perf/offline test before believing it.
- **Rotating elements:** `boundingBox()` of a spinner mid-turn is wider than its layout size; use
  `offsetWidth` for size, the bounding box only for the centre.
- **Dev server cache-busting:** after an edit Vite requests `/src/main.tsx?t=…`; route patterns
  must allow the query (`/\/src\/main\.tsx(\?|$)/`), or a "block the app" test silently doesn't.
- **Don't `pkill -f "vite preview"` from a shell whose own command line contains that string** —
  it kills the shell. Track the spawned PID (the specs use `detached: true` + `process.kill(-pid)`).
- **Cloud container:** Playwright's bundled headless shell may not match `/opt/pw-browsers`; see
  `/mock-e2e-local` for the browser path and dev-server setup.
