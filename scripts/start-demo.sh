#!/usr/bin/env bash
# start-demo.sh — starts the full local demo stack
#
# Prerequisites:
#   - Docker running (for Postgres)
#   - .env.backend in the repo root (gitignored, copy from .env.backend.example)
#
# Usage:
#   ./scripts/start-demo.sh          # backend + frontend
#   ./scripts/start-demo.sh backend  # backend only
#   ./scripts/start-demo.sh frontend # frontend only

set -e

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ENV_FILE="$REPO_ROOT/.env.backend"

if [ ! -f "$ENV_FILE" ]; then
    echo "ERROR: $ENV_FILE not found. Copy .env.backend.example and fill in your values."
    exit 1
fi

# Source env file (key=value pairs, no export needed)
set -a
# shellcheck disable=SC1090
source "$ENV_FILE"
set +a

MODE="${1:-all}"

start_postgres() {
    echo ">>> Starting local Postgres..."
    docker compose -f "$REPO_ROOT/docker-compose.yml" up -d
    # Wait for Postgres to be ready
    for i in {1..15}; do
        docker compose -f "$REPO_ROOT/docker-compose.yml" exec -T postgres pg_isready -U fourplay -d fourplay_dev -q && break
        sleep 1
    done
    echo ">>> Postgres ready."
}

start_backend() {
    echo ">>> Starting backend (DEMO_MODE=true, local Postgres)..."
    cd "$REPO_ROOT/Server"
    ConnectionStrings__POSTGRES_CONNECTION_STRING="Host=localhost;Port=5432;Username=fourplay;Password=fourplay_local;Database=fourplay_dev" \
    DEMO_MODE="true" \
    ASPNETCORE_ENVIRONMENT=Development \
    dotnet run --no-launch-profile --urls http://localhost:5000
}

start_frontend() {
    echo ">>> Starting frontend..."
    cd "$REPO_ROOT/Client.React"
    VITE_API_TARGET=http://localhost:5000 npm run dev -- --port 5174
}

case "$MODE" in
    backend)
        start_postgres
        start_backend
        ;;
    frontend)
        start_frontend
        ;;
    all)
        start_postgres
        # Start backend in background, frontend in foreground
        start_backend &
        BACKEND_PID=$!
        echo ">>> Backend PID: $BACKEND_PID (waiting 12s for startup...)"
        sleep 12
        start_frontend
        # Cleanup on exit
        trap "kill $BACKEND_PID 2>/dev/null; docker compose -f '$REPO_ROOT/docker-compose.yml' stop" EXIT
        ;;
    *)
        echo "Usage: $0 [all|backend|frontend]"
        exit 1
        ;;
esac
