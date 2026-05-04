#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${CONTROLIT_ENV_FILE:-$ROOT_DIR/.env}"
MYSQL_CONTAINER="${MYSQL_CONTAINER:-}"

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
  if [[ "$value" == *$'\n'* || "$value" == *$'\r'* ]]; then
    echo "ERROR: $name must not contain newlines." >&2
    exit 1
  fi
}

sql_quote() {
  local value="$1"
  value="${value//\\/\\\\}"
  value="${value//\'/\'\'}"
  printf "'%s'" "$value"
}

if [[ ! -f "$ENV_FILE" ]]; then
  echo "ERROR: missing $ENV_FILE. Run ./scripts/setup-controlit-env.sh first." >&2
  exit 1
fi

MYSQL_DATABASE="${MYSQL_DATABASE:-$(read_env MYSQL_DATABASE)}"
MYSQL_ROOT_PASSWORD="${MYSQL_ROOT_PASSWORD:-$(read_env MYSQL_ROOT_PASSWORD)}"
CONTROLIT_DB_USER="${CONTROLIT_DB_USER:-$(read_env CONTROLIT_DB_USER)}"
CONTROLIT_DB_PASSWORD="${CONTROLIT_DB_PASSWORD:-$(read_env CONTROLIT_DB_PASSWORD)}"
MYSQL_CONTAINER="${MYSQL_CONTAINER:-$(read_env MYSQL_CONTAINER)}"
CONTROLIT_DB_USER="${CONTROLIT_DB_USER:-controlit_api}"
MYSQL_CONTAINER="${MYSQL_CONTAINER:-mysql-container}"

require_safe_identifier "MYSQL_DATABASE" "$MYSQL_DATABASE"
require_safe_identifier "CONTROLIT_DB_USER" "$CONTROLIT_DB_USER"
require_safe_secret "CONTROLIT_DB_PASSWORD" "$CONTROLIT_DB_PASSWORD"

if [[ -z "$MYSQL_ROOT_PASSWORD" || -z "$CONTROLIT_DB_PASSWORD" ]]; then
  echo "ERROR: MYSQL_ROOT_PASSWORD and CONTROLIT_DB_PASSWORD are required." >&2
  exit 1
fi

CONTROLIT_USER_SQL="$(sql_quote "$CONTROLIT_DB_USER")@'%'"

docker exec -i "$MYSQL_CONTAINER" mysql -uroot -p"$MYSQL_ROOT_PASSWORD" <<SQL
CREATE USER IF NOT EXISTS $(sql_quote "$CONTROLIT_DB_USER")@'%' IDENTIFIED BY $(sql_quote "$CONTROLIT_DB_PASSWORD");
ALTER USER $(sql_quote "$CONTROLIT_DB_USER")@'%' IDENTIFIED BY $(sql_quote "$CONTROLIT_DB_PASSWORD");
GRANT SELECT ON \`$MYSQL_DATABASE\`.* TO $CONTROLIT_USER_SQL;
FLUSH PRIVILEGES;
SQL

controlit_tables="$(
  docker exec -i "$MYSQL_CONTAINER" mysql -N -uroot -p"$MYSQL_ROOT_PASSWORD" \
    -e "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = $(sql_quote "$MYSQL_DATABASE") AND TABLE_NAME LIKE 'controlit\\\\_%';"
)"

if [[ -z "$controlit_tables" ]]; then
  echo "ERROR: no controlit_* tables found. Run ./scripts/run-controlit-migrations.sh first." >&2
  exit 1
fi

while IFS= read -r table_name; do
  [[ -z "$table_name" ]] && continue
  require_safe_identifier "controlit table name" "$table_name"
  docker exec -i "$MYSQL_CONTAINER" mysql -uroot -p"$MYSQL_ROOT_PASSWORD" \
    -e "GRANT SELECT, INSERT, UPDATE, DELETE ON \`$MYSQL_DATABASE\`.\`$table_name\` TO $CONTROLIT_USER_SQL;"
done <<< "$controlit_tables"

docker exec -i "$MYSQL_CONTAINER" mysql -uroot -p"$MYSQL_ROOT_PASSWORD" -e "FLUSH PRIVILEGES;"

echo "ControlIT runtime DB user ready: $CONTROLIT_DB_USER"
