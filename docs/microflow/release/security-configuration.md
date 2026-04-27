# Security Configuration

## Production Defaults

- `Jwt:SigningKey` 必须由环境变量或 secret store 覆盖，占位值会阻止生产启动。
- `Security:EnforceHttps=true`
- `Microflow:Security:EnableProductionGuard=true`
- `Microflow:Security:RequireWorkspaceId=true`
- `Microflow:Security:EntityAccessMode=DenyUnknownEntity`
- `Microflows:Metadata:SeedEnabled=false`
- `Microflows:SeedData:Enabled=false`
- `Microflow:Runtime:Rest:AllowRealHttp=false`
- `Microflow:Runtime:Rest:AllowPrivateNetwork=false`

## Workspace / Tenant

生产环境非 health 微流 API 需要认证，并且默认要求 `X-Workspace-Id`。缺少 workspace 返回 `MICROFLOW_PERMISSION_DENIED`，未认证返回 `MICROFLOW_UNAUTHORIZED`。

租户边界沿用平台 JWT + `X-Tenant-Id` 校验。试点建议使用单租户或受控租户，避免跨租户共享资源。

## Permissions

当前微流权限是基础边界：

- Resource 查询按 workspace 过滤。
- Metadata 默认使用当前 request context workspace。
- RunSession / trace 查询必须带 runId 并通过服务层读取。
- Delete / archive / restore / publish / rollback 需要在生产网关或平台权限层限制。

完整 workspace membership 和 Mendix EntityAccess 矩阵属于 post-61 follow-up。

## Audit

审计事件至少覆盖：

- `microflow.create`
- `microflow.update`
- `microflow.delete`
- `microflow.archive`
- `microflow.restore`
- `microflow.publish`
- `microflow.rollback`
- `microflow.duplicateVersion`
- `microflow.validate`
- `microflow.testRun.start`
- `microflow.testRun.cancel`
- `microflow.runtime.failed`
- `microflow.reference.rebuild`
- `microflow.metadata.seed`
- `microflow.retention.cleanup`

字段：eventId、eventType、userId、userName、workspaceId、tenantId、resourceId、runId、version、status、timestamp、traceId、ip、userAgent、summary、errorCode。

Round61 第一版允许使用结构化 `ILogger`，但不得记录完整 schema、token、cookie、password 或 connection string。

## Negative Smoke

生产环境应验证：

```http
GET /api/microflows
X-Tenant-Id: tenant
```

预期：`403` + `MICROFLOW_PERMISSION_DENIED`。

无认证访问非 health API 预期：`401` + `MICROFLOW_UNAUTHORIZED`。
