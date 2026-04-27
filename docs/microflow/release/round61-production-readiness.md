# Round 61 Production Readiness

## Scope

第 61 轮只做生产准备、运维硬化与内测准入整理，不新增 Runtime 语义、不新增 ActionExecutor、不改 FlowGram/AuthoringSchema 契约。

允许并行：文档润色、测试报告整理、运维脚本整理、小范围 UI 文案/错误态/日志字段修复。禁止并行：新 Runtime 语义、新 Connector 平台、新权限系统大改、新异步 Job Queue、新 FlowGram 协议和数据库大结构调整。

## Baseline

- Frontend: `app-web` 通过 `createAppMicroflowAdapterConfig` 默认走 HTTP，生产策略禁止 mock/local/MSW fallback；MSW 仅在 `VITE_MICROFLOW_API_MOCK=msw` 且非生产时启动。
- Backend: 已存在 Resource / Metadata / Validation / Publish / References / TestRun / Trace / Cancel / Runtime hardening API；新增 `appsettings.Production.json` 提供安全默认值。
- Database: SqlSugar code-first 初始化微流表，表包括 `MicroflowResource`、`MicroflowSchemaSnapshot`、`MicroflowVersion`、`MicroflowPublishSnapshot`、`MicroflowReference`、`MicroflowRunSession`、`MicroflowRunTraceFrame`、`MicroflowRunLog`、`MicroflowMetadataCache`、`MicroflowSchemaMigration`。
- Health: 已有 `/api/microflows/health`、`/api/microflows/storage/health`、`/api/microflow-metadata/health`，本轮新增 `/api/microflows/runtime/health`。
- Known gaps: 真正协作式运行中取消、自动 retention job、专用备份恢复脚本、浏览器级微流专项 E2E 仍为后续项。

## Blocker / Critical

- Blocker: 生产路径匿名访问风险已通过 `MicroflowProductionGuardFilter` 收敛为生产环境认证 + workspace 守卫；开发环境保持现有 HTTP verify 兼容。
- Critical: 生产配置模板缺失已补 `src/backend/Atlas.AppHost/appsettings.Production.json`。
- Critical: no mock/local 生产检查已补 `scripts/verify-microflow-production-no-mock.ts`。
- Critical: readiness gate 已补 `scripts/verify-microflow-production-readiness.ts`。

## Production Defaults

- `Microflow:Adapter:Mode=http`
- `Microflow:Runtime:Rest:AllowRealHttp=false`
- `Microflow:Runtime:Rest:AllowPrivateNetwork=false`
- `Microflow:Metadata:SeedEnabled=false`
- `Microflows:Metadata:SeedEnabled=false`
- `Microflows:SeedData:Enabled=false`
- `Microflow:Diagnostics:EnableInternalDebugApi=false`
- `Microflow:Security:RequireWorkspaceId=true`

## Verification

推荐顺序：

```powershell
npx tsx scripts/verify-microflow-production-no-mock.ts
dotnet build Atlas.SecurityPlatform.slnx -c Release
pnpm --dir src/frontend run build:app-web
npx tsx scripts/verify-microflow-production-readiness.ts
```

若 AppHost 尚未启动，可设置 `MICROFLOW_READINESS_SKIP_LIVE_HEALTH=1` 只生成静态门禁报告；这不能作为最终 go 证据。

## Go Criteria

- Blocker = 0。
- Critical = 0。
- no mock/local production verify 通过。
- 后端 Release build 通过。
- 前端 production build 通过。
- health/storage/metadata/runtime health 可访问。
- migration、backup/restore、rollback、monitoring、安全配置和 runbook 文档完整。

当前建议结论见 `docs/microflow/release/inner-test-readiness.md`。
