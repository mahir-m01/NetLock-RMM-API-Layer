#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BRANCH="${CONTROLIT_RELEASE_BRANCH:-production}"

cd "$ROOT_DIR"

git fetch --quiet origin "$BRANCH"

local_commit="$(git rev-parse HEAD)"
remote_commit="$(git rev-parse "origin/$BRANCH")"

echo "current=$local_commit"
echo "available=$remote_commit"

if [[ "$local_commit" == "$remote_commit" ]]; then
  echo "status=current"
  exit 0
fi

echo "status=update_available"
