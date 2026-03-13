#!/usr/bin/env bash
# Cloud Agent 环境安装脚本（幂等，支持 Cursor 缓存复用）
# Cursor 会在拉取最新代码后自动执行本脚本
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FRONTEND_DIR="$ROOT_DIR/src/frontend/Atlas.WebApp"

# 同步子模块（如有），确保代码完整（幂等）
if [[ -f "$ROOT_DIR/.gitmodules" ]]; then
  git submodule update --init --recursive
fi

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
