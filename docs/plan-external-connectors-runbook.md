# External Collaboration Connector 端到端联调 Runbook

> v4 报告 27-31 章 P0 落地的端到端验证手册。所有步骤在 **`Atlas.AppHost`（端口 5002）** 启动后，通过下文列出的 `POST /api/v1/connectors/*` 等端点 + 前端 `/org/:orgId/workspaces/:workspaceId/settings/connectors/*` 完成。Bosch 样例在 `src/backend/Atlas.AppHost/Bosch.http/` 下按主题拆分；`Connectors.http` 历史文件已随 `Atlas.PlatformHost` 删除，可用手册 § 引用或自补 `.http` 段落。

## 0. 前置条件

- 后端：`dotnet build` 0 警告 0 错误（已验证）。
- 测试套件：`dotnet test tests/Atlas.SecurityPlatform.Tests --filter "FullyQualifiedName~ExternalConnectors"` 全绿（已验证 5/5）。
- 前端：`pnpm install` 注册 `@atlas/external-connectors-react` 到 workspace；Windows 环境若遇到 `EPERM symlink` 错误，需在管理员 PowerShell 下执行（pnpm 严格软链需要权限）。
- 配置：在 `appsettings.json` 顶层补一段 `ExternalConnectors`：
  ```json
  {
    "ExternalConnectors": {
      "DataProtectionKey": "REPLACE_WITH_32_PLUS_CHARS_PROD_SECRET"
    }
  }
  ```
  开发环境可缺省，bridge 自动回退到默认 dev key（不可用于生产）。

## 1. 注册 provider

1. 通过 `POST /api/v1/connectors/providers`（见 `Connectors.http § 1.3 / 1.4`）创建 WeCom + Feishu 两个 provider 实例。
2. `secretJson` 为明文 JSON：
   - WeCom: `{ "corpSecret": "<plain>", "callbackToken": "<token>", "callbackEncodingAesKey": "<aes>" }`
   - Feishu: `{ "appSecret": "<plain>", "eventVerificationToken": "<token>", "eventEncryptKey": "<key>" }`
   - 由 `ConnectorSecretProtectorBridge` 走 `DataProtectionService` AES-CBC 加密入库；前端不可读，需要时由 `:rotate` 端点更新。
3. 返回的 `id` 即 `providerId`，供后续接口使用。

## 2. OAuth 登录闭环

1. 前端 `ConnectorProvidersPage` 列出 provider；点击 "登录绑定" → 调用 `POST /connectors/oauth/start`，携带 `postLoginRedirect`（限制必须为本地 URL）。
2. 服务端：
   - 解析 provider，生成 OAuth state（`OAuthState.CreateValue()`）入 `IOAuthStateStore`，TTL 10 分钟。
   - 调用 `IExternalIdentityProvider.BuildAuthorizationUrl`：企微会做"可信域名"前置拦截（提前避开 50001）。
3. 浏览器跳到外部授权地址 → 用户同意 → 跳回 `${CallbackBaseUrl}/api/v1/connectors/oauth/callback?state=...&code=...`。
4. 前端 `ConnectorOAuthCallbackPage` 调 `POST /connectors/oauth/callback`：
   - state 单次消费 + 跨租户校验。
   - `ExchangeCodeAsync` → `ExternalUserProfile`。
   - `IExternalIdentityBindingService.ResolveOrAttemptBindAsync`（默认 Mobile 策略）：
     - 命中已绑定 → 直接调 `IConnectorJwtIssuer.IssueAsync` 签发 JWT、返回 `accessToken/refreshToken/redirectTo`，写 `ExternalIdentityBindingAuditLog`。
     - 未命中 → 返回 `pendingBindingTicket`，前端进入 `/sign?bindingPending=1` 待绑定页（管理员或本人后续走 `POST /identity-bindings/manual`）。
     - 命中冲突（同本地用户在同 provider 已绑到不同 ExternalUserId） → 返回 `Conflict` + 冲突 binding 摘要，前端进入 `/sign?bindingConflict=1` 冲突中心。

## 3. 通讯录同步与对账

1. 前端 `ConnectorDirectorySyncPage` 点 "立即全量同步" → `POST /providers/{id}/directory/sync/full`。
2. 服务端 `ExternalDirectorySyncService`：
   - 从根部门递归拉部门（企微 root=`1`，飞书 root=`0`），逐个 upsert 到 `ExternalDepartmentMirror`。
   - 按部门取直属成员，逐个 upsert 到 `ExternalUserMirror` + 写 `ExternalDepartmentUserRelation`。
   - 应用可见范围错误（企微 60011 / 飞书 99992402）由 provider 层降级返回空集合，不阻塞整体同步；当前 job 仍标记 `Succeeded` 但 `FailedItems > 0`。
3. 对账：`GET /providers/{id}/directory/sync/jobs`、`GET .../jobs/{jobId}/diffs` 分页查看差异行；失败行带 `errorMessage`。
4. 增量：`POST /providers/{id}/directory/sync/incremental` 接收 `ExternalDirectoryEvent`；事件只带 `EntityId` 时由 `IExternalDirectoryProvider.GetUserAsync/GetDepartmentAsync` 自动补拉详情（应对 2022-08-15 后只返 ID 的企微回调）。
5. 定时校准：provider 创建/更新时 SyncCron 非空时，由 `ConnectorRecurringJobScheduler.Schedule` 注册 Hangfire RecurringJob（`connector-dir-sync:{tenantId}:{providerId}`），由 `ExternalDirectoryRecurringSyncRunner` 在后台消费，自动通过 `ITenantContextWriter` 注入 TenantId。

