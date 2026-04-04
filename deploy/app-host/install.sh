#!/usr/bin/env bash
set -euo pipefail

TENANT_ID="${1:-}"
INSTANCE_ID="${2:-}"
PACKAGE_ZIP="${3:-}"
BASE_DIR="${BASE_DIR:-/opt/atlas}"

if [[ -z "${TENANT_ID}" || -z "${INSTANCE_ID}" || -z "${PACKAGE_ZIP}" ]]; then
  echo "Usage: $0 <tenant-id> <instance-id> <package-zip>" >&2
  exit 1
fi

if [[ ! -f "${PACKAGE_ZIP}" ]]; then
  echo "Package not found: ${PACKAGE_ZIP}" >&2
  exit 1
fi

instance_root="${BASE_DIR}/instances/${TENANT_ID}/${INSTANCE_ID}"
release_root="${instance_root}/releases"
release_id="release-$(date -u +%Y%m%d%H%M%S)"
target_release="${release_root}/${release_id}"

mkdir -p "${target_release}" "${instance_root}/config" "${instance_root}/logs" "${instance_root}/data" "${instance_root}/run"
unzip -oq "${PACKAGE_ZIP}" -d "${target_release}"

if [[ -L "${instance_root}/current" ]]; then
  ln -sfn "$(readlink -f "${instance_root}/current")" "${instance_root}/previous"
fi
ln -sfn "${target_release}" "${instance_root}/current"

echo "Installed app package to ${target_release}"
