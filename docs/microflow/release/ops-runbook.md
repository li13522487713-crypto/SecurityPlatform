# Ops Runbook

## Start

Backend:

```powershell
$env:ASPNETCORE_ENVIRONMENT="Production"
$env:ATLAS_PLATFORM_CONFIG_ROOT="D:\atlas-config"
dotnet run --project src/backend/Atlas.AppHost -c Release
```

Frontend:

```powershell
pnpm --dir src/frontend run build:app-web
```

部署 `src/frontend/apps/app-web/dist` 到静态站点，并确保 `/api` 反向代理到 AppHost。

## Database

- 默认使用 SqlSugar + SQLite，连接串来自 `Database:ConnectionString`。
- 微流表由 code-first 初始化，不应清空已有数据。
- 关键表：`MicroflowResource`、`MicroflowSchemaSnapshot`、`MicroflowVersion`、`MicroflowPublishSnapshot`、`MicroflowReference`、`MicroflowRunSession`、`MicroflowRunTraceFrame`、`MicroflowRunLog`、`MicroflowMetadataCache`、`MicroflowSchemaMigration`。

## Migration

1. 备份数据库文件或实例。
2. 设置 `DatabaseInitializer:RunOnStartup=false` 时，在维护窗口启动一次受控 AppHost 或使用现有 migration 验证脚本。
3. 检查 `/api/microflows/storage/health`，所有微流表 `exists=true`。
4. 检查 `MicroflowSchemaMigration` 是否记录当前 schema 状态。

## Seed

- 生产必须保持 `Microflows:Metadata:SeedEnabled=false`、`Microflows:SeedData:Enabled=false`。
- 开发态如需 seed，只在 Development 环境启用，并使用独立数据库。

## Runtime Limits

关键配置位于 `Microflow:Runtime`：

- `MaxSteps`
- `MaxLoopIterations`
- `RunTimeoutSeconds`
- `ActionTimeoutSeconds`
- `MaxTraceFrames`
- `MaxRuntimeLogs`
- `MaxConcurrentRuns`
- `MaxConcurrentRunsPerResource`

## RestCall Allowlist

生产默认 `AllowRealHttp=false`、`AllowPrivateNetwork=false`。确需出网时：

1. 评审目标 host。
2. 写入 `AllowedHosts`。
3. 保持 `DeniedHosts` 覆盖内网与元数据地址。
4. 设置 `MaxResponseBytes` 与 `TimeoutSeconds`。
5. 复跑 readiness gate。

## Health

```http
GET /api/microflows/health
GET /api/microflows/storage/health
GET /api/microflow-metadata/health
GET /api/microflows/runtime/health
GET /health/live
GET /health/ready
```

health 响应不得泄露 connection string、token、cookie 或敏感 header。

## Run / Trace

- 查询 run: `GET /api/microflows/runs/{runId}`
- 查询 trace: `GET /api/microflows/runs/{runId}/trace?pageIndex=1&pageSize=50`
- 取消 run: `POST /api/microflows/runs/{runId}/cancel`

若 run stuck，先查 `RunTimeoutSeconds`、`MaxSteps`、`MaxTraceFrames`，再按 traceId 搜索后端日志。

## Cleanup

Round61 不暴露生产 cleanup endpoint。手工清理只允许：

- 测试前缀：`R60_E2E_`、`E2E_MF_`、`R61_SMOKE_`
- dry run 先统计资源、run、trace、log 数量。
- 不删除 `MicroflowVersion`、`MicroflowSchemaSnapshot`、`MicroflowPublishSnapshot`。

## Backup / Restore

见 `backup-restore-guide.md`。

## Rollback

见 `rollback-guide.md`。

## Fault Handling

- PublishBlocked: 查询 validation issues 和 impact report，确认后再发布。
- ValidationFailed: 使用 `fieldPath` 定位 schema 节点，禁止直接改数据库 JSON。
- ConnectorRequired: 检查 connector registry 和 action support matrix，不要伪造 success。
- TraceWriteFailed: 搜索 traceId，检查 DB 写入、表存在、磁盘空间。
- MigrationFailed: 停止发布，恢复备份，收集 AppHost 启动日志。

## Diagnostic Package

收集：

- readiness summary。
- AppHost NLog error/warn。
- health 响应。
- run session、trace frame、runtime log。
- appsettings key 清单，不包含 secret value。

## Bug Report

必须包含 workspaceId、resourceId、runId、traceId、时间窗口、操作步骤、预期结果、实际错误码和是否触发 connector required。
