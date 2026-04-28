# Inner Test Readiness

## Conclusion

结论：`conditional-go`。

允许进入受控内测 / 试点，但必须满足条件：使用真实 AppHost + 真实数据库配置，执行 no mock/readiness/build/health/migration 验证并保留报告；内测范围限制在单实例、受控 workspace、受控用户和非公网 RestCall。

## Gate

- Blocker: 0。
- Critical: 0。
- Major: 已列入风险接受清单。
- Production mock fallback: 禁止。
- FlowGram JSON persistence: 禁止。
- Resource / Schema、Metadata / Validation、Publish / Version / References、TestRun / Debug 主链路：以 Round60 报告为基线，Round61 只做准入复核。
- Cancel / Timeout: API 具备取消状态与超时配置；运行中协作式取消仍是限制。
- Health: 必须覆盖 api、storage、metadata、runtime。
- Backup / restore runbook: 完成。
- Known limitations: 完成。

## Risk Acceptance

- Major: RunSession / TraceFrame / RuntimeLog 暂无自动 retention job。接受条件：按 `ops-runbook.md` 执行手工 dry run + 前缀清理；生产不清理 Version / SchemaSnapshot / PublishSnapshot。
- Major: Cancel 当前主要保证会话状态可取消，不保证已进入同步执行段的即时中断。接受条件：设置 RunTimeoutSeconds / ActionTimeoutSeconds，并在用户文案中说明。
- Major: 审计第一版使用结构化 `ILogger` 和平台审计能力边界说明，不新增审计表。接受条件：发布、删除、rollback、testRun cancel 等操作日志可查询 traceId。
- Major: 浏览器级微流专项 E2E 不完整。接受条件：Round60 HTTP E2E + 本轮 readiness gate 通过，UI 内测由人工脚本补充。

## Required Evidence

- `artifacts/microflow-release/round61/readiness-summary.json`
- `artifacts/microflow-release/round61/readiness-summary.md`
- 后端 Release build 日志。
- 前端 production build 日志。
- `.http` 或自动化 health 验证记录。
- 数据库 schema 初始化 / migration 验证记录。

## No-Go Conditions

- 生产包中启用 MSW、mock adapter、local resource adapter 或 Contract Mock。
- 生产配置启用 metadata seed、默认 real HTTP、private network 或 internal debug API。
- 缺少 `X-Workspace-Id` 仍能在生产访问非 health 微流 API。
- 发布快照可变、FlowGram JSON 被持久化、或删除测试数据可能命中真实资源。

## Post-61 Follow-Up

- 实现 `IMicroflowRetentionService` 与 dry-run cleanup command。
- 将取消令牌贯穿到长运行 Runtime runner。
- 接入 Microflow 专用 ActivitySource / Metrics。
- 补浏览器级微流资源库、编辑器、DebugPanel Playwright 用例。
- 把审计从结构化日志升级为平台审计表事件。
