# Deployment Guide

## Backend

1. 准备配置目录，例如 `D:\atlas-config`。
2. 复制 `src/backend/Atlas.AppHost/appsettings.Production.json`，用环境变量覆盖 secret：
   - `Jwt__SigningKey`
   - `Database__ConnectionString`
   - 外部连接器凭据。
3. 设置：

```powershell
$env:ASPNETCORE_ENVIRONMENT="Production"
$env:ATLAS_PLATFORM_CONFIG_ROOT="D:\atlas-config"
dotnet src/backend/Atlas.AppHost/bin/Release/net10.0/Atlas.AppHost.dll
```

4. 验证 `/health/ready`、`/api/microflows/storage/health`、`/api/microflows/runtime/health`。

## Frontend

1. 设置 `src/frontend/apps/app-web/.env.production` 或部署环境变量：
   - `VITE_APP_RUNTIME_MODE=direct`
   - `VITE_API_BASE=/api`
   - `VITE_MICROFLOW_ADAPTER_MODE=http`
   - 不设置 `VITE_MICROFLOW_API_MOCK`。
2. 构建：

```powershell
pnpm --dir src/frontend run build:app-web
```

3. 部署 `apps/app-web/dist`，反向代理 `/api` 到 AppHost。

## Database Migration

当前项目使用 SqlSugar code-first，不使用 EF migration。微流 schema 初始化必须满足：

- 可重复执行。
- 不 truncate 表。
- 不删除已有 `MicroflowResource`、`MicroflowSchemaSnapshot`、`MicroflowVersion`、`MicroflowPublishSnapshot`。
- `MicroflowSchemaMigration` 可读。

生产建议在维护窗口受控启动 migration，不建议每次发布无审计地自动改 schema。

## Release Verify

```powershell
npx tsx scripts/verify-microflow-production-no-mock.ts
dotnet build Atlas.SecurityPlatform.slnx -c Release
pnpm --dir src/frontend run build:app-web
npx tsx scripts/verify-microflow-production-readiness.ts
```

若 readiness 输出 `no-go`，不得发布。

## Package Checklist

- AppHost Release binary。
- AppWeb dist。
- `appsettings.Production.json` 模板和环境变量清单。
- `.http` smoke 示例。
- readiness summary。
- rollback / backup / restore / monitoring / security 文档。
