# CLAUDE.md

This file provides guidance to Claude Code when working with this repository.

## What This App Is

**IV League** is a weekly pick'em pool for NFL and CFB. Users pick N teams against a **league-admin-configured teased spread** each week. ALL picks must be correct to win. Winners collect juice (money) from losers.

The tease amount is **NOT hardcoded** — set per-league via `LeagueJuiceMapping` (`Juice` / `JuiceDivisional` / `JuiceConference` / `WeeklyCost`).

Admin pages: `/admin/leagueManagement` — requires `isAdmin(user)=true`. Log in as `admin` in the demo stack.

## CRITICAL: Pick Count Invariants

**Run `/pick-rules` before touching any pick logic.** Full NFL/CFB pick count tables, CFB slate numbering, seeder invariants, and test rot checklist live there.

Short version: NFL regular season = 4 picks; postseason decreases 3→3→2→1. CFB slates 1–14 = 4, 15–16 = 3, 17 = 2, 18 = 1.

---

## Test Quality Standards

### Before Writing Any Test
1. Understand the correct business rule FIRST — run `/pick-rules` if touching pick logic
2. A test asserting the wrong value is WORSE than no test — false confidence hides bugs
3. Tests must assert THE RULE, not the current (possibly broken) implementation

### The Seeder Is Production-Critical
`DemoDataSeeder` seeds the demo DB that Playwright e2e tests run against. After any pick logic change: re-verify seeder counts AND run `npm run test:e2e:demo`. Never fudge `ExpectedPickCount`.

### Test Rot Prevention
When fixing a bug: grep existing tests for old wrong values before shipping. When changing `GetRequiredPicks`/`GetCfbRequiredPicks`: read ALL test files that reference the function, update expected values first, then fix the implementation.

---

## CRITICAL: TDD Is the Primary Development Methodology
- **Write failing tests FIRST, then implement** — no exceptions
- Red → Green → Refactor is the only acceptable order

### What Requires a Test (mandatory checklist per feature)

| What changed | Required test |
|---|---|
| New backend controller endpoint | xUnit test: happy path, auth/ownership 403, error branch |
| New frontend pure function / helper | Vitest unit test covering edge cases |
| New page or significant UI component | Mock-based Playwright test (happy path + empty/error state) |
| New nav link or conditional UI element | Unit test asserting visibility rules (shown/hidden by role/state) |
| New API route in routes.ts | Verify mock is wired — missing mock silently falls back to defaults |
| Security guard (ownership/admin check) | xUnit: Forbid unauthorized, Ok authorized, Ok admin |
| Pick reveal / game kickoff logic | Unit test: scheduled=hidden, in_progress=visible, ESPN null=fail-open |

### Common Test Gaps to Watch For
- **Controller ownership guard** — every `Forbid()` branch needs its own test
- **Session-derived flags** (`ownedLeagues`, sport filter) — test in `session.test.tsx`
- **New page with no route in `routes.ts`** — will 404 silently in all existing e2e tests
- **Frontend pick reveal** — `revealPicksForStartedGames` tested in `sportAdapter.test.ts`; update those tests first

---

## CRITICAL: API Surface — DTOs and Strongly Typed Objects

**Every API boundary must use an explicit DTO or strongly typed record/class — no anonymous objects, no `dynamic`, no raw `Dictionary<string,object>`, no `object` return types.**

- **Backend**: all controller endpoints return and accept named records or classes from `Shared/Models/Data/Dtos/` or `Shared/Models/`. Never serialize anonymous `new { }` objects — create a DTO.
- **Frontend**: all API functions in `src/api/` must declare and export a TypeScript `interface` or `type` for every request and response shape. Never use `any`; avoid `unknown` except at boundaries where you immediately narrow the type.
- When adding a new endpoint, add the corresponding DTO to `Shared/Models/Data/Dtos/` first, then wire it in.

---

## CRITICAL: NFL/CFB Code Sharing — No Duplicated Logic Between Sport Paths

**One implementation, two sport call sites — never two implementations.**

- Business logic (status derivation, pick validation, security guards, leaderboard scoring, spread formatting) lives in **one shared function/service** that both NFL and CFB call. Never copy-paste and modify.
- `PicksPage`, `ScoresPage`, `LeaderboardPage` are sport-agnostic via `adapter: SportAdapter` — keep them that way. Put sport-specific logic in the adapter, not the page.
- When fixing a bug on one sport's path, **always check the other sport's equivalent** before shipping — open the CFB controller/adapter/service when you fix the NFL one, and vice versa.
- Duplicated logic between NFL and CFB paths is a P0 bug magnet — it has repeatedly caused diverging guards and status-parsing bugs.

---

## CRITICAL: Branch Rules
- **NEVER push or commit directly to `main`** — all changes go through a PR
- Branch flow: `feature/*` → PR → `dev` → PR → `main`
- Before a `dev`→`main` cutover (season launch or major release), run `/prod-live-test` — the full live E2E prod-readiness gate (real users, real ESPN data, real emails, real job schedule on staging)

