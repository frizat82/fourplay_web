# IV League

Private NFL pick'em league app. Pick against the spread each week, compete on a leaderboard, get locked out at kickoff. No draft. No waiver wire.

## Stack

| Layer | Tech |
|---|---|
| Backend | ASP.NET Core 9, EF Core, ASP.NET Identity, Quartz.NET, Serilog |
| Frontend | React 19, Vite, MUI v7, React Query, React Hook Form, Zod |
| Database | PostgreSQL (Neon in prod, Docker locally) |
| Auth | JWT (HttpOnly cookies), refresh token rotation |
| Hosting | Railway (API) + Vercel (SPA) |
| Testing | xUnit + NSubstitute (backend), Vitest + RTL (frontend), Playwright (e2e) |

## Features

- Weekly NFL spread picks with per-game kickoff locking
- Over/Under picks in playoffs
- Live leaderboard with scoring and juice/vig
- Admin tools: manage spreads, scores, leagues, users
- Invitation-based user onboarding
- Email confirmation + password reset

## Project Structure

```
ivleague/
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

---

## Local Development

### Prerequisites

| Tool | Purpose | Required? |
|---|---|---|
| [.NET 9 SDK](https://dotnet.microsoft.com/download) | Run the backend | ✅ Yes |
| [Node.js 22+](https://nodejs.org) | Run the frontend | ✅ Yes |
| [Docker Desktop](https://www.docker.com/products/docker-desktop) | Local PostgreSQL only | ✅ Yes |

> **Docker is only used for the local database.** The backend and frontend run natively — no containers needed for them.

---

### Quickest Start: Demo Mode

Demo mode seeds fake users, picks, spreads, and scores so you can explore the full UI without a live NFL season.

**Step 1 — Copy env file:**
```bash
cp .env.backend.example .env.backend
# Fill in your values (JWT key, admin credentials, email config)
```

**Step 2 — Start everything:**
```bash
./scripts/start-demo.sh
```

This script:
1. Starts Docker PostgreSQL on `localhost:5432`
2. Starts the .NET backend on `http://localhost:5000` with `DEMO_MODE=true`
3. Starts the Vite frontend on `http://localhost:5173`

Wait ~2 minutes for the `UserManagerJob` to finish seeding, then open `http://localhost:5173`.

**Demo credentials:**

| User | Email | Password | Role |
|---|---|---|---|
| Admin | *(your `ADMIN_EMAIL`)* | *(your `ADMIN_PASSWORD`)* | Admin |
| Alice | alice@demo.local | `DemoPass@123` | Member |
| Bob | bob@demo.local | `DemoPass@123` | Member |
| Carlos | carlos@demo.local | `DemoPass@123` | Member |
| Dana | dana@demo.local | `DemoPass@123` | Member |
| Eve | eve@demo.local | `DemoPass@123` | Member |

---

### Manual Start (step by step)

**Step 1 — Start local Postgres (Docker required):**
```bash
docker compose up -d
```
Creates a Postgres instance at `localhost:5432` (DB: `fourplay_dev`, user: `fourplay`, password: `fourplay_local`). Data persists in a Docker volume between restarts.

**Step 2 — Start the backend** (run from repo root):
```bash
ConnectionStrings__POSTGRES_CONNECTION_STRING="Host=localhost;Port=5432;Username=fourplay;Password=fourplay_local;Database=fourplay_dev" \
  Jwt__Key="your-32-char-minimum-secret-key" \
  Jwt__Issuer="FourPlayWebAppClientDev" \
  Jwt__Audience="FourPlayWebAppClientDev" \
  Jwt__ExpiresMinutes="1000" \
  FOURPLAY_EMAIL_USER="your@gmail.com" \
  FOURPLAY_EMAIL_PASS="your-gmail-app-password" \
  ADMIN_EMAIL="your@email.com" \
  ADMIN_USERNAME="yourusername" \
  ADMIN_PASSWORD='yourpassword' \
  APP_URL="http://localhost:5173" \
  DEMO_MODE="true" \
  ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --project Server --no-launch-profile --urls http://localhost:5000
```

> **Note:** Use single quotes around `ADMIN_PASSWORD` — double quotes cause bash to expand `!` as history substitution.

EF Core migrations run automatically on startup. The API is ready when you see `Now listening on: http://localhost:5000`.

**Step 3 — Start the frontend** (new terminal, from repo root):
```bash
cd Client.React
npm install          # first time only
VITE_API_TARGET=http://localhost:5000 npm run dev -- --port 5173
```

> **Important:** Always start the backend before Vite. If Vite starts first, all `/api/*` requests return SPA HTML (200 text/html) instead of JSON. Fix: kill Vite and restart after the backend is up.

App is at `http://localhost:5173`.

---

### Port Conflicts

| Port | Service |
|---|---|
| 5000 | .NET backend API |
| 5173 | Vite frontend (may increment to 5174, 5175 if taken) |
| 5432 | PostgreSQL (Docker) |

Kill a port if needed: `lsof -ti :5000 | xargs kill -9`

---

## Environment Variables

Copy `.env.backend.example` to `.env.backend` and fill in values.

| Variable | Description |
|---|---|
| `ConnectionStrings__POSTGRES_CONNECTION_STRING` | PostgreSQL connection string |
| `Jwt__Key` | Minimum 32-char secret for JWT signing |
| `Jwt__Issuer` | JWT issuer (e.g. `FourPlayWebAppClientDev`) |
| `Jwt__Audience` | JWT audience (e.g. `FourPlayWebAppClientDev`) |
| `Jwt__ExpiresMinutes` | Token expiry in minutes |
| `ADMIN_EMAIL` | Seeded admin account email |
| `ADMIN_USERNAME` | Seeded admin account username |
| `ADMIN_PASSWORD` | Seeded admin account password (reset on every startup) |
| `FOURPLAY_EMAIL_USER` | Gmail address for sending emails |
| `FOURPLAY_EMAIL_PASS` | Gmail App Password (not your Gmail password) |
| `APP_URL` | Frontend URL used in email links (e.g. `http://localhost:5173`) |
| `DEMO_MODE` | Set to `"true"` to seed demo data on startup |

---

## Testing

```bash
# Backend unit tests
dotnet test

# Frontend unit tests
cd Client.React && npm run test -- --run

# E2e tests (requires full stack running)
cd Client.React && npx playwright test
```

CI runs on every PR to `dev` and `main`. Required checks: backend build + test, frontend lint + test + build.

---

## Deployment

### Railway (Backend)

- Connects to this repo, deploys `Server/` on push to `main`
- Set all env vars from `.env.backend.example` in Railway service settings
- EF Core migrations run automatically on startup

### Vercel (Frontend)

- Connects to this repo, deploys `Client.React/` on push to `main`
- Set `RAILWAY_URL` to your Railway service URL (e.g. `https://yourapp.up.railway.app`)
- Do **not** set `VITE_API_BASE_URL` — leave unset so the Vercel proxy handles `/api/*` routing
- `vercel.json` rewrites `/api/*` to Railway at the CDN layer (required for Safari ITP + same-origin cookies)

### Neon (Database)

- Serverless PostgreSQL
- Connection string set as `ConnectionStrings__POSTGRES_CONNECTION_STRING` in Railway
- EF Core migrations apply automatically on deploy

---

## Branch Flow

```
feature/* → PR → dev → PR → main
```

- No direct pushes to `main` or `dev`
- PRs require passing CI (backend build + test, frontend lint + test + build)
- Branch protection enforced on both `dev` and `main`

---

## Demo Site

A staging deployment with demo data is available at the dev Vercel URL (password protected). Use the demo credentials above to log in.
