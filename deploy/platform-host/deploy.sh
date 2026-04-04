#!/usr/bin/env bash
set -euo pipefail

PACKAGE_DIR="${1:-}"
BASE_DIR="${BASE_DIR:-/opt/atlas/platform-host}"
SERVICE_NAME="${SERVICE_NAME:-atlas-platformhost}"

if [[ -z "${PACKAGE_DIR}" ]]; then
  echo "Usage: $0 <platform-package-dir>" >&2
  exit 1
fi

if [[ ! -d "${PACKAGE_DIR}" ]]; then
  echo "Package directory not found: ${PACKAGE_DIR}" >&2
  exit 1
fi

timestamp="$(date -u +%Y%m%d%H%M%S)"
releases_dir="${BASE_DIR}/releases"
target_dir="${releases_dir}/release-${timestamp}"
current_link="${BASE_DIR}/current"
previous_link="${BASE_DIR}/previous"

mkdir -p "${releases_dir}"
cp -R "${PACKAGE_DIR}" "${target_dir}"

if [[ -L "${current_link}" ]]; then
  prev_target="$(readlink -f "${current_link}")"
  ln -sfn "${prev_target}" "${previous_link}"
fi

ln -sfn "${target_dir}" "${current_link}"

if command -v systemctl >/dev/null 2>&1; then
  systemctl daemon-reload || true
  systemctl restart "${SERVICE_NAME}"
  systemctl status "${SERVICE_NAME}" --no-pager
else
  echo "systemctl not found, skip service restart." >&2
fi

echo "PlatformHost deployed to ${target_dir}"