## Task Tracking
- Use `bd` (beads) — `LD_LIBRARY_PATH=~/.local/lib BEADS_DIR=~/.beads ~/.local/bin/bd`
- Binary lives at `~/.local/bin/bd`; requires `dolt` in PATH (`~/go/bin/dolt`)

---

## Bead Standards

### Creating a Bead
Every bead MUST include: `--title`, `--type` (bug|feature|task|chore), `--priority` (P0–P4), `--description`, `--design`, `--acceptance` (Unit Tests red first / Functional Gates / Success Definition), `--external-ref` (GitHub issue), `--deps`.

### Taking a Bead
1. `bd show <id>` — read design notes and acceptance
2. Write ALL listed unit tests first — they must be red (failing)
3. Confirm failing: `dotnet test` or `npm run test -- --run`
4. Implement; all tests must go green; 0 regressions

### CRITICAL: Before Opening a PR (mandatory — no exceptions)
Run ALL three steps in order. Skipping any is a violation of this process.

1. **Full test suite** — all must pass:
   ```bash
   dotnet test && npm run test -- --run && npm run test:e2e -- --project=chromium
   ```
2. **`/simplify`** — review for reuse, quality, efficiency; apply fixes
3. **`/feature-dev:code-review`** — review for bugs, logic errors, security; address findings

### Definition of Done
Closeable ONLY when: full test suite passes · mandatory checklist satisfied · `/simplify` + `/code-review` run · PR merged to `dev` then `main`

---

## Commands

### Backend (from repo root or `Server/`)
```bash
dotnet build
dotnet test
dotnet test --filter "ClassName"
dotnet run --project Server --no-launch-profile --urls http://localhost:5000
```

### Frontend (from `Client.React/`)
```bash
npm run typecheck          # tsc -b (matches CI/Vercel strict mode)
npm run lint
npm run test -- --run
npm run test -- --run src/__tests__/GameCard.test.tsx
npm run test:e2e -- --project=chromium   # mock-based Playwright
npm run dev -- --port 5173
npm run build
```

### Demo Stack
Run `/demo-stack` for full startup instructions, users, seed data, and troubleshooting.
```bash
./scripts/start-demo.sh               # easiest: postgres + backend + frontend (port 5174)
npm run test:e2e:demo                 # integration tests (from Client.React/)
npm run test:e2e:replay               # replay integration tests — backend needs DEMO_REPLAY_MODE=true too
```

---

## Architecture

### Stack
- **Backend**: ASP.NET Core 9 + EF Core + ASP.NET Identity + Quartz.NET + Serilog
- **Frontend**: React 19 + Vite + MUI v7 + React Router v7
- **Database**: PostgreSQL (local Docker for dev, Neon for prod)
- **Auth**: JWT in HttpOnly cookies (`AuthToken`) + refresh token rotation
- **Hosting**: Railway (API) + Vercel (SPA)
- **Testing**: xUnit + NSubstitute (backend), Vitest + RTL (frontend), Playwright (e2e)

### Dual-Sport Architecture
Sport is determined by subdomain: `localhost:5173` / `ivleague.com` → NFL; `cfb.localhost:5173` / `cfb.ivleague.com` → CFB.
`useSportContext()` detects sport via `window.location.hostname.startsWith('cfb.')`. The session layer filters leagues by `leagueType` (0=NFL, 1=CFB).

**NFL and CFB are siblings, not separate products — treat them as one system with two data sources.** When adding or changing any backend job, service, scheduler, or repository method for one sport, the default assumption is the other sport needs the identical mechanism, not a bespoke one — build one shared implementation (interface + base class, or a single parameterized function) that both sports' thin adapters plug into, rather than writing NFL-shaped code and CFB-shaped code side by side. If a genuine sport-specific difference exists (e.g. CFB's slate/eligibility model, which NFL has no equivalent of), isolate *only* that difference behind the shared interface — do not let it justify duplicating the surrounding logic too. Before merging any PR that adds NFL-only or CFB-only code, ask explicitly: does the other sport need this? If yes, it ships together, not as a follow-up. This is not a style preference — duplicated NFL/CFB logic has repeatedly drifted into real, shipped bugs (mismatched guards, asymmetric job cadences that went unnoticed until reviewed, status-parsing bugs that only manifested for one sport).

`PicksPage`, `ScoresPage`, and `LeaderboardPage` are sport-agnostic via an `adapter: SportAdapter` prop injected by `App.tsx`.
- **NFL adapter** (`nflAdapter.ts`): polls ESPN `/api/espn/scores`, fetches spreads via `spreadBatch`
- **CFB adapter** (`cfbAdapter.ts`): spreads/picks/slate metadata from our own DB (`/api/cfb/*`), live score/status/situation overlaid from ESPN via `ICfbCacheService` (`/api/espn/cfb/scores`, `/api/espn/cfb/livegames`, SSE at `/api/cfb/live-stream`) — same shape as NFL's `IEspnCacheService`, just a different cache instance. DB only ever persists a game once it's FINAL, matching NFL's `NflScoresJob`.

