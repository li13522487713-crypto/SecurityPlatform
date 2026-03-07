#!/usr/bin/env bash
set -euo pipefail

COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.yml}"
ENV_FILE="${ENV_FILE:-deploy/.env.deploy}"
HEALTH_URL="${HEALTH_URL:-http://127.0.0.1/api/v1/health}"
MAX_ATTEMPTS="${MAX_ATTEMPTS:-30}"

if [[ ! -f "${ENV_FILE}" ]]; then
  echo "Env file not found: ${ENV_FILE}" >&2
  exit 1
fi

# shellcheck disable=SC1090
source "${ENV_FILE}"

if [[ -z "${PREVIOUS_BACKEND_IMAGE_TAG:-}" || -z "${PREVIOUS_FRONTEND_IMAGE_TAG:-}" ]]; then
  echo "No previous image tags available. Rollback cannot continue." >&2
  exit 1
fi

failed_backend_tag="${BACKEND_IMAGE_TAG:-}"
failed_frontend_tag="${FRONTEND_IMAGE_TAG:-}"

tmp_env_file="$(mktemp)"
cat > "${tmp_env_file}" <<EOF
BACKEND_IMAGE_TAG=${PREVIOUS_BACKEND_IMAGE_TAG}
FRONTEND_IMAGE_TAG=${PREVIOUS_FRONTEND_IMAGE_TAG}
PREVIOUS_BACKEND_IMAGE_TAG=${failed_backend_tag}
PREVIOUS_FRONTEND_IMAGE_TAG=${failed_frontend_tag}
CORS_ALLOWED_ORIGIN=${CORS_ALLOWED_ORIGIN:-http://localhost}
EOF

echo "Rolling back to backend:${PREVIOUS_BACKEND_IMAGE_TAG}, frontend:${PREVIOUS_FRONTEND_IMAGE_TAG}..."
docker compose -f "${COMPOSE_FILE}" --env-file "${tmp_env_file}" up -d --remove-orphans backend frontend nginx

for i in $(seq 1 "${MAX_ATTEMPTS}"); do
  if curl -fsS "${HEALTH_URL}" >/tmp/atlas-rollback-health.json; then
    echo "Rollback succeeded. Health endpoint is ready."
    cat /tmp/atlas-rollback-health.json
    mv "${tmp_env_file}" "${ENV_FILE}"
    exit 0
  fi
  echo "Rollback health check failed (${i}/${MAX_ATTEMPTS}), retrying in 2s..."
  sleep 2
done

echo "Rollback failed: health endpoint still unavailable." >&2
rm -f "${tmp_env_file}"
exit 1
