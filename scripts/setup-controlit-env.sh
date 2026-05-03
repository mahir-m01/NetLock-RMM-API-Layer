#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${CONTROLIT_ENV_FILE:-$ROOT_DIR/.env}"
EXAMPLE_FILE="$ROOT_DIR/.env.example"

if [[ ! -f "$EXAMPLE_FILE" ]]; then
  echo "ERROR: missing $EXAMPLE_FILE" >&2
  exit 1
fi

if [[ -e "$ENV_FILE" && "${CONTROLIT_ENV_FORCE:-}" != "1" ]]; then
  echo "ERROR: $ENV_FILE already exists. Set CONTROLIT_ENV_FORCE=1 to overwrite." >&2
  exit 1
fi

random_secret() {
  openssl rand -base64 48 | tr -d '\n'
}

CONTROLIT_JWT_SIGNING_KEY="${CONTROLIT_JWT_SIGNING_KEY:-$(random_secret)}"
CONTROLIT_DB_PASSWORD="${CONTROLIT_DB_PASSWORD:-$(random_secret)}"
CONTROLIT_BOOTSTRAP_PASSWORD="${CONTROLIT_BOOTSTRAP_PASSWORD:-$(random_secret)}"
CONTROLIT_BOOTSTRAP_EMAIL="${CONTROLIT_BOOTSTRAP_EMAIL:-admin@controlit.local}"

umask 077
tmp_file="$(mktemp "${ENV_FILE}.tmp.XXXXXX")"
trap 'rm -f "$tmp_file"' EXIT

while IFS= read -r line || [[ -n "$line" ]]; do
  case "$line" in
    CONTROLIT_JWT_SIGNING_KEY=*) echo "CONTROLIT_JWT_SIGNING_KEY=$CONTROLIT_JWT_SIGNING_KEY" ;;
    CONTROLIT_BOOTSTRAP_EMAIL=*) echo "CONTROLIT_BOOTSTRAP_EMAIL=$CONTROLIT_BOOTSTRAP_EMAIL" ;;
    CONTROLIT_BOOTSTRAP_PASSWORD=*) echo "CONTROLIT_BOOTSTRAP_PASSWORD=$CONTROLIT_BOOTSTRAP_PASSWORD" ;;
    CONTROLIT_DB_PASSWORD=*) echo "CONTROLIT_DB_PASSWORD=$CONTROLIT_DB_PASSWORD" ;;
    *) echo "$line" ;;
  esac
done < "$EXAMPLE_FILE" > "$tmp_file"

mv "$tmp_file" "$ENV_FILE"
chmod 600 "$ENV_FILE"
trap - EXIT

cat <<EOF
Created: $ENV_FILE

Bootstrap SuperAdmin credentials - shown once:
  email:    $CONTROLIT_BOOTSTRAP_EMAIL
  password: $CONTROLIT_BOOTSTRAP_PASSWORD

Next:
  1. Make sure NetLock/MySQL is already running. ControlIT setup does not install or start NetLock.
  2. Fill MYSQL_ROOT_PASSWORD, CONTROLIT_NETLOCK_TOKEN, CONTROLIT_NETLOCK_FILES_KEY, NETBIRD_BASE_URL, NETBIRD_TOKEN in .env.
  3. Run migrations once with privileged DB credentials.
  4. Create least-privilege runtime DB user with scripts/init-controlit-db-user.sql.

Keep .env private. It is ignored by git.
EOF
