#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${CONTROLIT_ENV_FILE:-$ROOT_DIR/.env}"
COMPOSE_FILE="${CONTROLIT_COMPOSE_FILE:-$ROOT_DIR/docker-compose.controlit.yml}"
BRANCH="${CONTROLIT_RELEASE_BRANCH:-production}"
HEALTH_URL="${CONTROLIT_HEALTH_URL:-http://localhost:5290/health/ready}"
LOCK_DIR="$ROOT_DIR/.controlit-update.lock"

if [[ ! -f "$ENV_FILE" ]]; then
  echo "ERROR: missing $ENV_FILE. Run ./scripts/setup-controlit-env.sh first." >&2
  exit 1
fi

cd "$ROOT_DIR"

if ! mkdir "$LOCK_DIR" 2>/dev/null; then
  echo "ERROR: another ControlIT update is already running." >&2
  exit 1
fi
trap 'rm -rf "$LOCK_DIR"' EXIT

if [[ -n "$(git status --porcelain)" ]]; then
  echo "ERROR: working tree has local changes. Commit/stash before updating." >&2
  exit 1
fi

backup_file="$ENV_FILE.backup.$(date +%Y%m%d%H%M%S)"
cp "$ENV_FILE" "$backup_file"

git fetch origin "$BRANCH"
current_commit="$(git rev-parse HEAD)"
available_commit="$(git rev-parse "origin/$BRANCH")"

if [[ "$current_commit" == "$available_commit" && "${CONTROLIT_FORCE_UPDATE:-}" != "1" ]]; then
  echo "ControlIT already current: $current_commit"
  echo "Set CONTROLIT_FORCE_UPDATE=1 to rebuild without a new release."
  exit 0
fi

git checkout "$BRANCH"
git pull --ff-only origin "$BRANCH"

./scripts/run-controlit-migrations.sh
./scripts/apply-controlit-db-user.sh
docker compose -f "$COMPOSE_FILE" up -d --build

for attempt in {1..30}; do
  if curl -fsS "$HEALTH_URL" >/dev/null; then
    echo "ControlIT update complete. Readiness check passed: $HEALTH_URL"
    echo "Environment backup: $backup_file"
    exit 0
  fi

  sleep 2
done

echo "ERROR: ControlIT update finished but readiness check did not pass: $HEALTH_URL" >&2
echo "Environment backup: $backup_file" >&2
exit 1
