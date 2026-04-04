#!/usr/bin/env bash
set -euo pipefail

INSTANCE_ID="${1:-}"
SERVICE_PREFIX="${SERVICE_PREFIX:-atlas-apphost}"

if [[ -z "${INSTANCE_ID}" ]]; then
  echo "Usage: $0 <instance-id>" >&2
  exit 1
fi

if command -v systemctl >/dev/null 2>&1; then
  systemctl stop "${SERVICE_PREFIX}@${INSTANCE_ID}"
  systemctl status "${SERVICE_PREFIX}@${INSTANCE_ID}" --no-pager || true
else
  echo "systemctl not found, skip stop." >&2
  exit 1
fi
