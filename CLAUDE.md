# Project Instructions

## CRITICAL: Branch Rules
- **NEVER push or commit directly to `main`** — all changes go through a PR
- Branch flow: `feature/*` → PR → `dev` → PR → `main`
- Always create a feature branch, open a PR to `dev` first

## Task Tracking
- Use `bd` (beads) for all task tracking — `BEADS_DIR=~/.beads ~/go/bin/bd`
- Requires `dolt` in PATH (`~/go/bin/dolt`)

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
- `--deps` — any blocking beads (e.g. email bead before onboarding bead)

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
1. **Unit tests first (TDD)** — write red tests before any implementation code
   - Backend logic: xUnit tests in `Server.UnitTests/`
   - Frontend logic/components: Vitest tests in `Client.React/src/__tests__/`
2. **Playwright e2e test** — at least one happy-path test covering the full user journey
   - Use `e2e/helpers/routes.ts` + `mockAuth()` for authenticated flows
   - Use typed factory functions from `src/test/fixtures.ts` — never raw JSON fixtures
3. **Full suite must stay green** — `dotnet test` + `npm run test -- --run` + `npx playwright test`

### Before Opening a PR
Before creating any PR, always run these two steps in order:
1. `/simplify` — review changed code for reuse, quality, and efficiency; fix any issues found
2. `/feature-dev:code-review` — review for bugs, logic errors, and security issues; fix any issues found

### Definition of Done
A bead is closeable ONLY when:
- All unit tests in acceptance are written and green
- Full test suite passes:
  - `dotnet test` (backend)
  - `npm run test -- --run` (Vitest, 67+ tests)
  - `npx playwright test` (Playwright e2e, 28+ tests)
- All functional gates verified
- No regressions introduced
- `/simplify` and `/feature-dev:code-review` have been run and issues addressed
- A PR has been opened to `dev`, all CI checks pass, and the PR is merged to `dev`
- A PR has been opened from `dev` to `main`, all CI checks pass, and the PR is merged to `main`

---

## Dev Environment

### Design Philosophy
- **Mobile-first**: Primary audience is iOS users on ~390px viewport (iPhone). Always test UI at 390px width. Touch targets ≥44px.
- **Dark/light theme**: MUI theme toggle, respect system preference.
- Admin pages are secondary UX — functional over beautiful.

### Running the Stack Locally
**Backend** (must start before Vite or proxy breaks, run from `Server/`):
```bash
ConnectionStrings__POSTGRES_CONNECTION_STRING="..." \
  Jwt__Key="..." Jwt__Issuer="FourPlayWebApp" Jwt__Audience="FourPlayWebAppClient" Jwt__ExpiresMinutes="1000" \
  FOURPLAY_EMAIL_USER="..." FOURPLAY_EMAIL_PASS="..." \
  ADMIN_EMAIL="..." ADMIN_USERNAME="frizat" ADMIN_PASSWORD='...' \
  APP_URL="https://fourplaywebapp.azurewebsites.net" \
  ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --no-launch-profile
```
All env vars are in `.env` at the repo root — but `source .env` fails because `FOURPLAY_EMAIL_PASS` contains spaces. Pass vars explicitly or write a wrapper script.
**IMPORTANT**: Use single quotes for `ADMIN_PASSWORD` — double quotes cause bash to expand `!` as history substitution, garbling the password and causing UserManagerJob to set the wrong hash on startup.

**Frontend** (run from `Client.React/`):
```bash
VITE_API_TARGET=http://localhost:5000 npm run dev -- --port 5173
```
The default `VITE_API_TARGET` is `https://localhost:7209` — if you omit this, all API calls will 500.

**Critical**: If Vite starts before the backend, ALL `/api/*` requests return the SPA HTML (200 text/html). Symptom: GET /api/... → 200 text/html, POST /api/... → 404. Fix: kill Vite and restart after backend is up.

**Port conflicts**: Backend uses port 5000. If it fails to bind, `lsof -ti :5000 | xargs kill -9`.

### Database
- PostgreSQL on Neon (connection string in `.env`)
- EF Core migrations auto-apply at startup via `db.Database.Migrate()` — dev only
- Quartz.NET scheduler tables (`quartz.qrtz_*`) are also EF-managed, created on first migration
- To check pending migrations before startup: `db.Database.GetPendingMigrations()`
- For prod: GitHub Actions runs migrations as a pre-deploy step (planned)

### MUI / React Gotchas
- `useMediaQuery(theme.breakpoints.down('md'))` returns `false` on first render (SSR-safe default) — always pass `{ noSsr: true }` for drawer open/close logic
- MUI Drawer: `variant="temporary"` (overlay, mobile) vs `"persistent"` (pushes content, desktop)
- `useState<T>(() => fn())` lazy initializer for synchronous reads (e.g. localStorage) to avoid async race conditions
- All data tables need `<Box sx={{ overflowX: 'auto' }}>` wrapper for mobile scroll
- MUI Select `toHaveValue` doesn't work in Vitest/JSDOM — check visible text content instead

### Chrome DevTools MCP
- Used for live browser debugging via `mcp__plugin_chrome-devtools-mcp_chrome-devtools__*` tools
- Browser emulates iPhone viewport (390×844) by default in this project
- Check network requests with `list_network_requests` to diagnose API failures before reading code
