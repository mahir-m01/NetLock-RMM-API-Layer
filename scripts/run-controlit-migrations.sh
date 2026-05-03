#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${CONTROLIT_ENV_FILE:-$ROOT_DIR/.env}"

read_env() {
  local key="$1"
  awk -F= -v key="$key" '$1 == key { sub(/^[^=]*=/, ""); print; exit }' "$ENV_FILE"
}

require_safe_identifier() {
  local name="$1"
  local value="$2"
  if [[ ! "$value" =~ ^[A-Za-z0-9_]+$ ]]; then
    echo "ERROR: $name must contain only letters, numbers, and underscore." >&2
    exit 1
  fi
}

if [[ ! -f "$ENV_FILE" ]]; then
  echo "ERROR: missing $ENV_FILE. Run ./scripts/setup-controlit-env.sh first." >&2
  exit 1
fi

MYSQL_DATABASE="${MYSQL_DATABASE:-$(read_env MYSQL_DATABASE)}"
MYSQL_ROOT_PASSWORD="${MYSQL_ROOT_PASSWORD:-$(read_env MYSQL_ROOT_PASSWORD)}"

require_safe_identifier "MYSQL_DATABASE" "$MYSQL_DATABASE"

if [[ -z "$MYSQL_ROOT_PASSWORD" ]]; then
  echo "ERROR: MYSQL_ROOT_PASSWORD is required." >&2
  exit 1
fi

CONTROLIT_DB_CONNECTION="Server=172.18.0.3;Port=3306;Database=$MYSQL_DATABASE;User=root;Password=$MYSQL_ROOT_PASSWORD;"

docker compose -f "$ROOT_DIR/docker-compose.controlit.yml" run \
  --rm \
  --build \
  -e CONTROLIT_MIGRATE_ONLY=true \
  -e CONTROLIT_AUTO_MIGRATE=true \
  -e CONTROLIT_DB_CONNECTION="$CONTROLIT_DB_CONNECTION" \
  controlit-api

echo "ControlIT EF migrations applied with privileged DB credentials."
