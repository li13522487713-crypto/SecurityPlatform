# 第 60 轮内测准入结论

## 准入判断

本文件由第 60 轮总控回归维护。是否进入第 61 轮生产准备，以本地生成的 `artifacts/microflow-e2e/round60/e2e-summary.md` 为最终证据。

建议准入条件：

1. `npx tsx scripts/verify-microflow-round60-full-e2e.ts` 完成，Blocker=0、Critical=0。
2. `dotnet build` 通过。
3. `pnpm --dir src/frontend run build:app-web` 通过。
4. AppHost `GET /internal/health/live` 与 `GET /api/microflows/health` 可用。
5. app-web 可启动并进入微流资源库/编辑器。
6. 报告中所有 Major 均有风险说明或后续修复计划。
7. `round60-known-limitations.md` 已更新，且没有把 blocker/critical 伪装成 known limitation。

## 第 61 轮建议重点

1. 生产运维硬化：统一健康检查、日志字段、traceId、错误码告警、运行证据归档。
2. 权限与租户：把微流 `[AllowAnonymous]` 路径纳入正式权限策略，覆盖 401/403 真实后端场景。
3. Runtime 稳定性：异步 TestRun/Job Queue、运行中 cancel、timeout finalizer、终态防覆盖。
4. 浏览器 E2E：补齐微流资源库、编辑器、ProblemPanel、DebugPanel、Modal/Drawer 的 Playwright 专项。
5. 性能基线：100+ 节点 schema load/save/validate/runtime plan/test-run/trace query 采样进入 CI 报告。
6. Legacy verify 清理：统一历史 verify 资源前缀与 cleanup 策略，降低本地数据库污染。
