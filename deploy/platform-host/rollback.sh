#!/usr/bin/env bash
set -euo pipefail

BASE_DIR="${BASE_DIR:-/opt/atlas/platform-host}"
SERVICE_NAME="${SERVICE_NAME:-atlas-platformhost}"
current_link="${BASE_DIR}/current"
previous_link="${BASE_DIR}/previous"

if [[ ! -L "${previous_link}" ]]; then
  echo "Previous release link not found: ${previous_link}" >&2
  exit 1
fi

current_target="$(readlink -f "${current_link}" || true)"
previous_target="$(readlink -f "${previous_link}")"

if [[ -z "${previous_target}" || ! -d "${previous_target}" ]]; then
  echo "Previous target invalid: ${previous_target}" >&2
  exit 1
fi

ln -sfn "${previous_target}" "${current_link}"
if [[ -n "${current_target}" && -d "${current_target}" ]]; then
  ln -sfn "${current_target}" "${previous_link}"
fi

if command -v systemctl >/dev/null 2>&1; then
  systemctl daemon-reload || true
  systemctl restart "${SERVICE_NAME}"
  systemctl status "${SERVICE_NAME}" --no-pager
else
  echo "systemctl not found, skip service restart." >&2
fi

echo "PlatformHost rollback completed to ${previous_target}"
