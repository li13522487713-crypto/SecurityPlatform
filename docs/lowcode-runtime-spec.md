# 低代码运行时协议规格（lowcode-runtime-spec）

> 范围：低代码运行时（runtime data plane）端点契约 + 协议帧 + 安全约束。
>
> 端点前缀双套：设计态 `/api/v1/lowcode/*`（PlatformHost 5001） / 运行时 `/api/runtime/*`（AppHost 5002），**禁止混用**。

## §1 Schema 字段全集（M01）

完整 AppSchema / PageSchema / ComponentSchema / BindingSchema / EventSchema / ActionSchema / ContentParamSchema / VariableSchema / LifecycleSchema 由 `@atlas/lowcode-schema` 与 zod 校验器维护；后端 DraftSchemaJson 字段用 `text` 列存储原文，写入前 `JsonDocument.Parse` 二次校验。

## §2 表达式语法、7 作用域、隔离规则（M02 + P5-1 修正）

- jsonata 全语法 + Jinja-like `{{ expr }} / {% if %} / {% for %} / {% break %} / {% continue %}`
- filter 链：`{{ x | upper | default('-') }}`；内置 8 个 filter，可 `registerFilter` 扩展
- 7 作用域读写矩阵（与 `@atlas/lowcode-schema/shared/enums.ts` `SCOPE_ROOTS` 严格对齐）：
  - **可读写**：`page` / `app`
  - **只读**：`system` / `component` / `event` / `workflow.outputs` / `chatflow.outputs`
- 写动作 `set_variable.scopeRoot` 仅允许 `page`/`app`，违规走双层校验抛 `ScopeViolationError`
- system 作用域为只读：`system.tenantId / system.userId / system.locale / system.theme / system.timezone` 等由前端 RuntimeContext 注入，业务侧不允许 `set_variable` 写入

## §3 动作链与状态补丁、超时熔断降级（M03）

- ActionSchema 7 子类型：set_variable / call_workflow / call_chatflow / navigate / open_external_link / show_toast / update_component
- onError 子链；when 条件分支；parallel 并发标记
- RuntimeStatePatch：`{ scope, path, op, value }`；`op ∈ {set, merge, unset}`；`path` 支持 `a.b.c[0].d` 与 `a.b.c.0.d`
- 弹性策略 ResiliencePolicy：timeoutMs / retry(maxAttempts, backoff, initialDelayMs) / circuitBreaker / fallback

## §4 RuntimeSchemaController（M08）

| Method | Path | 说明 |
| --- | --- | --- |
| GET | `/api/runtime/apps/{appId}/schema?renderer=` | 当前生效 schema 快照（含 X-Atlas-Lowcode-Renderer / Unsupported 头） |
| GET | `/api/runtime/apps/{appId}/versions/{versionId}/schema` | 历史版本不可变快照（schema_snapshot_json + resource_snapshot_json） |
| GET | `/api/runtime/renderers/{renderer}/capability` | 渲染器能力差异化（不支持组件清单） |

## §5 RuntimeWorkflowsController（同步 / 异步 / 批量）（M09 + M19）

| Method | Path | 说明 |
| --- | --- | --- |
| POST | `/api/runtime/workflows/{id}:invoke` | 同步执行（返回 Outputs + Patches） |
| POST | `/api/runtime/workflows/{id}:invoke-async` | 提交 Hangfire 持久化任务，返回 jobId |
| POST | `/api/runtime/workflows/{id}:invoke-batch` | 批量循环（CSV / JSON / database 输入源） |
| GET | `/api/runtime/async-jobs/{jobId}` | 查询进度 + 结果 |
| POST | `/api/runtime/async-jobs/{jobId}:cancel` | 取消任务 |

## §6 RuntimeFilesController（M10）

两阶段上传：prepare → upload(PUT direct) → complete；GC：每日 02:00 软删除 7 天未引用 fileHandle。

## §7 RuntimeChatflowsController + Sessions + 中断 / 恢复 / 插入（M11）

- SSE 4 类帧：`tool_call` / `message` / `error` / `final`，每帧含递增 seq
- `chatflowId` 是 long → 桥接到 Coze workflow 执行服务；当前先以真实执行结果组装 SSE，后续再补真正流式
- Pause / Resume / Inject：会话状态机；Resume 自动按消息日志最近一条 user_input + 后续 inject 重发完整 SSE 流
- Sessions：list / create / pin / archive / clear-messages

