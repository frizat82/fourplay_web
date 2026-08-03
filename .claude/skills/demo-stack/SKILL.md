---
name: demo-stack
description: Start, verify, and reset the local demo stack. Port conflicts, seed data, demo users, running integration e2e tests.
---

# Demo Stack

The demo stack runs a `DEMO_MODE=true` backend with deterministic seed data. Playwright integration e2e tests run against it.

## Starting the Stack

```bash
# Easiest — postgres + backend (port 5000) + frontend (port 5174):
./scripts/start-demo.sh

# Or manually:
docker compose up -d    # Step 1: start Postgres on localhost:5432

# Backend (from Server/):
ConnectionStrings__POSTGRES_CONNECTION_STRING="Host=localhost;Port=5432;Username=fourplay;Password=fourplay_local;Database=fourplay_dev" \
  DEMO_MODE="true" ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --no-launch-profile --urls http://localhost:5000

# Frontend (from Client.React/):
VITE_API_TARGET=http://localhost:5000 npm run dev -- --port 5174
```

## URLs

| URL | Sport |
|-----|-------|
| `localhost:5174` | NFL |
| `cfb.localhost:5174` | CFB |
| `/admin/leagueManagement` | Admin (log in as `admin`) |

## Demo Users

| Username | Password | Role |
|----------|----------|------|
| alice | `DemoPass@123` | User |
| bob | `DemoPass@123` | User |
| carlos | `DemoPass@123` | User |
| dana | `DemoPass@123` | User |
| eve | `DemoPass@123` | User |
| admin | `DemoPass@123` | Admin |

## Seed Data

Seed is deterministic and idempotent — re-running clears and re-seeds.

- **NFL**: 2025 season, all 18 regular season weeks + Wild Card/Divisional/Conference/Super Bowl (NflWeeks 19–22). Week 18 games from frozen `sample_espn_nfl.json`. All weeks have spreads + scores + picks.
- **CFB**: 2025 season, all 18 slates ("CFB Demo League"). All slates have spreads, scores, and picks.
- Alice's NFL Week 18 picks: BUF, DAL, MIN, MIA
- Alice's CFP Championship pick: IU (Indiana)

Pick count invariants → see `/pick-rules`.

## Running Integration E2E Tests

```bash
cd Client.React/
npm run test:e2e:demo                           # all demo tests
npm run test:e2e:demo -- --grep "CFB picks"     # target a specific area
```

Setup projects (`setup.nfl.ts`, `setup.cfb.ts`) log in as Alice and save cookies to `e2e/demo/.auth/`. Run `npm run test:e2e:demo` locally before pushing — don't push blind and wait for CI.

## Troubleshooting

**Port conflict on 5000**: `lsof -ti :5000 | xargs kill -9`

**Vite starts before backend**: all `/api/*` requests return SPA HTML. Kill Vite, wait for backend, restart Vite.

**Use single quotes for `ADMIN_PASSWORD`** — double quotes cause bash history expansion on `!`.

**Local dev always uses Docker PostgreSQL** — never connect to Neon (dev or prod) directly.
