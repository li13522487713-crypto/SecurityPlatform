#!/usr/bin/env node
/**
 * 在 CI/本地无 vitest 时，可用 node 调起 pnpm 运行契约测试：
 *   node scripts/verify-microflow-contracts.mjs
 * 需在 src/frontend 目录执行。
 */
import { spawnSync } from "node:child_process";

const r = spawnSync("pnpm", ["--filter", "@atlas/mendix-studio-core", "run", "verify-contracts"], {
  stdio: "inherit",
  shell: true
});
process.exit(r.status ?? 1);
