# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## CRITICAL: TDD Is the Primary Development Methodology
- **Write failing tests FIRST, then implement** — no exceptions
- Red → Green → Refactor is the only acceptable order
- If you catch yourself writing code without a failing test, stop and write the test first

## CRITICAL: Branch Rules
- **NEVER push or commit directly to `main`** — all changes go through a PR
- Branch flow: `feature/*` → PR → `dev` → PR → `main`
- Always create a feature branch, open a PR to `dev` first

## Task Tracking
- Use `bd` (beads) for all task tracking — `BEADS_DIR=~/.beads bd`
- Binary lives at `~/.local/bin/bd`; requires `dolt` in PATH (`~/go/bin/dolt`)

## Bead Standards

### Creating a Bead
Every bead MUST include:
- `--title` — clear, action-oriented title
- `--type` — bug | feature | task | chore
- `--priority` — P0 (critical) through P4 (nice-to-have)
- `--description` — what the problem is and why it matters
- `--design` — where in the codebase to look, proposed approach
- `--acceptance` — structured with the sections below
- `--external-ref` — GitHub issue number if one exists (e.g. `gh-44`)
- `--deps` — any blocking beads

Acceptance criteria MUST contain:
1. **Unit Tests (red first)** — specific failing test cases to write before any implementation
2. **Functional Gates** — observable behaviors that must work
3. **Success Definition** — one paragraph describing the done state unambiguously

### Taking a Bead (starting work)
Before writing any code:
1. `bd show <id>` — read the full bead including design notes and acceptance
2. Write ALL unit tests listed in acceptance first — they must be red (failing)
3. Confirm `dotnet test` or `npm run test -- --run` shows the new tests failing
4. Only then implement the fix/feature
5. All tests must go green before marking complete
6. Run the full test suite — 0 regressions allowed

### New Feature Requirements
Every new user-facing feature MUST have:
1. **Unit tests first (TDD)**
   - Backend logic: xUnit tests in `Server.UnitTests/`
   - Frontend logic/components: Vitest tests in `Client.React/src/__tests__/`
2. **Playwright e2e test** — at least one happy-path test
   - Mock-based: use `mockAuth()` + `setupRoutes()` from `e2e/helpers/`
   - Integration: add to `e2e/demo/` if it requires real backend data
   - Use typed factory functions from `src/test/fixtures.ts` — never raw JSON

### Before Opening a PR
1. `/simplify` — review changed code for reuse, quality, and efficiency
2. `/feature-dev:code-review` — review for bugs, logic errors, and security issues

### Definition of Done
A bead is closeable ONLY when:
- Full test suite passes: `dotnet test` + `npm run test -- --run` (189+ tests) + `npm run test:e2e -- --project=chromium` (40+ tests)
- `/simplify` and `/feature-dev:code-review` have been run and issues addressed
- PR merged to `dev`, then PR merged to `main`

---

## Commands

### Backend (run from repo root or `Server/`)
```bash
dotnet build                          # build
dotnet test                           # run all xUnit tests
dotnet test --filter "ClassName"      # run a single test class
dotnet run --project Server --no-launch-profile --urls http://localhost:5000
```

### Frontend (run from `Client.React/`)
```bash
npm run typecheck          # tsc -b (matches CI/Vercel strict mode)
npm run lint               # ESLint
npm run test -- --run      # Vitest (all tests, no watch)
npm run test -- --run src/__tests__/GameCard.test.tsx   # single file
npm run test:e2e -- --project=chromium                 # mock-based Playwright
npm run test:e2e:demo                                  # integration tests (needs demo stack)
npm run test:e2e:demo -- --grep "CFB picks"            # target a specific area
npm run dev -- --port 5173             # Vite dev server
npm run build                          # production build
```