## §8 RuntimeTriggersController + RuntimeWebviewDomainsController（M12）

### 8.1 Trigger 三类 kind

| kind | 触发源 | 入口 |
| --- | --- | --- |
| cron | Hangfire RecurringJob | UpsertAsync 自动 AddOrUpdate；启动期 LowCodeTriggerReconcileHostedService 重新注册 |
| event | 业务方代码调用 IRuntimeTriggerService.RaiseEventAsync | POST `/api/runtime/triggers/events/{eventName}:raise`（需登录态） |
| webhook | 外部系统 HTTP POST | POST `/api/runtime/webhooks/triggers/{id}`（[AllowAnonymous]，X-Atlas-Webhook-Secret 校验） |

### 8.2 Trigger 端点

| Method | Path | 说明 |
| --- | --- | --- |
| GET | `/api/runtime/triggers` | 列表 |
| POST | `/api/runtime/triggers` | 创建 |
| PUT | `/api/runtime/triggers/{id}` | 更新 |
| DELETE | `/api/runtime/triggers/{id}` | 删除（同时 RemoveIfExists Hangfire 任务） |
| POST | `/api/runtime/triggers/{id}:pause` | 禁用（RemoveIfExists） |
| POST | `/api/runtime/triggers/{id}:resume` | 启用（重新 SyncCronRegistration） |
| POST | `/api/runtime/triggers/{id}:rotate-webhook-secret` | 仅 kind=webhook：生成 24 字节 hex 密钥（whs_ 前缀），明文响应只 1 次 |
| POST | `/api/runtime/triggers/events/{eventName}:raise` | 触发匹配 EventName 的所有 enabled event 触发器 |
| POST | `/api/runtime/webhooks/triggers/{id}` | 外部回调入口（X-Atlas-Webhook-Secret 常量时间比对） |

### 8.3 触发执行链

`Trigger.FireAsync` → 若 WorkflowId 非空 → `IRuntimeWorkflowExecutor.SubmitAsyncAsync` → Hangfire 异步执行（不阻塞调度器）；审计 `lowcode.runtime.trigger.fire` 始终成功落表。

### 8.4 Webview 域名

| Method | Path | 说明 |
| --- | --- | --- |
| GET | `/api/runtime/webview-domains` | 列表 |
| POST | `/api/runtime/webview-domains` | 添加（生成 verificationToken） |
| POST | `/api/runtime/webview-domains/{id}:verify` | http_file：GET `https://{domain}/.well-known/atlas-webview-verify.txt` 比对；dns_txt：未引入 DnsClient 包前标记 `dns_txt:not-implemented` 并允许通过 |
| DELETE | `/api/runtime/webview-domains/{id}` | 删除 |

## §9 RuntimeEventsController.Dispatch + Trace 6 维检索（M13）

- POST `/api/runtime/events/dispatch`：所有 UI 事件唯一桥梁；不允许直接调用 workflows / chatflows / files / triggers
- DispatchActionDto：kind / id / payload(JsonElement) / onError[]
- 内置 5 类 action 直接产 patch；call_workflow 委托 RuntimeWorkflowExecutor
- Trace 6 维：traceId / appId+page / component / from-to / errorType / userId
- Spans：每 action 一帧；attributes 按 docs/lowcode-resilience-spec.md §2 脱敏

## §10 RuntimeVersionsController（archive / rollback）+ AppVersionsController（设计态 v1）（M14）

- 设计态：POST `/api/v1/lowcode/apps/{id}/snapshot` / GET `/versions` / GET `/versions/{from}/diff/{to}` / POST `/versions/{id}/rollback`
- 运行时：GET `/api/runtime/apps/{appId}/versions/{versionId}/schema`（不可变历史快照）
- DiffOps：JSON Patch 子集 add / remove / replace（schema 节点 id 稳定，无需 move/copy/test）

## §11 Runtime publish artifacts 只读端点（M17）

- GET `/api/runtime/publish-artifacts?appId=` 列出当前应用所有产物（hosted / embedded-sdk / preview）
- 每个产物含 fingerprint / publicUrl / rendererMatrixJson / status / errorMessage

## 跨章节硬约束

- 所有写接口必须 `IAuditWriter` 审计 success/failed 双路径
- 文件 mime / 大小双校验
- 外链 `open_external_link` 必须经 webview 白名单校验
- Trace span attributes 默认按 LowCodeCredentialProtector.Mask 脱敏（凭据/cookie/token 类字段）