CFB uses an 18-slate season. Slate/ESPN week mappings and pick counts → run `/pick-rules`.

### Backend Structure
- `Server/Controllers/` — thin HTTP controllers; business logic in services
- `Server/Services/` — `DemoDataSeeder`, `SpreadCalculatorService`, `LeaderboardService`, `EspnCacheService`, etc.
- `Server/Jobs/` — Quartz.NET: `NflScoresJob`, `NflSpreadJob`, `CfbSlateSeederJob`, `CfbSpreadJob`, `CfbScoresJob`, `MissingPicksJob`
- `Server/Data/ApplicationDbContext.cs` — key tables: `NflPicks/Spreads/Scores/Weeks`, `CfbSlates/SeasonWeekConfigs/Spreads/Scores/Picks`, `LeagueInfo`, `LeagueJuiceMapping`

### Frontend Structure
- `src/pages/` — `PicksPage`, `ScoresPage`, `LeaderboardPage` (sport-agnostic via adapter)
- `src/components/` — `sports/GameCard.tsx`, `WeekYearSelector.tsx`, `SpreadRelease.tsx`
- `src/services/` — `auth.tsx`, `session.tsx`, `sport.tsx`, `sportAdapter.ts`
- `src/api/` — typed fetch functions: `league.ts`, `espn.ts`, `cfb.ts`, etc.
- `src/utils/gameHelpers.ts` — week conversions, ESPN status parsing, spread formatting

### Auth Flow
JWT in HttpOnly cookie (`AuthToken`), read via custom `OnMessageReceived` hook in `Program.cs`. Refresh tokens rotate on each use. `auth.tsx` calls `GET /api/auth/me` on load.

**All frontend API calls — including `EventSource`/SSE connections — must use relative paths (`/api/...`), never an absolute `VITE_API_TARGET` URL.** Every request goes through a same-origin proxy (Vite locally, Vercel's `/api/:path*` rewrite in prod — see `vercel.json`), which is required both for Safari ITP and because the auth cookie is `SameSite=Lax` when not `Secure` (only `SameSite=None` over HTTPS in prod). An absolute cross-origin URL bypasses the proxy and silently drops the cookie — this broke CFB's live SSE stream (`sseUrl` in `nflAdapter.ts`/`cfbAdapter.ts`) until fixed.

The login endpoint is rate-limited to 5 requests/minute per IP (`Program.cs`). E2E specs that log in as admin to drive test-only endpoints should authenticate once and reuse the `APIRequestContext`, not log in per call.

### Testing Architecture
- **Mock-based Playwright** (`e2e/` excl. `demo/`): `page.route()` intercepts all `/api/*`. `mockAuth()` + `setupRoutes()` from `e2e/helpers/`. Runs in CI against a Vite dev server.
- **Integration Playwright** (`e2e/demo/`): live `DEMO_MODE=true` backend at `localhost:5174`. Alice's session saved to `e2e/demo/.auth/`. See `/demo-stack`.
- **Replay Playwright** (`e2e/demo/replay-*.spec.ts`): live `DEMO_MODE=true DEMO_REPLAY_MODE=true` backend at `localhost:5175` (NFL) / `cfb.localhost:5175` (CFB) — `ReplayCacheService` drives one real captured ESPN game through actual state transitions (scheduled→halftime→in_progress→final) via test-only `POST /api/replay/{advance,reset}`, proving the full pick→live-update→settle flow against real wire data instead of a static fixture. `npm run test:e2e:replay`.

---

## Dev Environment

**Mobile-first**: primary audience is iOS (~390px viewport). Touch targets ≥44px. Dark/light MUI theme.

**Database**: EF Core migrations auto-apply at startup in Development. **Local dev always uses Docker PostgreSQL — never connect to Neon directly.** Prod: connection string in `.env`.

### MUI / React Gotchas
- `useMediaQuery(theme.breakpoints.down('md'))` returns `false` on first render — always pass `{ noSsr: true }` for drawer open/close logic
- MUI Select `toHaveValue` doesn't work in Vitest/JSDOM — check visible text content instead
- All data tables need `<Box sx={{ overflowX: 'auto' }}>` wrapper for mobile scroll
- **Run `/style-guide` before touching any color, button variant, or status indicator (dot, chip, badge).** Color semantics, MUI disabled-state gotchas, and the recurring light/dark contrast bug live there — don't re-derive or re-litigate them per component.

### Chrome DevTools MCP
`mcp__plugin_chrome-devtools-mcp_chrome-devtools__*` tools. Browser emulates iPhone (390×844) by default. Use `list_network_requests` to diagnose API failures before reading code.
