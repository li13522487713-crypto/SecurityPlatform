/**
 * P0-10: 一键串联 P0 阶段全部静态验证脚本，便于 CI 与本地回归。
 *
 * 目前不调用 `dotnet test`（让 CI 单独以矩阵方式跑后端集成测试），
 * 仅串联：
 *   1. verify-microflow-production-no-mock.ts
 *   2. verify-microflow-runtime-coverage.ts
 *   3. verify-microflow-production-readiness.ts（live health 默认跳过；
 *      由 MICROFLOW_READINESS_SKIP_LIVE_HEALTH=0 显式开启）
 *
 * 任一脚本 fail 整个流程返非零。
 */

import { spawnSync } from "node:child_process";

interface Step {
  name: string;
  command: string[];
  env?: Record<string, string | undefined>;
}

const steps: Step[] = [
  {
    name: "verify-microflow-production-no-mock",
    command: ["node", "scripts/verify-microflow-production-no-mock.ts"],
  },
  {
    name: "verify-microflow-runtime-coverage",
    command: ["node", "scripts/verify-microflow-runtime-coverage.ts"],
  },
  {
    name: "verify-microflow-production-readiness",
    command: ["node", "scripts/verify-microflow-production-readiness.ts"],
    env: {
      MICROFLOW_READINESS_SKIP_BUILDS: process.env.MICROFLOW_READINESS_SKIP_BUILDS ?? "1",
      MICROFLOW_READINESS_SKIP_LIVE_HEALTH: process.env.MICROFLOW_READINESS_SKIP_LIVE_HEALTH ?? "1",
    },
  },
];

let failed = 0;
for (const step of steps) {
  const env = { ...process.env, ...(step.env ?? {}) } as NodeJS.ProcessEnv;
  console.log(`\n=== ${step.name} ===`);
  const result = spawnSync(step.command[0], step.command.slice(1), {
    stdio: "inherit",
    env,
    shell: process.platform === "win32",
    timeout: 10 * 60 * 1000,
  });
  if (result.status !== 0) {
    failed += 1;
    console.error(`!! ${step.name} exited ${result.status}`);
  }
}

if (failed > 0) {
  console.error(`\n${failed} P0 readiness step(s) failed.`);
  process.exit(1);
}
console.log("\nAll P0 readiness steps passed.");
