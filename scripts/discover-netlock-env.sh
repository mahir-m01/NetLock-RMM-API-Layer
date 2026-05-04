#!/usr/bin/env bash
set -euo pipefail
export LC_ALL=C

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${CONTROLIT_ENV_FILE:-$ROOT_DIR/.env}"

read_env_file() {
  local file="$1"
  local key="$2"
  awk -F= -v key="$key" '
    $1 == key {
      sub(/^[^=]*=/, "")
      gsub(/\r$/, "")
      if ($0 ~ /^'\''.*'\''$/ || $0 ~ /^".*"$/) {
        print substr($0, 2, length($0) - 2)
      } else {
        print
      }
      exit
    }
  ' "$file"
}

read_env() {
  local key="$1"
  [[ -f "$ENV_FILE" ]] || return 0
  read_env_file "$ENV_FILE" "$key"
}

is_missing() {
  local value="${1:-}"
  [[ -z "$value" || "$value" == REPLACE_WITH_* || "$value" == GENERATED_BY_SETUP_SCRIPT ]]
}

require_identifier() {
  local name="$1"
  local value="$2"
  if [[ ! "$value" =~ ^[A-Za-z0-9_]+$ ]]; then
    echo "ERROR: discovered $name is not a safe identifier: $value" >&2
    exit 1
  fi
}

dotenv_value() {
  local value="$1"
  if [[ "$value" =~ ^[A-Za-z0-9_./:@,+%=-]+$ ]]; then
    printf '%s' "$value"
    return
  fi

  if [[ "$value" == *"'"* ]]; then
    echo "ERROR: discovered value contains a single quote and cannot be written safely. Set it manually in $ENV_FILE." >&2
    exit 1
  fi

  printf "'%s'" "$value"
}

set_env() {
  local key="$1"
  local value="$2"
  local rendered
  rendered="$(dotenv_value "$value")"

  if grep -q "^$key=" "$ENV_FILE"; then
    tmp_file="$(mktemp "${ENV_FILE}.tmp.XXXXXX")"
    awk -v key="$key" -v replacement="$key=$rendered" '
      BEGIN { replaced = 0 }
      $0 ~ "^" key "=" {
        if (!replaced) {
          print replacement
          replaced = 1
        }
        next
      }
      { print }
      END {
        if (!replaced) {
          print replacement
        }
      }
    ' "$ENV_FILE" > "$tmp_file"
    mv "$tmp_file" "$ENV_FILE"
    chmod 600 "$ENV_FILE"
  else
    printf '\n%s=%s\n' "$key" "$rendered" >> "$ENV_FILE"
  fi
}

find_netlock_env_file() {
  local explicit="${CONTROLIT_NETLOCK_ENV_FILE:-}"
  if [[ -n "$explicit" && -f "$explicit" ]]; then
    echo "$explicit"
    return
  fi

  local candidates=(
    "$ROOT_DIR/../netlock/.env"
    "$ROOT_DIR/../netlock-rmm/.env"
    "$ROOT_DIR/../NetLock/.env"
    "$ROOT_DIR/../NetLock-RMM/.env"
    "$ROOT_DIR/../NetLock-RMM-API-Layer/.env"
  )

  local candidate
  for candidate in "${candidates[@]}"; do
    if [[ -f "$candidate" && "$(cd "$(dirname "$candidate")" && pwd)/$(basename "$candidate")" != "$(cd "$(dirname "$ENV_FILE")" && pwd)/$(basename "$ENV_FILE")" ]]; then
      if grep -q '^MYSQL_ROOT_PASSWORD=' "$candidate"; then
        echo "$candidate"
        return
      fi
    fi
  done

  while IFS= read -r candidate; do
    if [[ "$(cd "$(dirname "$candidate")" && pwd)/$(basename "$candidate")" == "$(cd "$(dirname "$ENV_FILE")" && pwd)/$(basename "$ENV_FILE")" ]]; then
      continue
    fi
    if grep -q '^MYSQL_ROOT_PASSWORD=' "$candidate"; then
      echo "$candidate"
      return
    fi
  done < <(find "$ROOT_DIR/.." -maxdepth 2 -name .env -type f 2>/dev/null | sort)
}

detect_network() {
  local mysql_container="$1"
  local configured
  configured="$(read_env NETLOCK_DOCKER_NETWORK)"
  if [[ -n "$configured" ]] && docker network inspect "$configured" >/dev/null 2>&1; then
    echo "$configured"
    return
  fi

  local networks
  networks="$(docker inspect -f '{{range $name, $_ := .NetworkSettings.Networks}}{{println $name}}{{end}}' "$mysql_container" 2>/dev/null || true)"
  if [[ -z "$networks" ]]; then
    return 1
  fi

  local network
  while IFS= read -r network; do
    [[ -z "$network" ]] && continue
    if [[ "$network" == *netlock* || "$network" == *NetLock* ]]; then
      echo "$network"
      return
    fi
  done <<< "$networks"

  echo "$networks" | awk 'NF { print; exit }'
}

