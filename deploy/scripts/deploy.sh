#!/usr/bin/env bash
set -euo pipefail

COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.yml}"
ENV_FILE="${ENV_FILE:-deploy/.env.deploy}"
HEALTH_URL="${HEALTH_URL:-http://127.0.0.1/api/v1/health}"
MAX_ATTEMPTS="${MAX_ATTEMPTS:-30}"
IMAGE_TAG="${IMAGE_TAG:-${1:-}}"
CORS_ALLOWED_ORIGIN="${CORS_ALLOWED_ORIGIN:-http://localhost}"

if [[ -z "${IMAGE_TAG}" ]]; then
  echo "IMAGE_TAG is required. Use IMAGE_TAG=<tag> ./deploy/scripts/deploy.sh" >&2
  exit 1
fi

if [[ -z "${JWT_SIGNING_KEY:-}" ]]; then
  echo "JWT_SIGNING_KEY is required and must be provided via environment." >&2
  exit 1
fi

if [[ "${JWT_SIGNING_KEY:-}" == *"CHANGE_ME"* ]]; then
  echo "JWT_SIGNING_KEY must not contain placeholder value (CHANGE_ME)." >&2
  exit 1
fi

if [[ -z "${BOOTSTRAP_ADMIN_PASSWORD:-}" ]]; then
  echo "BOOTSTRAP_ADMIN_PASSWORD is required and must be provided via environment." >&2
  exit 1
fi

if [[ "${BOOTSTRAP_ADMIN_PASSWORD:-}" == *"CHANGE_ME"* ]]; then
  echo "BOOTSTRAP_ADMIN_PASSWORD must not contain placeholder value (CHANGE_ME)." >&2
  exit 1
fi

for cmd in docker curl; do
  command -v "${cmd}" >/dev/null 2>&1 || { echo "Missing command: ${cmd}" >&2; exit 1; }
done

docker compose version >/dev/null

run_root() {
  if command -v sudo >/dev/null 2>&1; then
    sudo "$@"
  else
    "$@"
  fi
}

run_root mkdir -p /opt/atlas/data /opt/atlas/uploads /opt/atlas/plugins /opt/atlas/logs/backend /opt/atlas/logs/nginx

extract_tag() {
  local image_ref="$1"
  if [[ -z "${image_ref}" ]]; then
    echo ""
    return 0
  fi
  if [[ "${image_ref}" == *":"* ]]; then
    echo "${image_ref##*:}"
  else
    echo ""
  fi
}

current_backend_tag=""
current_frontend_tag=""

backend_cid="$(docker compose -f "${COMPOSE_FILE}" ps -q backend 2>/dev/null || true)"
if [[ -n "${backend_cid}" ]]; then
  backend_image_ref="$(docker inspect --format '{{.Config.Image}}' "${backend_cid}")"
  current_backend_tag="$(extract_tag "${backend_image_ref}")"
fi

frontend_cid="$(docker compose -f "${COMPOSE_FILE}" ps -q frontend 2>/dev/null || true)"
if [[ -n "${frontend_cid}" ]]; then
  frontend_image_ref="$(docker inspect --format '{{.Config.Image}}' "${frontend_cid}")"
  current_frontend_tag="$(extract_tag "${frontend_image_ref}")"
fi

cat > "${ENV_FILE}" <<EOF
BACKEND_IMAGE_TAG=${IMAGE_TAG}
FRONTEND_IMAGE_TAG=${IMAGE_TAG}
PREVIOUS_BACKEND_IMAGE_TAG=${current_backend_tag}
PREVIOUS_FRONTEND_IMAGE_TAG=${current_frontend_tag}
CORS_ALLOWED_ORIGIN=${CORS_ALLOWED_ORIGIN}
EOF

echo "Deploying tag ${IMAGE_TAG}..."
docker compose -f "${COMPOSE_FILE}" --env-file "${ENV_FILE}" build --pull backend frontend
docker compose -f "${COMPOSE_FILE}" --env-file "${ENV_FILE}" up -d --remove-orphans backend frontend nginx

for i in $(seq 1 "${MAX_ATTEMPTS}"); do
  if curl -fsS "${HEALTH_URL}" >/tmp/atlas-health.json; then
    echo "Deployment succeeded. Health endpoint is ready."
    cat /tmp/atlas-health.json
    exit 0
  fi
  echo "Health check failed (${i}/${MAX_ATTEMPTS}), retrying in 2s..."
  sleep 2
done

echo "Deployment health check failed. Starting rollback..." >&2
bash deploy/scripts/rollback.sh
exit 1