## 4. 表单推审批闭环（模式 C 默认）

1. 管理员在 `ConnectorApprovalMappingPage` 配置：
   - `:refresh` 拉取外部审批模板 → 缓存到 `ExternalApprovalTemplateCache`。
   - `PUT /providers/{id}/approvals/template-mappings/{flowDefinitionId}` 写入字段映射：本地表单字段 → 外部模板控件 ID，校验器要求每条都有 `localFieldKey + externalControlId + valueType`。
   - 选择 `IntegrationMode = Hybrid`（默认）。
2. 业务用户提交本地表单 → 本地审批引擎创建 `ApprovalProcessInstance` → 调用 `IExternalApprovalDispatchService.OnInstanceStartedAsync`：
   - 找到 mapping，按 `IntegrationMode` 决定是否调外部 provider 提单。
   - `IExternalApprovalProvider.SubmitApprovalAsync` 装配 apply_data → 返回 `ExternalApprovalInstanceRef`。
   - 写 `ExternalApprovalInstanceLink`（本地 instance ↔ 外部 sp_no/instance_code）。
3. 推消息卡片：通过 `IApprovalNotificationSender` 总线（新增 `WeCom=5` / `Feishu=6` 两个 channel），`WeComApprovalNotificationSender` / `FeishuApprovalNotificationSender` 把 `ApprovalNotificationTemplate` 渲染为 `ExternalMessageCard` → `IExternalMessagingProvider.SendCardAsync`。
4. 用户在企微/飞书侧审批通过 → 平台收到 webhook：
   - `POST /api/v1/connectors/providers/{id}/callbacks/{topic}`（匿名）。
   - `IConnectorEventVerifier.Verify`：企微走 SHA1 + AES-CBC 解密；飞书走 SHA256 + AES-CBC + verification token 校验。
   - `IConnectorCallbackInboxService.AcceptAsync` 写 `ExternalCallbackEvent`（密文落库 + IdempotencyKey + ReplayGuard），命中重复直接 `Duplicated`。
   - 解析事件类型 → 命中 `approval-status` 走 `ExternalApprovalDispatchService.OnInstanceStatusChangedAsync` 推进本地实例；命中 `contact.*` 走 `ExternalDirectorySyncService.ApplyIncrementalEventAsync`。
5. 状态变更后调 `IExternalMessagingProvider.UpdateCardAsync` 把待审批卡片改成 "已通过 / 已拒绝"（企微走 `update_template_card` + `response_code`；飞书走 `PATCH im/v1/messages/{id}`）。

## 5. 三种集成模式回归

| 模式 | mapping 配置 | 行为差异 |
| --- | --- | --- |
| `LocalLed` (B) | `IntegrationMode = LocalLed` | `OnInstanceStartedAsync` 直接返回 `Pushed=false, reason=local-led`；本地引擎独立流转，外部仅做消息卡片通知。 |
| `Hybrid` (C) | `IntegrationMode = Hybrid` | 本地引擎流转 + 调用 `SubmitApprovalAsync` + 写 `ExternalApprovalInstanceLink` + `SyncThirdPartyInstanceAsync` 同步任务/抄送/状态。飞书走 `external_instances/check`；企微无原生三方审批同步接口 → 退化为模板卡片更新。 |
| `ExternalLed` (A) | `IntegrationMode = ExternalLed` | 与 C 相同的提单链路；本地引擎不主动推进，由外部回调驱动。 |

## 6. 健康看板

- 连接器列表：`/org/:orgId/workspaces/:workspaceId/settings/connectors`（`ConnectorsPage`）。
- 每个 provider 详情：`/.../settings/connectors/:providerId`（`ConnectorDetailPage`）含三段：
  - `ConnectorBindingsPage`（按 status 筛选 Active/PendingConfirm/Conflict/Revoked）。
  - `ConnectorDirectorySyncPage`（最近 20 次 job + 立即全量同步入口）。
  - `ConnectorApprovalMappingPage`（模板缓存 + 字段映射）。
- 入站事件死信：`SELECT * FROM ExternalCallbackEvent WHERE Status = DeadLetter` 暂未提供单独 UI，下一阶段 P1 增补。

## 7. 等保对齐核对清单

- 凭据加密：`ExternalIdentityProvider.SecretEncrypted` AES-CBC（DataProtectionService）。
- OAuth state：单次消费 + TTL + 跨租户校验。
- 入站 Webhook：HMAC + 解密 + ReplayGuard + 幂等键 + 死信。
- 审计：`ExternalIdentityBindingAuditLog` 覆盖自动绑定/换号/解绑/冲突解决；调用与回调走现有 `IAuditRecorder`。
- 多租户隔离：所有 11 个新实体继承 `TenantEntity`，仓储统一加 `TenantIdValue` 过滤。
- 可信域名：企微 OAuth start 阶段前置拦截（避免 50001）；飞书可选启用。

## 8. 后续 P1 建议（已超出本里程碑）

- 死信 UI 与一键重放。
- 基于 WireMock.Net 的真实集成测试套件（pinning provider 错误码映射）。
- 单 provider 多 corp 的"应用商店"模式（当前每租户一份 corp 的 provider 实例已可，但前端筛选体验仍需打磨）。
- 钉钉 provider（`Atlas.Connectors.DingTalk` 当前为占位）。