detect_netlock_server() {
  local network="$1"
  if docker inspect netlock-rmm-server >/dev/null 2>&1; then
    echo "netlock-rmm-server"
    return
  fi

  docker network inspect -f '{{range .Containers}}{{println .Name}}{{end}}' "$network" 2>/dev/null \
    | awk 'tolower($0) ~ /netlock/ && tolower($0) ~ /server/ { print; exit }'
}

query_mysql() {
  local mysql_container="$1"
  local root_password="$2"
  local database="$3"
  local sql="$4"
  docker exec -i "$mysql_container" mysql -N -s -uroot -p"$root_password" "$database" -e "$sql" 2>/dev/null \
    | awk 'NF { print; exit }' \
    | LC_ALL=C tr -d '\r'
}

if [[ ! -f "$ENV_FILE" ]]; then
  echo "ERROR: missing $ENV_FILE. Run ./scripts/setup-controlit-env.sh first." >&2
  exit 1
fi

netlock_env_file="$(find_netlock_env_file || true)"

mysql_root_password="$(read_env MYSQL_ROOT_PASSWORD)"
mysql_database="$(read_env MYSQL_DATABASE)"
mysql_container="$(read_env MYSQL_CONTAINER)"

if [[ -n "$netlock_env_file" ]]; then
  if is_missing "$mysql_root_password"; then
    mysql_root_password="$(read_env_file "$netlock_env_file" MYSQL_ROOT_PASSWORD)"
  fi
  if is_missing "$mysql_database"; then
    mysql_database="$(read_env_file "$netlock_env_file" MYSQL_DATABASE)"
  fi
fi

mysql_database="${mysql_database:-iphbmh}"
mysql_container="${mysql_container:-mysql-container}"

if is_missing "$mysql_root_password"; then
  echo "ERROR: could not auto-discover MYSQL_ROOT_PASSWORD." >&2
  echo "Install ControlIT beside the NetLock folder, or set CONTROLIT_NETLOCK_ENV_FILE=/path/to/netlock/.env, then rerun." >&2
  exit 1
fi

if ! docker inspect "$mysql_container" >/dev/null 2>&1; then
  echo "ERROR: NetLock MySQL container not found: $mysql_container" >&2
  echo "Set MYSQL_CONTAINER in $ENV_FILE if your NetLock container uses a different name." >&2
  exit 1
fi

require_identifier "MYSQL_DATABASE" "$mysql_database"

network_name="$(detect_network "$mysql_container" || true)"
if [[ -z "$network_name" ]]; then
  echo "ERROR: could not detect NetLock Docker network from $mysql_container." >&2
  echo "Set NETLOCK_DOCKER_NETWORK in $ENV_FILE and rerun." >&2
  exit 1
fi

netlock_server="$(detect_netlock_server "$network_name")"
netlock_server="${netlock_server:-netlock-rmm-server}"

remote_session_token="$(read_env CONTROLIT_NETLOCK_TOKEN)"
if is_missing "$remote_session_token"; then
  remote_session_token="$(
    query_mysql "$mysql_container" "$mysql_root_password" "$mysql_database" \
      "SELECT remote_session_token FROM accounts WHERE role = 'Administrator' AND remote_session_token IS NOT NULL AND remote_session_token <> '' ORDER BY id LIMIT 1;"
  )"
fi

files_api_key="$(read_env CONTROLIT_NETLOCK_FILES_KEY)"
if is_missing "$files_api_key"; then
  files_api_key="$(
    query_mysql "$mysql_container" "$mysql_root_password" "$mysql_database" \
      "SELECT files_api_key FROM settings WHERE files_api_key IS NOT NULL AND files_api_key <> '' ORDER BY id LIMIT 1;"
  )"
fi

if is_missing "$remote_session_token"; then
  echo "ERROR: no NetLock administrator remote_session_token was found." >&2
  echo "Login to the NetLock console once as an administrator, then rerun this installer." >&2
  exit 1
fi

if is_missing "$files_api_key"; then
  echo "ERROR: no NetLock files_api_key was found in settings." >&2
  echo "Confirm the NetLock server completed its initial setup, then rerun this installer." >&2
  exit 1
fi

set_env MYSQL_ROOT_PASSWORD "$mysql_root_password"
set_env MYSQL_DATABASE "$mysql_database"
set_env MYSQL_CONTAINER "$mysql_container"
set_env CONTROLIT_DB_HOST "$mysql_container"
set_env CONTROLIT_DB_PORT "$(read_env CONTROLIT_DB_PORT || true)"
set_env NETLOCK_DOCKER_NETWORK "$network_name"
set_env CONTROLIT_NETLOCK_TOKEN "$remote_session_token"
set_env CONTROLIT_NETLOCK_FILES_KEY "$files_api_key"
set_env CONTROLIT_NETLOCK_HUB_URL "http://$netlock_server:7080/commandHub"

if is_missing "$(read_env CONTROLIT_DB_PORT)"; then
  set_env CONTROLIT_DB_PORT "3306"
fi

echo "NetLock auto-discovery complete."
echo "  MySQL container: $mysql_container"
echo "  MySQL database:  $mysql_database"
echo "  Docker network:  $network_name"
echo "  SignalR hub:     http://$netlock_server:7080/commandHub"
echo "Secrets were written to $ENV_FILE and were not printed."
