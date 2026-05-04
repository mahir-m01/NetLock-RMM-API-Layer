#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${CONTROLIT_ENV_FILE:-$ROOT_DIR/.env}"
COMPOSE_FILE="${CONTROLIT_COMPOSE_FILE:-$ROOT_DIR/docker-compose.controlit.yml}"

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
CONTROLIT_DB_HOST="${CONTROLIT_DB_HOST:-$(read_env CONTROLIT_DB_HOST)}"
CONTROLIT_DB_PORT="${CONTROLIT_DB_PORT:-$(read_env CONTROLIT_DB_PORT)}"

require_safe_identifier "MYSQL_DATABASE" "$MYSQL_DATABASE"

if [[ -z "$MYSQL_ROOT_PASSWORD" ]]; then
  echo "ERROR: MYSQL_ROOT_PASSWORD is required." >&2
  exit 1
fi

CONTROLIT_DB_HOST="${CONTROLIT_DB_HOST:-mysql-container}"
CONTROLIT_DB_PORT="${CONTROLIT_DB_PORT:-3306}"

CONTROLIT_DB_CONNECTION="Server=$CONTROLIT_DB_HOST;Port=$CONTROLIT_DB_PORT;Database=$MYSQL_DATABASE;User=root;Password=$MYSQL_ROOT_PASSWORD;"

docker compose -f "$COMPOSE_FILE" run \
  --rm \
  --build \
  -e CONTROLIT_MIGRATE_ONLY=true \
  -e CONTROLIT_AUTO_MIGRATE=true \
  -e CONTROLIT_DB_CONNECTION="$CONTROLIT_DB_CONNECTION" \
  controlit-api

echo "ControlIT EF migrations applied with privileged DB credentials."
