#!/usr/bin/env bash
set -euo pipefail

TENANT_ID="${1:-}"
INSTANCE_ID="${2:-}"
TARGET_RELEASE_DIR="${3:-}"
BASE_DIR="${BASE_DIR:-/opt/atlas}"

if [[ -z "${TENANT_ID}" || -z "${INSTANCE_ID}" ]]; then
  echo "Usage: $0 <tenant-id> <instance-id> [target-release-dir]" >&2
  exit 1
fi

instance_root="${BASE_DIR}/instances/${TENANT_ID}/${INSTANCE_ID}"
current_link="${instance_root}/current"
previous_link="${instance_root}/previous"

if [[ -z "${TARGET_RELEASE_DIR}" ]]; then
  if [[ ! -L "${previous_link}" ]]; then
    echo "No previous release to rollback: ${previous_link}" >&2
    exit 1
  fi
  TARGET_RELEASE_DIR="$(readlink -f "${previous_link}")"
fi

if [[ ! -d "${TARGET_RELEASE_DIR}" ]]; then
  echo "Target release directory not found: ${TARGET_RELEASE_DIR}" >&2
  exit 1
fi

if [[ -L "${current_link}" ]]; then
  ln -sfn "$(readlink -f "${current_link}")" "${previous_link}"
fi
ln -sfn "${TARGET_RELEASE_DIR}" "${current_link}"

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
bash "${script_dir}/restart-instance.sh" "${INSTANCE_ID}"

echo "Rollback completed: instance=${INSTANCE_ID}, current=${TARGET_RELEASE_DIR}"
