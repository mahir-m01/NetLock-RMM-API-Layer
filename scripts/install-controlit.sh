#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${CONTROLIT_ENV_FILE:-$ROOT_DIR/.env}"
COMPOSE_FILE="${CONTROLIT_COMPOSE_FILE:-$ROOT_DIR/docker-compose.yml}"
HEALTH_URL="${CONTROLIT_HEALTH_URL:-http://localhost:5290/health/ready}"

read_env() {
  local key="$1"
  awk -F= -v key="$key" '$1 == key { sub(/^[^=]*=/, ""); print; exit }' "$ENV_FILE"
}

require_env() {
  local key="$1"
  local value
  value="$(read_env "$key")"
  if [[ -z "$value" || "$value" == REPLACE_WITH_* || "$value" == GENERATED_BY_SETUP_SCRIPT ]]; then
    echo "ERROR: set $key in $ENV_FILE" >&2
    exit 1
  fi
}

if [[ ! -f "$ENV_FILE" ]]; then
  "$ROOT_DIR/scripts/setup-controlit-env.sh"
fi

require_env MYSQL_ROOT_PASSWORD
require_env MYSQL_DATABASE
require_env CONTROLIT_DB_HOST
require_env CONTROLIT_NETLOCK_TOKEN
require_env CONTROLIT_NETLOCK_FILES_KEY
require_env CONTROLIT_NETLOCK_HUB_URL
require_env CONTROLIT_PUBLIC_API_URL
require_env CONTROLIT_ALLOWED_ORIGINS
require_env NETBIRD_BASE_URL
require_env NETBIRD_TOKEN

network_name="$(read_env NETLOCK_DOCKER_NETWORK)"
if [[ -n "$network_name" ]] && ! docker network inspect "$network_name" >/dev/null 2>&1; then
  echo "ERROR: Docker network not found: $network_name" >&2
  echo "Set NETLOCK_DOCKER_NETWORK to the existing NetLock Docker network." >&2
  exit 1
fi

"$ROOT_DIR/scripts/run-controlit-migrations.sh"
"$ROOT_DIR/scripts/apply-controlit-db-user.sh"
docker compose -f "$COMPOSE_FILE" up -d --build

for attempt in {1..30}; do
  if curl -fsS "$HEALTH_URL" >/dev/null; then
    echo "ControlIT install complete. Readiness check passed: $HEALTH_URL"
    exit 0
  fi

  sleep 2
done

echo "ERROR: ControlIT started but readiness check did not pass: $HEALTH_URL" >&2
exit 1
