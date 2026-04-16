#!/usr/bin/env bash
# refresh-token.sh — Sync the NetLock remote_session_token into appsettings.Development.json
#
# NetLock regenerates remote_session_token in the accounts table on every container
# restart. Run this script from the repo root after any Docker restart:
#
#   ./scripts/refresh-token.sh
#
# The script:
#   1. Reads remote_session_token from MySQL via docker exec (no host credentials needed)
#   2. Patches NetLock:AdminSessionToken in appsettings.Development.json
#   3. Prints a confirmation with the first 6 characters of the token masked

set -euo pipefail

CONTAINER="mysql-container"
DB="iphbmh"
APPSETTINGS="$(cd "$(dirname "$0")/.." && pwd)/src/ControlIT.Api/appsettings.Development.json"

# ── Fetch token from running container ───────────────────────────────────────
TOKEN=$(docker exec "$CONTAINER" \
  mysql -u root -pEuMmvIqcJjafr6fb "$DB" \
  --skip-column-names --silent \
  -e "SELECT remote_session_token FROM accounts LIMIT 1;" \
  2>/dev/null | tr -d '\r\n')

if [[ -z "$TOKEN" ]]; then
  echo "ERROR: could not read remote_session_token — is $CONTAINER running?" >&2
  exit 1
fi

# ── Patch appsettings.Development.json ───────────────────────────────────────
# Uses sed to replace the AdminSessionToken value in-place.
# Matches:  "AdminSessionToken": "<anything>"
# Handles tokens that contain special sed characters by escaping & / \ in the value.
ESCAPED_TOKEN=$(printf '%s\n' "$TOKEN" | sed 's/[&/\]/\\&/g')

sed -i.bak \
  "s|\"AdminSessionToken\":.*|\"AdminSessionToken\": \"${ESCAPED_TOKEN}\"|" \
  "$APPSETTINGS"

rm -f "${APPSETTINGS}.bak"

# ── Confirmation — show only first 6 chars ───────────────────────────────────
MASKED="${TOKEN:0:6}..."
echo "Token refreshed. AdminSessionToken set to: ${MASKED}"
echo "File updated: $APPSETTINGS"
