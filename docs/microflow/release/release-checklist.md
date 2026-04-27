# Release Checklist

## Configuration

- [ ] `ASPNETCORE_ENVIRONMENT=Production`
- [ ] `ATLAS_PLATFORM_CONFIG_ROOT` 指向生产配置目录。
- [ ] `Jwt__SigningKey` 由 secret store 注入。
- [ ] `Database__ConnectionString` 已配置。
- [ ] `Microflow:Runtime:Rest:AllowRealHttp=false`，除非完成 allowlist 审批。
- [ ] `Microflows:Metadata:SeedEnabled=false`
- [ ] `Microflows:SeedData:Enabled=false`
- [ ] `Microflow:Diagnostics:EnableInternalDebugApi=false`

## Build

- [ ] `dotnet build Atlas.SecurityPlatform.slnx -c Release`
- [ ] `pnpm --dir src/frontend run build:app-web`
- [ ] `npx tsx scripts/verify-microflow-production-no-mock.ts`

## Database

- [ ] 发布前备份完成。
- [ ] 微流表存在。
- [ ] `MicroflowSchemaMigration` 可读。
- [ ] migration 不清空已有资源、版本、快照、发布快照。

## Health

- [ ] `/health/live`
- [ ] `/health/ready`
- [ ] `/api/microflows/health`
- [ ] `/api/microflows/storage/health`
- [ ] `/api/microflow-metadata/health`
- [ ] `/api/microflows/runtime/health`

## Smoke

- [ ] Resource / Schema 主链路。
- [ ] Metadata / Validation 主链路。
- [ ] Publish / Version / References 主链路。
- [ ] TestRun / Debug / Trace / Cancel 主链路。
- [ ] Permission negative smoke。
- [ ] Audit smoke。
- [ ] Observability smoke。

## Documents

- [ ] deployment guide
- [ ] rollback guide
- [ ] backup/restore guide
- [ ] monitoring/alerting guide
- [ ] security configuration
- [ ] ops runbook
- [ ] known limitations
- [ ] inner-test readiness

## Gate

- [ ] `npx tsx scripts/verify-microflow-production-readiness.ts`
- [ ] Blocker = 0
- [ ] Critical = 0
- [ ] Major 风险有接受人和说明。
- [ ] readinessStatus 非 `no-go`。