### Demo Stack
```bash
./scripts/start-demo.sh               # easiest: starts postgres + backend + frontend (port 5174)
docker compose up -d                  # start Postgres only (localhost:5432)
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
The app serves two sports from a single codebase. Sport is determined by subdomain:
- `localhost:5173` / `ivleague.com` → NFL
- `cfb.localhost:5173` / `cfb.ivleague.com` → CFB

`useSportContext()` in `src/services/sport.tsx` detects the sport via `window.location.hostname.startsWith('cfb.')`. The session layer (`src/services/session.tsx`) then filters leagues by `leagueType` (0=NFL, 1=CFB). `AppLayout.tsx` shows a "No NFL/CFB access" message if the user has no leagues for the current sport.

#### SportAdapter Pattern
`PicksPage`, `ScoresPage`, and `LeaderboardPage` receive an `adapter: SportAdapter` prop and are sport-agnostic. `App.tsx` injects `nflAdapter` or `cfbAdapter` based on `useSportContext().isCfb`.

- **NFL adapter** (`src/services/nflAdapter.ts`): polls ESPN's `/api/espn/scores`, fetches spreads via `spreadBatch`, loads live game situations
- **CFB adapter** (`src/services/cfbAdapter.ts`): uses our own DB (slates/spreads/scores/picks via `/api/cfb/*`), no ESPN live data

Both adapters normalize to `GameView` / `LoadedWeek` / `LoadedScores` from `src/services/sportAdapter.ts`.

#### CFB Slate Numbering
CFB seasons use "slates" (not ESPN weeks). SlateNumbers 1–14 = regular season weeks, 15–19 = postseason:

| SlateNumber | Label |
|---|---|
| 1–14 | Week 1 – Week 14 |
| 15 | Conf. Championships |
| 16 | CFP First Round |
| 17 | CFP Quarterfinals |
| 18 | CFP Semifinals |
| 19 | CFP National Championship |

`cfbSlateNumberToWeek(n)` / `cfbWeekToSlateNumber(week, isPostSeason)` convert between them (`src/utils/gameHelpers.ts`).

#### NFL Week Numbering
NFL stores weeks 1–18 as regular season and maps postseason rounds to weeks 1–4 (Wild Card, Divisional, Conference, Super Bowl). `getWeekFromEspnWeek(week, isPostSeason)` converts ESPN week numbers to the DB's `nflWeek` value (postseason weeks are offset by +18).

### Backend Structure
- **`Server/Controllers/`** — thin HTTP controllers; business logic lives in services
- **`Server/Services/`** — `DemoDataSeeder`, `SpreadCalculatorService`, `LeaderboardService`, `EspnCacheService`, etc.
- **`Server/Jobs/`** — Quartz.NET scheduled jobs: `NflScoresJob` / `NflSpreadJob` (pull from ESPN/odds API weekly), `CfbSlateSeederJob` / `CfbSpreadJob` / `CfbScoresJob` (CFB data), `UserManagerJob` (creates/confirms demo users), `MissingPicksJob`
- **`Server/Data/ApplicationDbContext.cs`** — single EF Core context; key tables: `NflPicks`, `NflSpreads`, `NflScores`, `NflWeeks`, `CfbSlates`, `CfbSpreads`, `CfbScores`, `CfbPicks`, `LeagueInfo`, `LeagueUserMapping`, `LeagueJuiceMapping`
- **`Shared/`** — DTOs shared between backend and (historically) frontend

### Frontend Structure
- **`src/pages/`** — route-level components (`PicksPage`, `ScoresPage`, `LeaderboardPage` are sport-agnostic via adapter)
- **`src/components/`** — shared UI; key: `sports/GameCard.tsx` (pick/score card for both sports), `WeekYearSelector.tsx` (navigates weeks/seasons/season-type), `SpreadRelease.tsx` (countdown or "no odds" message)
- **`src/services/`** — React context providers: `auth.tsx` (JWT + refresh), `session.tsx` (league selection + sport access), `sport.tsx` (subdomain detection), `sportAdapter.ts` (shared interface)
- **`src/api/`** — typed async fetch functions grouped by domain (`league.ts`, `espn.ts`, `cfb.ts`, etc.)
- **`src/utils/gameHelpers.ts`** — week conversions, ESPN status parsing, spread formatting

### Auth Flow
JWT is stored in an HttpOnly cookie (`AuthToken`). The backend reads it via a custom `OnMessageReceived` Kestrel hook in `Program.cs`. Refresh tokens rotate on each use. `src/services/auth.tsx` calls `GET /api/auth/me` on load to hydrate the auth context.

### Testing Architecture

#### Mock-based Playwright (`e2e/` excluding `demo/`)
Uses `page.route()` to intercept all `/api/*` calls. `mockAuth()` in `e2e/helpers/auth.ts` sets a fake JWT cookie and routes `/api/auth/me` to return `TEST_USER` or `ADMIN_USER`. All routes are centralized in `e2e/helpers/routes.ts`. Runs in CI against a Vite dev server (no real backend).

#### Integration Playwright (`e2e/demo/`)
Runs against a live `DEMO_MODE=true` backend at `localhost:5174` (NFL) and `cfb.localhost:5174` (CFB). Uses storage-state auth: `setup.nfl.ts` / `setup.cfb.ts` log in as Alice once and save cookies to `e2e/demo/.auth/`. Test projects (`demo-nfl`, `demo-cfb`) depend on the setup projects. Run with `npm run test:e2e:demo`.

**Demo seed data** (deterministic, idempotent):
- NFL: 2023 Week 8, 16 games (frozen `sample_espn_nfl.json`), 16 spreads, "Demo League"
- CFB: 2025 season, 19 slates (Week 1–14 + 5 CFP rounds), "CFB Demo League"
- Users: Alice, Bob, Carlos, Dana, Eve (password: `DemoPass@123`) + admin
- Alice's NFL picks: BUF, DAL, MIN, MIA; Alice's CFP Championship pick: IU

---

## Dev Environment

### Design Philosophy
- **Mobile-first**: Primary audience is iOS users on ~390px viewport (iPhone). Touch targets ≥44px.
- **Dark/light theme**: MUI theme toggle, respects system preference.
- Admin pages are secondary UX — functional over beautiful.

### Running the Stack Locally

**IMPORTANT: Local dev uses a local Docker PostgreSQL — never connect to Neon directly.**

```bash
docker compose up -d                                      # Step 0: start Postgres (localhost:5432)
./scripts/start-demo.sh                                   # easiest full-stack start
```

Or manually:
```bash
# Backend (from Server/):
ConnectionStrings__POSTGRES_CONNECTION_STRING="Host=localhost;Port=5432;Username=fourplay;Password=fourplay_local;Database=fourplay_dev" \
  DEMO_MODE="true" ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --no-launch-profile --urls http://localhost:5000

# Frontend (from Client.React/):
VITE_API_TARGET=http://localhost:5000 npm run dev -- --port 5173
```

**Use single quotes for `ADMIN_PASSWORD`** — double quotes cause bash history expansion on `!`, garbling the password.

**If Vite starts before the backend**: all `/api/*` requests return SPA HTML. Fix: kill Vite, restart after backend is up.

**Port conflict**: `lsof -ti :5000 | xargs kill -9`

### Database
- EF Core migrations auto-apply at startup in Development (`db.Database.Migrate()`)
- Quartz.NET scheduler tables (`quartz.qrtz_*`) are EF-managed
- Prod: Neon PostgreSQL; connection string in `.env`

### MUI / React Gotchas
- `useMediaQuery(theme.breakpoints.down('md'))` returns `false` on first render — always pass `{ noSsr: true }` for drawer open/close logic
- MUI Select `toHaveValue` doesn't work in Vitest/JSDOM — check visible text content instead
- All data tables need `<Box sx={{ overflowX: 'auto' }}>` wrapper for mobile scroll

### Chrome DevTools MCP
- Used for live browser debugging via `mcp__plugin_chrome-devtools-mcp_chrome-devtools__*` tools
- Browser emulates iPhone viewport (390×844) by default in this project
- Check network requests with `list_network_requests` to diagnose API failures before reading code
