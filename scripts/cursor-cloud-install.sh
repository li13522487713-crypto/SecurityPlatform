#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FRONTEND_DIR="$ROOT_DIR/src/frontend/Atlas.WebApp"

if ! command -v node >/dev/null 2>&1; then
  echo "Node.js is required but not found." >&2
  exit 1
fi

NODE_MAJOR="$(node -p "process.versions.node.split('.')[0]")"
if [[ "$NODE_MAJOR" != "22" ]]; then
  echo "Node.js 22 is required. Current version: $(node -v)" >&2
  exit 1
fi

if ! command -v dotnet >/dev/null 2>&1; then
  echo ".NET SDK is required but not found." >&2
  exit 1
fi

dotnet restore "$ROOT_DIR/Atlas.SecurityPlatform.slnx"
npm install --prefix "$FRONTEND_DIR"
