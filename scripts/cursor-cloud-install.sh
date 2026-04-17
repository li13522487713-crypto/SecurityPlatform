#!/usr/bin/env bash
# Cloud Agent 环境安装脚本（幂等，支持 Cursor 缓存复用）
# Cursor 会在拉取最新代码后自动执行本脚本
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FRONTEND_DIR="$ROOT_DIR/src/frontend"

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

if ! command -v pnpm >/dev/null 2>&1; then
  echo "Installing pnpm via corepack..." >&2
  corepack enable
  corepack prepare pnpm@latest --activate
fi

dotnet restore "$ROOT_DIR/Atlas.SecurityPlatform.slnx"

cd "$FRONTEND_DIR"
# 优先按 lockfile + 离线 store 安装；失败则回落允许更新 lockfile，避免离线缓存
# 缺失少量 metadata 时整体阻塞
if ! pnpm install --frozen-lockfile --prefer-offline; then
  echo "frozen install failed, fallback to lockfile-update install" >&2
  pnpm install --prefer-offline
fi

# 检查依赖图是否还能去重（任何重复版本都会导致 install/build 时间膨胀，并可能
# 重新引入像 @swc/helpers 0.4.x 的传递性 metadata 阻塞链）
pnpm dedupe --check
