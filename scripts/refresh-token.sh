#!/usr/bin/env bash
# refresh-token.sh — Sync the NetLock remote_session_token into dotnet user-secrets
#
# NetLock regenerates remote_session_token in the accounts table on every container
# restart. Run this script from the repo root after any Docker restart:
#
#   ./scripts/refresh-token.sh
#
# The script:
#   1. Reads remote_session_token from MySQL via docker exec (no host credentials needed)
#   2. Writes NetLock:AdminSessionToken to dotnet user-secrets (Development only)
#   3. Prints a confirmation with the first 6 characters of the token

set -euo pipefail

CONTAINER="mysql-container"
ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
ENV_FILE="${CONTROLIT_ENV_FILE:-$ROOT_DIR/.env}"
DB="${MYSQL_DATABASE:-}"
MYSQL_ROOT_PASSWORD="${MYSQL_ROOT_PASSWORD:-}"
PROJECT="$(cd "$(dirname "$0")/.." && pwd)/src/ControlIT.Api/ControlIT.Api.csproj"

read_env() {
  local key="$1"
  awk -F= -v key="$key" '$1 == key { sub(/^[^=]*=/, ""); print; exit }' "$ENV_FILE"
}

if [[ -z "$DB" && -f "$ENV_FILE" ]]; then
  DB="$(read_env MYSQL_DATABASE)"
fi

if [[ -z "$MYSQL_ROOT_PASSWORD" && -f "$ENV_FILE" ]]; then
  MYSQL_ROOT_PASSWORD="$(read_env MYSQL_ROOT_PASSWORD)"
fi

if [[ -z "$DB" || -z "$MYSQL_ROOT_PASSWORD" ]]; then
  echo "ERROR: MYSQL_DATABASE and MYSQL_ROOT_PASSWORD are required in .env or environment." >&2
  exit 1
fi

# ── Fetch token from running container ───────────────────────────────────────
TOKEN=$(docker exec "$CONTAINER" \
  mysql -u root -p"$MYSQL_ROOT_PASSWORD" "$DB" \
  --skip-column-names --silent \
  -e "SELECT remote_session_token FROM accounts LIMIT 1;" \
  2>/dev/null | tr -d '\r\n')

if [[ -z "$TOKEN" ]]; then
  echo "ERROR: could not read remote_session_token — is $CONTAINER running?" >&2
  exit 1
fi

# ── Write token to dotnet user-secrets ───────────────────────────────────────
dotnet user-secrets set "NetLock:AdminSessionToken" "$TOKEN" --project "$PROJECT"

# ── Confirmation — show only first 6 chars ───────────────────────────────────
MASKED="${TOKEN:0:6}..."
echo "Token refreshed. NetLock:AdminSessionToken set to: ${MASKED}"
echo "Stored in: dotnet user-secrets (project: $PROJECT)"
