# Rollback Guide

## Application Rollback

1. 停止流量或将反向代理切到上一版本。
2. 停止当前 AppHost。
3. 恢复上一版本 AppHost binary 与 AppWeb dist。
4. 恢复上一版本 `appsettings`，但不要回滚 secret 到已泄露值。
5. 启动后检查 health、storage health、metadata health、runtime health。

## Microflow Version Rollback

使用已存在版本回滚 API：

```http
POST /api/microflows/{id}/versions/{versionId}/rollback
```

要求：

- rollback 不修改历史 `MicroflowVersion`。
- rollback 不修改历史 `MicroflowPublishSnapshot`。
- 只创建或切换当前资源 schema 指针。
- 操作必须记录 traceId 和审计日志。

## Database Rollback

只在 migration 失败或数据损坏时执行：

1. 停止 AppHost。
2. 备份当前异常数据库，保留诊断证据。
3. 恢复发布前数据库备份。
4. 启动 AppHost。
5. 验证 `MicroflowSchemaMigration`、storage health 和关键资源数。

## No-Rollback Cases

- 已对外发布并被调用的 `PublishSnapshot` 不允许原地修改。
- 只因 validation failed / publish blocked 不做数据库回滚。
- 只因 connector required 不做数据库回滚。

## Verification

回滚后必须执行：

```powershell
npx tsx scripts/verify-microflow-production-no-mock.ts
npx tsx scripts/verify-microflow-production-readiness.ts
```

如 readiness 为 `conditional-go`，需要记录风险接受人；如为 `no-go`，保持回滚状态并停止内测。
