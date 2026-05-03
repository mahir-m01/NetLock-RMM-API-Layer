#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${CONTROLIT_ENV_FILE:-$ROOT_DIR/.env}"
MYSQL_CONTAINER="${MYSQL_CONTAINER:-mysql-container}"

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

require_safe_secret() {
  local name="$1"
  local value="$2"
  if [[ ! "$value" =~ ^[A-Za-z0-9_+=/@:.,-]+$ ]]; then
    echo "ERROR: $name contains unsupported characters for non-interactive SQL setup." >&2
    exit 1
  fi
}

sql_quote() {
  printf "'%s'" "$1"
}

if [[ ! -f "$ENV_FILE" ]]; then
  echo "ERROR: missing $ENV_FILE. Run ./scripts/setup-controlit-env.sh first." >&2
  exit 1
fi

MYSQL_DATABASE="${MYSQL_DATABASE:-$(read_env MYSQL_DATABASE)}"
MYSQL_ROOT_PASSWORD="${MYSQL_ROOT_PASSWORD:-$(read_env MYSQL_ROOT_PASSWORD)}"
CONTROLIT_DB_USER="${CONTROLIT_DB_USER:-$(read_env CONTROLIT_DB_USER)}"
CONTROLIT_DB_PASSWORD="${CONTROLIT_DB_PASSWORD:-$(read_env CONTROLIT_DB_PASSWORD)}"

require_safe_identifier "MYSQL_DATABASE" "$MYSQL_DATABASE"
require_safe_identifier "CONTROLIT_DB_USER" "$CONTROLIT_DB_USER"
require_safe_secret "CONTROLIT_DB_PASSWORD" "$CONTROLIT_DB_PASSWORD"

if [[ -z "$MYSQL_ROOT_PASSWORD" || -z "$CONTROLIT_DB_PASSWORD" ]]; then
  echo "ERROR: MYSQL_ROOT_PASSWORD and CONTROLIT_DB_PASSWORD are required." >&2
  exit 1
fi

docker exec -i "$MYSQL_CONTAINER" mysql -uroot -p"$MYSQL_ROOT_PASSWORD" <<SQL
CREATE USER IF NOT EXISTS $(sql_quote "$CONTROLIT_DB_USER")@'%' IDENTIFIED BY $(sql_quote "$CONTROLIT_DB_PASSWORD");
GRANT SELECT ON \`$MYSQL_DATABASE\`.* TO $(sql_quote "$CONTROLIT_DB_USER")@'%';
GRANT SELECT, INSERT, UPDATE, DELETE ON \`$MYSQL_DATABASE\`.\`controlit_%\` TO $(sql_quote "$CONTROLIT_DB_USER")@'%';
FLUSH PRIVILEGES;
SQL

echo "ControlIT runtime DB user ready: $CONTROLIT_DB_USER"
