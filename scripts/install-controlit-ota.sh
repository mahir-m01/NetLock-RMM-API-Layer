#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${CONTROLIT_ENV_FILE:-$ROOT_DIR/.env}"
SERVICE_NAME="${CONTROLIT_OTA_SERVICE_NAME:-controlit-update}"
ON_CALENDAR="${CONTROLIT_OTA_ON_CALENDAR:-*-*-* 03:30:00}"
RANDOM_DELAY="${CONTROLIT_OTA_RANDOM_DELAY:-30m}"

if ! command -v systemctl >/dev/null 2>&1; then
  echo "ERROR: systemd not found. Run ./scripts/update-controlit.sh manually or use cron." >&2
  exit 1
fi

if [[ ! -f "$ENV_FILE" ]]; then
  echo "ERROR: missing $ENV_FILE. Run ./scripts/setup-controlit-env.sh first." >&2
  exit 1
fi

sudo tee "/etc/systemd/system/$SERVICE_NAME.service" >/dev/null <<EOF
[Unit]
Description=ControlIT OTA update
After=network-online.target docker.service
Wants=network-online.target

[Service]
Type=oneshot
WorkingDirectory=$ROOT_DIR
Environment=CONTROLIT_ENV_FILE=$ENV_FILE
ExecStart=$ROOT_DIR/scripts/update-controlit.sh
EOF

sudo tee "/etc/systemd/system/$SERVICE_NAME.timer" >/dev/null <<EOF
[Unit]
Description=Run ControlIT OTA update

[Timer]
OnCalendar=$ON_CALENDAR
RandomizedDelaySec=$RANDOM_DELAY
Persistent=true

[Install]
WantedBy=timers.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable --now "$SERVICE_NAME.timer"
sudo systemctl list-timers "$SERVICE_NAME.timer" --no-pager
