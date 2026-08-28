---
name: mock-e2e-local
description: Run the mock-based Playwright e2e suite (e2e/*.spec.ts, excl. demo/) locally instead of pushing blind and waiting on CI. Covers the webServer gotcha, exact env vars, and how to read failures.
---

# Mock E2E — Run Locally

This is the `page.route()`-mocked Playwright suite (`Client.React/e2e/*.spec.ts`,
excluding `e2e/demo/`) — no backend needed, everything is intercepted. This is
**different from `/demo-stack`**, which runs against a real `DEMO_MODE=true` backend.
Use this skill for the mock suite; use `/demo-stack` for integration tests.

**Always run this locally before pushing, the same way `/demo-stack` says to for the
integration suite** — don't push blind and wait for CI. A missing/stale route mock
(e.g. a new endpoint the frontend now calls unconditionally) breaks the *entire* suite
with `ECONNREFUSED` on every spec, not just the ones that touch it, since one Vite dev
server backs all of them.

## The webServer gotcha

`playwright.config.ts`'s `webServer` is **conditional on `process.env.CI`** — Playwright
only auto-starts (and kills) the dev server in CI. Locally, `webServer` is `undefined`,
so **you must start the dev server yourself first**, with the exact same command CI uses:

```bash
cd Client.React/
VITE_API_TARGET=http://localhost:9999 npm run dev -- --port 5173
```

The `VITE_API_TARGET=http://localhost:9999` matters even though nothing runs on 9999 —
every request the mocks don't intercept falls through to Vite's proxy, which tries to
reach that (deliberately nonexistent) target and fails loudly instead of silently
serving the SPA's `index.html` for an API call.

Then, in a second terminal:

```bash
cd Client.React/
npm run test:e2e -- --project=chromium
```

If you skip starting the dev server first, every single spec fails with
`net::ERR_CONNECTION_REFUSED at http://localhost:5173/` — that's not a real failure,
it just means nothing is listening on the port yet.

## Browsers already installed?

Check before trying to install:

```bash
ls ~/.cache/ms-playwright/
```

If `chromium-<version>` is already there, you don't need `npx playwright install`.
Attempting `npx playwright install chromium --with-deps` in a sandboxed/no-sudo
environment fails on the `--with-deps` step (needs `apt`/root) even when the browser
binary itself is already cached and perfectly usable — don't let that failure stop you
from just running the tests.

## Reading failures — match CI's concurrency, don't chase local-only flakes

CI runs `workers: 1` (sequential, see `playwright.config.ts`). Running the full suite
locally with default parallelism can produce failures — timeouts, "element not stable/
detached from DOM" — that are pure resource contention in a busy local environment, not
real bugs. Before treating a failure as real:

```bash
npx playwright test --project=chromium <path/to/spec.ts> --workers=1
```

Re-run the specific failing spec 2-3 times in isolation. If it passes reliably alone,
it was contention noise from running 80+ browser tests at once — not something to "fix."
If it fails reliably alone too, it's a real regression.

For a fully CI-faithful local run (slower, but zero false positives from contention):

```bash
npm run test:e2e -- --project=chromium --workers=1
```

## Route mocks live in `e2e/helpers/`

`setupRoutes()` (`routes.ts`, NFL) and `mockCfbAuth()`/`setupCfbRoutes()` (`cfbRoutes.ts`,
CFB) install one catch-all `page.route('**/*', ...)` handler each, matching on URL +
method. When you add a new API call the frontend makes unconditionally on a page these
mocks cover, add its mock here too — grep the existing `url.includes('/api/...')`
branches for the pattern, and reuse a fixture from `../../src/test/fixtures.ts` where
one already exists (e.g. `createScores`, `createCurrentWeek`) rather than hand-rolling
a new shape.

## Related

- `/demo-stack` — integration e2e against a real `DEMO_MODE=true` backend (`npm run test:e2e:demo`), plus demo user creds and seed data.
- `/full-test-local` — the broader local real-mode prod-readiness gate (Docker Postgres, real ESPN/email), for a `dev`→`main` cutover.
