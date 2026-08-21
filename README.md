# Fourplay

A fantasy sports picks app for NFL and other leagues. Players make weekly spread picks, compete on a leaderboard, and get locked out once games kick off.

## Stack

| Layer | Tech |
|---|---|
| Backend | ASP.NET Core 9, EF Core, ASP.NET Identity, Quartz.NET, Serilog |
| Frontend | React 19, Vite, MUI v7, React Query, React Hook Form, Zod |
| Database | PostgreSQL (Neon) |
| Auth | JWT (HttpOnly cookies), refresh token rotation |
| Hosting | Railway (API) + Vercel (SPA) |
| Testing | xUnit + NSubstitute (backend), Vitest + RTL (frontend), Playwright (e2e) |

## Features

- Weekly NFL spread picks with per-game kickoff locking
- Over/Under picks per game
- Live leaderboard with scoring
- Admin tools: manage spreads, scores, leagues, users
- Invitation-based user onboarding
- Email confirmation flow

## Project Structure

```
fourplay/
├── Server/                    # ASP.NET Core API
│   ├── Controllers/           # HTTP endpoints
│   ├── Services/              # Business logic
│   ├── Jobs/                  # Quartz.NET background jobs
│   ├── Data/                  # EF Core DbContext + configurations
│   ├── Models/                # Domain models
│   └── Migrations/            # EF Core migrations
├── Server.UnitTests/          # xUnit tests
├── Client.React/              # React SPA
│   ├── src/
│   │   ├── pages/             # Route-level components
│   │   ├── components/        # Shared UI components
│   │   ├── services/          # Auth context, API client
│   │   ├── api/               # Typed API call functions
│   │   └── __tests__/         # Vitest unit tests
│   └── e2e/                   # Playwright e2e tests
└── .github/workflows/         # CI/CD
```

## Local Development

### Prerequisites

- .NET 9 SDK
- Node.js 22+
- PostgreSQL (or use Neon free tier)

### Backend

Copy `.env.example` to `.env` and fill in values, then:

```bash
cd Server
# Export env vars from .env (see note below about special characters)
dotnet run --no-launch-profile
```

> **Note:** If `ADMIN_PASSWORD` contains `!`, use single quotes when exporting to avoid bash history expansion.

The API starts on `http://localhost:5000`. EF Core migrations run automatically on startup in Development.

### Frontend

```bash
cd Client.React
npm install
VITE_API_TARGET=http://localhost:5000 npm run dev -- --port 5173
```

> **Important:** Start the backend before Vite. If Vite starts first, all `/api/*` requests return the SPA HTML until Vite is restarted.

App is at `http://localhost:5173`.

### Environment Variables

See `.env.example` for all required variables. Key ones:

| Variable | Description |
|---|---|
| `ConnectionStrings__POSTGRES_CONNECTION_STRING` | PostgreSQL connection string |
| `Jwt__Key` | Minimum 32-char secret for JWT signing |
| `ADMIN_EMAIL` | Seeded admin account email |
| `ADMIN_USERNAME` | Seeded admin account username |
| `ADMIN_PASSWORD` | Seeded admin account password (reset on every startup) |
| `FOURPLAY_EMAIL_USER` | Gmail address for sending emails |
| `FOURPLAY_EMAIL_PASS` | Gmail App Password |

## Testing

```bash
# Backend unit tests
dotnet test

# Frontend unit tests
cd Client.React && npm run test -- --run

# E2e tests (requires running stack)
cd Client.React && npx playwright test
```

CI runs on every PR to `dev` and `main`.

## Deployment

### Railway (Backend)

- Connects to this repo; the `development` environment auto-deploys `Server/` on push to `dev`, the `production` environment on push to `main`
- Set all env vars from `.env.example` in Railway service settings (per environment)
- `RAILWAY_URL` must be set in Vercel (see below)

### Vercel (Frontend)

- Connects to this repo; `dev.ivleague.xyz` / `cfb.dev.ivleague.xyz` track pushes to `dev`, `ivleague.xyz` / `cfb.ivleague.xyz` track pushes to `main`
- Set `RAILWAY_URL` to your Railway service URL (e.g. `https://yourapp.up.railway.app`)
- Do **not** set `VITE_API_BASE_URL` — leave it unset so the Vercel proxy handles `/api/*` routing

The `vercel.json` rewrites `/api/*` to Railway at the CDN layer, keeping cookies same-origin (required for Safari ITP compatibility).

If a push to `dev` or `main` doesn't show up on Railway/Vercel within a minute or two, check each platform's deployment history for the commit — an entry that's `SKIPPED`/`FAILED` means the platform saw the push but declined to build; no entry at all for that commit means the trigger never fired (check `gh api repos/<owner>/<repo>/deployments?sha=<sha>` — both platforms register a GitHub Deployment record when they pick up a push, so a missing record here is a fast way to tell "never triggered" from "triggered but stuck").

## Branch Flow

```
feature/* → PR → dev → PR → main
```

- No direct pushes to `main` or `dev`
- PRs require passing CI (lint, type-check, unit tests, build)
- **Merge with a regular merge, never squash** — squash merges have been observed to not reliably trigger the Railway/Vercel deploy webhooks on this repo; squash merging is disabled repo-wide as a result

## Contributing

1. Fork and clone
2. Create a `feature/your-feature` branch
3. Write tests first (TDD)
4. Open a PR to `dev`
