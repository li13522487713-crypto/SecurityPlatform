# 阶段 C 验证报告（M09-M14 适配器与运营）

## 范围
- M09 lowcode-workflow-adapter + RuntimeWorkflowsController + RuntimeAsyncJobsController + 弹性 / 双哲学
- M10 lowcode-asset-adapter + RuntimeFilesController + LowCodeAssetGcJob（Hangfire）
- M11 chatflow + session 适配器 + RuntimeChatflowsController + RuntimeSessionsController + RuntimeMessageLogService
- M12 trigger + webview-policy 适配器 + RuntimeTriggersController + RuntimeWebviewDomainsController + DAG 节点 34/35/36
- M13 RuntimeEventsController.Dispatch + RuntimeTraceService（6 维）+ lowcode-debug-client + 脱敏中间件 + list_spans → OK
- M14 LowCodeAppVersionsController（diff/rollback v1）+ RuntimeVersionsController（archive/rollback runtime）+ ResourceReferenceGuard + AppFaq

## 验证结果

### 后端
- `dotnet build Atlas.SecurityPlatform.slnx` → **0 警告 0 错误**（最终 ~20s）。
- 新增端点：
  - `/api/runtime/workflows/{id}:invoke|invoke-async|invoke-batch` + `/api/runtime/async-jobs/{jobId}` 4 端点
  - `/api/runtime/files:prepare-upload|complete-upload` + `/{handle}` + `sessions/{token}:cancel` 5 端点
  - `/api/runtime/chatflows/{id}:invoke` + `sessions/{sid}:pause|resume|inject` 4 端点
  - `/api/runtime/sessions` + `{id}/clear|pin|archive` 5 端点
  - `/api/runtime/triggers` + `{id}:pause|resume` + `/{id}` CRUD 共 7 端点
  - `/api/runtime/webview-domains` + `{id}:verify` 4 端点
  - `/api/runtime/events/dispatch`、`/api/runtime/traces` + `/{traceId}` + `/api/runtime/message-log`
  - `/api/v1/lowcode/apps/{id}/versions/{from}/diff/{to}` + `/{ver}/rollback`
  - `/api/v1/lowcode/resources/{type}/{id}/references`
  - `/api/v1/lowcode/faq` 4 端点
  - `/api/runtime/versions/archive` + `/{ver}:rollback`
- 新增 schema catalog 实体：`RuntimeWorkflowAsyncJob` / `LowCodeAssetUploadSession` / `LowCodeSession` / `LowCodeMessageLogEntry` / `LowCodeTrigger` / `LowCodeWebviewDomain` / `RuntimeTrace` / `RuntimeSpan` / `AppFaqEntry`（共 9 张新表）。
- DAG 节点新增：`TriggerUpsert(34)` / `TriggerRead(35)` / `TriggerDelete(36)`。
- 写接口全部经 `IAuditWriter`；批量操作严守 AGENTS.md "禁止循环 DB"（ReorderBatch / ExpireOlderThan / ReplaceForApp / InsertSpansBatch / InsertBatch 全部单 SQL）。
- `docs/coze-api-gap.md`：`list_spans` / `OpenAPIChatFlowRun` 标 **OK-via-runtime**（指向 `/api/runtime/*`）。

### 前端
- `pnpm run i18n:check` → **0 缺失**。
- 各包 `pnpm test`：
  - lowcode-workflow-adapter → **13**（5 binding × 2 + mappings + orchestration + resilience）
  - lowcode-asset-adapter → **4**（mime / size / 7 类白名单覆盖）
  - lowcode-chatflow-adapter → **3**（SSE 4 类 + 非法帧 + 多行 data 拼接）
  - lowcode-session-adapter → 0（纯客户端，不重复测试）
  - lowcode-trigger-adapter → **2**（CRON 5/6 字段校验）
  - lowcode-webview-policy-adapter → **3**（精确 / 通配子域 / 非法 URL）
  - lowcode-debug-client → **5**（buildQueryString / buildSpanTree / summarizePhases）
  - lowcode-versioning-client → **3**（groupDiffsByGroup）
- 阶段 C 累计：**33**；累计总计 **133 + 33 = 166**（阶段 A 75 + 阶段 B 58 + 阶段 C 33）。

### 文档
- `docs/lowcode-binding-matrix.md`（M09 完整：10 模式 A + 10 模式 B + loadingTargets/errorTargets 规则）
- `docs/lowcode-resilience-spec.md`（M09 完整：默认值 + 配置层级 + 降级 + 配额 + 灰度 + OTel + 脱敏 + 反例）
- `docs/lowcode-message-log-spec.md`（M11 完整：统一时间线模型 + 跨域聚合规则 + 6 维 + 脱敏 + OTel）

## 关键决策与对齐
- **dispatch 唯一桥梁**（PLAN §1.3 #2）：M13 RuntimeEventsController.Dispatch 落地；DispatchExecutor 内置 5 类前端语义动作 + 委托 IRuntimeWorkflowExecutor 处理 call_workflow（自动 patch 转换）。
- **作用域隔离**（PLAN §1.3 #3）：set_variable 在 dispatch 中再做 scope 守门，写入 system / component / event / workflow.outputs / chatflow.outputs 直接抛 `scope_violation`。
- **API 双前缀严守**：M14 端点双套校准完成（设计态 v1：diff/rollback；运行时：archive/rollback）。
- **资源引用治理**（PLAN §M14 S14-3）：`IResourceReferenceGuardService` 拒绝删除被引用资源；`ReindexForAppAsync` 替换语义 + 单 SQL 批量。
- **Coze 兼容层**：list_spans / chatflow stream 已通过 RuntimeTraceService / RuntimeChatflowService 替换为 OK-via-runtime；chatflow 真实流式已桥接到 IDagWorkflowExecutionService.StreamRunAsync（chatflowId 是 long 时），SseEvent → ChatChunk 4 类自动映射；非 long chatflowId 走 mock pipeline 兜底。

## 进入阶段 D
- M15 lowcode-runtime-mini + lowcode-mini-host（5187 + Taro 微信/抖音/H5）
- M16 Yjs 协同（自定义 SignalR provider）
- M17 web-sdk + 三类发布产物 + sdk-playground(5186) + 外链白名单完整
