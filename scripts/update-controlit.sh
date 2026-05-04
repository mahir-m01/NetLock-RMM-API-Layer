#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${CONTROLIT_ENV_FILE:-$ROOT_DIR/.env}"
BRANCH="${CONTROLIT_RELEASE_BRANCH:-production}"
HEALTH_URL="${CONTROLIT_HEALTH_URL:-http://localhost:5290/health/ready}"

if [[ ! -f "$ENV_FILE" ]]; then
  echo "ERROR: missing $ENV_FILE. Run ./scripts/setup-controlit-env.sh first." >&2
  exit 1
fi

cd "$ROOT_DIR"

backup_file="$ENV_FILE.backup.$(date +%Y%m%d%H%M%S)"
cp "$ENV_FILE" "$backup_file"

git fetch origin "$BRANCH"
git checkout "$BRANCH"
git pull --ff-only origin "$BRANCH"

./scripts/run-controlit-migrations.sh
./scripts/apply-controlit-db-user.sh
docker compose -f docker-compose.controlit.yml up -d --build

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
