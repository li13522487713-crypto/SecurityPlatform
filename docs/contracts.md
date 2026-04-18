# Atlas Security Platform Contracts

## Workflow Host 约束

- 当前仓库仅维护 `src/frontend/apps/app-web` 单宿主，不再维护独立 `src/coze-workflow-host` 目录。
- `app-web` 直接挂载 Coze 原生工作流/空间适配层（`@coze-workflow/playground-adapter`、`@coze-studio/workspace-adapter`），前端实现位于 `src/frontend/packages/workflow/**` 与 `src/frontend/packages/foundation/**`。
- `@atlas/workflow-core-react`、`@atlas/workflow-editor-react`、`@atlas/module-workflow-react` 已删除，不再作为宿主桥接边界。
- 前端与后端协议对齐统一走兼容层：`/api/workflow_api/*`、`/api/playground_api/*`、`/api/op_workflow/*`、`/api/bot/*`、`/api/space/*`、`/api/draftbot/*`、`/v1/workflow/*`、`/v1/workflows/*`。

## Organization / Workspace Portal

### 路由与工作模式

- 新前端主入口采用组织-工作空间层级：
  - `/sign`
  - `/org/:orgId/workspaces`
  - `/org/:orgId/workspaces/:workspaceId/dashboard`
  - `/org/:orgId/workspaces/:workspaceId/develop`
  - `/org/:orgId/workspaces/:workspaceId/develop/chat`
  - `/org/:orgId/workspaces/:workspaceId/develop/model-configs`
  - `/org/:orgId/workspaces/:workspaceId/develop/assistant-tools`
  - `/org/:orgId/workspaces/:workspaceId/develop/publish-center`
  - `/org/:orgId/workspaces/:workspaceId/library`
  - `/org/:orgId/workspaces/:workspaceId/library/data`
  - `/org/:orgId/workspaces/:workspaceId/library/variables`
  - `/org/:orgId/workspaces/:workspaceId/manage/:tab`
  - `/org/:orgId/workspaces/:workspaceId/settings/:tab`
  - `/org/:orgId/workspaces/:workspaceId/apps/:appId`
  - `/org/:orgId/workspaces/:workspaceId/apps/:appId/publish`
  - `/org/:orgId/workspaces/:workspaceId/agents/:agentId`
  - `/org/:orgId/workspaces/:workspaceId/agents/:agentId/publish`
  - `/org/:orgId/workspaces/:workspaceId/apps/:appId/workflows/:workflowId`
  - `/org/:orgId/workspaces/:workspaceId/apps/:appId/chatflows/:workflowId`
- `/apps/:appKey/*` 仅保留为兼容跳转入口，不再作为主壳或主导航来源。
- 第一版 `orgId` 直接对应当前登录租户 ID。
- 工作空间为真实后端实体，显式绑定 `AppInstanceId + AppKey`。

### 后端实体

- `Workspace`
  - `Id`
  - `TenantIdValue`
  - `Name`
  - `Description`
  - `Icon`
  - `AppInstanceId`
  - `AppKey`
  - `IsArchived`
  - `CreatedBy`
  - `CreatedAt`
  - `UpdatedBy`
  - `UpdatedAt`
  - `LastVisitedAt`
- `WorkspaceRole`
  - `WorkspaceId`
  - `Code`
  - `Name`
  - `IsSystem`
  - `DefaultActionsJson`
- `WorkspaceMember`
  - `WorkspaceId`
  - `UserId`
  - `WorkspaceRoleId`
  - `JoinedAt`
- `WorkspaceResourcePermission`
  - `WorkspaceId`
  - `WorkspaceRoleId`
  - `ResourceType`
  - `ResourceId`
  - `ActionsJson`

### 资源归属字段

- 下列核心设计资源新增可空 `WorkspaceId`：
  - `AiApp`
  - `Agent`
  - `WorkflowMeta`
  - `KnowledgeBase`
  - `AiDatabase`
  - `AiPlugin`
- `AiApp.AgentId` 为可空字段，支持“先绑定 Workflow、后续再绑定 Agent”的创建流程。
- 启动期初始化逻辑会为默认工作空间回填历史资源的 `WorkspaceId`。

### API

- `GET /api/v1/organizations/{orgId}/workspaces`
  - 返回当前用户可访问的工作空间卡片列表
- `GET /api/v1/organizations/{orgId}/workspaces/by-app-key/{appKey}`
  - 旧 `/apps/:appKey/*` 路由迁移时用于定位对应工作空间
- `GET /api/v1/organizations/{orgId}/workspaces/{workspaceId}`
  - 返回工作空间上下文详情，包括 `appKey / appInstanceId / roleCode / allowedActions`
- `POST /api/v1/organizations/{orgId}/workspaces`
  - 创建绑定到指定应用实例的新工作空间
- `PUT /api/v1/organizations/{orgId}/workspaces/{workspaceId}`
  - 更新工作空间名称、描述与图标，不允许修改既有 `AppInstanceId / AppKey` 绑定
- `DELETE /api/v1/organizations/{orgId}/workspaces/{workspaceId}`
  - 归档工作空间（软删除）；归档后列表、详情与 `by-app-key` 查询均不再返回该工作空间
- `GET /api/v1/organizations/{orgId}/workspaces/{workspaceId}/develop/apps`
  - 应用优先开发页首屏数据，只返回应用卡片
- `POST /api/v1/organizations/{orgId}/workspaces/{workspaceId}/develop/apps`
  - 在工作空间内创建应用，并同步创建关联标准 Workflow；创建阶段允许 `AiApp.AgentId = null`
- `GET /api/v1/organizations/{orgId}/workspaces/{workspaceId}/resources`
  - 按资源类型懒加载工作空间资源分区
- `GET /api/v1/organizations/{orgId}/workspaces/{workspaceId}/members`
- `POST /api/v1/organizations/{orgId}/workspaces/{workspaceId}/members`
- `PUT /api/v1/organizations/{orgId}/workspaces/{workspaceId}/members/{userId}`
- `DELETE /api/v1/organizations/{orgId}/workspaces/{workspaceId}/members/{userId}`
- `GET /api/v1/organizations/{orgId}/workspaces/{workspaceId}/resources/{resourceType}/{resourceId}/permissions`
- `PUT /api/v1/organizations/{orgId}/workspaces/{workspaceId}/resources/{resourceType}/{resourceId}/permissions`

### 权限语义

- 内置工作空间角色：
  - `Owner`
  - `Admin`
  - `Member`
- 默认动作：
  - `Owner/Admin`: `view/edit/publish/delete/manage-permission`
  - `Member`: `view`
- `WorkspaceResourcePermission` 用于资源级覆盖；若未配置覆盖，则回退到角色默认动作。

## Coze Workflow API 兼容层

- `PlatformHost` 与 `AppHost` 额外提供原生 Coze 协议兼容入口：`/api/workflow_api/*`
- 兼容层当前覆盖 workflow playground 必需接口：
  - `canvas`
  - `save`
  - `publish`
  - `node_type`
  - `node_template_list`
  - `old_validate`
  - `test_run`
  - `get_process`
  - `test_resume`
  - `cancel`
  - `nodeDebug`
  - `workflow_references`
  - `released_workflows`
  - `copy`
  - `validate_tree`（M1：返回 `Array<ValidateTreeInfo>`，每项含 `workflow_id`、`name`、`errors[]`）
  - `node_panel_search`（M1：节点目录关键字搜索，命中节点放在 `data.resource_workflow.workflow_list`）
  - `history_schema`（M1：按 commitId / executionId 解析历史画布 JSON）
  - `get_node_execute_history`（M1：节点执行快照，`extra` 包含 `input/output/variables` 三段 JSON 字符串）
  - `get_trace`（M1 改造：`spans[].extra.input/output/variables` 三段；`spans[].status_code/duration/start_time` 与 `header.duration/start_time` 与 trace.thrift 对齐；返回值会先附一条根 `Workflow` span 作为入口）
- 兼容层内部统一复用 `/api/v2/workflows*` 与现有 Dag 工作流服务（`IDagWorkflow*Service`），不在前端重复协议转换。

### Coze 兼容层风险已修复登记

| 风险 | 状态 | 修复点 | 回归用例 |
|---|---|---|---|
| 1. 不重写上游 packages/workflow/* / packages/coze-* | 已遵守 | 本次 diff 全程没有触碰 `packages/workflow/*`、`packages/coze-*`、`packages/agent-ide/*`、`packages/foundation/*`、`packages/arch/foundation-sdk` 等任何上游目录；所有适配工作通过新增的 `packages/atlas-foundation-bridge` 桥接包与 rsbuild alias 完成。 | n/a（约束类） |
| 2. 不引入新依赖 | 已遵守 | `app-web/package.json` 仅新增 `"@atlas/foundation-bridge": "workspace:*"`，是 workspace 内部包；React / pnpm overrides 等版本未变。 | n/a（约束类） |
| 3. 节点 ID 双轨制 | 已修复 | 1) 在 `WorkflowCanvasJsonBridge.TryResolveNodeType` 增加 JSON 字符串数字（`"3"`）反向解析分支。<br>2) `CozeWorkflowCompatControllerBase.SaveWorkflow` 与 `ValidateTree` 入参 schema 落库前调用新增的 `NormalizeCanvasJsonFromCoze`，把 type 统一规整为 Atlas `WorkflowNodeType` 整型。 | `tests/Atlas.SecurityPlatform.Tests/Workflows/CozeCanvasNodeTypeRoundtripTests.cs`（覆盖 5 种 type 形态 + 未知节点拒绝 + 整型规整） |
| 4. i18n 双向 | 已加固 | `WorkflowRuntimeBoundary.toCozeLocale` 抽为 named export 并接管未知区域、空字符串、`zh-Hans` / `zh-TW` / `en-GB` 等变体；非中文非英文一律走 `en` 兜底。 | `apps/app-web/src/app/workflow-runtime-boundary.spec.tsx → describe("toCozeLocale")` |
| 5. foundation-sdk 行为差异 | 已加固 | `atlas-foundation-bridge.useSpace` 在宿主只注入 `id` 时给出 cozelib 必填的 `space_type / space_mode / role_type` 兜底（默认 Team / Normal / Member），避免 cozelib 在严格相等判断分支降级为不可用。 | `packages/atlas-foundation-bridge/src/__tests__/bridge.spec.ts` 新增"useSpace 字段缺失"用例 |

### M1 新增 DTO 命名规范

- M1 新增的 3 个对外 DTO **不再带 `V2` 后缀**，使用业务正式命名：
  - `WorkflowVariableTreeDto`
  - `WorkflowNodeExecutionHistoryDto`
  - `WorkflowHistorySchemaDto`
- 与之相关的接口方法签名、服务实现、控制器返回类型、xUnit 测试同步使用上述名称。
- **M7（已完成）**：后端 C# 类型与文件已统一为 `DagWorkflow*`（控制器 `DagWorkflowController`、服务 `IDagWorkflowQueryService` / `IDagWorkflowCommandService` / `IDagWorkflowExecutionService`、DTO `DagWorkflow*Request` / `DagWorkflow*Dto`）。**HTTP 路由**仍为 `api/v2/workflows`：其中 **`v2` 表示 REST API 版本号**，与 `api/v1/*` 并行，**不是**产品「Workflow V2」语义；详见 [`docs/plan-m7-workflowv2-rename.md`](plan-m7-workflowv2-rename.md) 策略 1。
- 前端 `@coze-arch/idl` 自动生成物仍保留上游 `WorkflowV2*` 命名；Atlas 侧可选用 [`@coze-arch/idl/dag-workflow-aliases.ts`](../src/frontend/packages/arch/idl/src/dag-workflow-aliases.ts) 语义别名，避免手改 `auto-generated`。

### DagWorkflow 名称规则

适用范围：

- `POST /api/v2/workflows` 的 `DagWorkflowCreateRequest.Name`
- `PUT /api/v2/workflows/{id}/meta` 的 `DagWorkflowUpdateMetaRequest.Name`

校验规则：

- 正则：`^[A-Za-z][A-Za-z0-9_]{0,29}$`（首字符必须是英文字母，仅允许字母、数字、下划线）
- 长度：1..30（与 Coze 上游 `WORKFLOW_NAME_MAX_LEN = 30` 对齐）
- 与前端真源对齐：[`src/frontend/packages/workflow/base/src/constants/index.ts`](../src/frontend/packages/workflow/base/src/constants/index.ts) 的 `WORKFLOW_NAME_REGEX` + `WORKFLOW_NAME_MAX_LEN`

错误码（FluentValidation `WithErrorCode` 挂在 `Name` 字段）：

- `DAG_WORKFLOW_NAME_LENGTH`：长度超过 30
- `DAG_WORKFLOW_NAME_FORMAT`：字符集不符合（数字开头、含中文、含 `-` 等）

i18n 资源 key（`Atlas.Application/Resources/Messages.{zh-CN,en-US}.resx`）：

- `DagWorkflowNameFormat`
- `DagWorkflowNameLength`
- 注：`Atlas.Application` 内的 Validator 当前为无参构造、不注入 `IStringLocalizer`，错误消息为 `WithMessage` 直接挂中文文案 + `WithErrorCode` 挂错误码；需要英文回写时由控制器层基于 `ErrorCode` 通过 `IStringLocalizer<Messages>` 解析对应 key。

兼容层差异（重要）：

- `POST /api/workflow_api/create_workflow` 由 [`CozeWorkflowCompatControllerBase`](../src/backend/Atlas.Presentation.Shared/Controllers/Ai/CozeWorkflowCompatControllerBase.cs) 直接调用 `IDagWorkflowCommandService.CreateAsync`，**不**经过 `DagWorkflowCreateRequestValidator`；
- 当 Coze 兼容层入参 `name` 为空时，会用中文默认名 `"未命名工作流"` 兜底，违反上述正则；
- 这是 Coze 上游交互的既有契约；统一 REST 与 Coze 兼容层的命名展示由 `DagWorkflow*` 类型与本文档对齐。

### M2 新端点 ↔ 上游 Thrift 字段差异说明

> 详见 [`docs/coze-api-gap.md`](coze-api-gap.md)。M2 一次性把 `batch_delete / delete_strategy / copy_wk_template / example_workflow_list / apiDetail / upload/auth_token / sign_image_url / list_publish_workflow / get_async_sub_process` 9 个端点从 fallback 升级为真实实现。

### M3 新端点

- `POST /api/draftbot/get_draft_bot_list`：走 `ITeamAgentService.GetPagedAsync`，返回 `{total, list[], has_more}`，与 Coze `DraftBotList` 字段对齐。
- `POST /api/draftbot/get_display_info`：走 `ITeamAgentService.GetByIdAsync`，返回 `{bot_id, name, description, icon_url, agent_type, publish_status, schema_config_json, ...}`。
- `POST /api/playground_api/space/info`：走 `IWorkspacePortalService.ListWorkspacesAsync`，返回 `{data: {id, name, description, icon_url, space_type, role_type, space_mode}}`。

### M5 等保 / 多租户 / 鉴权

- 全部 `/api/workflow_api/*`、`/api/playground_api/*`、`/api/draftbot/*`、`/api/bot/*`、`/v1/workflows/*`、`/v1/workflow/*` 路由均挂 `[Authorize]`，由 JWT 中间件强制认证（PAT 走 `PatAuthenticationHandler`）。
- 多租户上下文继续由 `TenantContextMiddleware` 强制：`X-Tenant-Id` Header 必须与 JWT `tenant_id` Claim 一致；不一致返回 `403 CROSS_TENANT_FORBIDDEN`。
- Coze 兼容层写动作（`save / publish / delete / test_run / cancel / nodeDebug / copy / batch_delete / copy_wk_template`）全部通过 `TryWriteAuditAsync` 触发 `IAuditWriter`，actor 为 JWT identity，target 为 `workflow:<id>`，action 为 `coze_workflow.<verb>`，符合等保2.0 关于「关键操作可追溯」的要求。
- `/v1/workflow/run` / `/v1/workflow/stream_run` / `/v1/workflow/stream_resume` / `/v1/workflows/chat` 等 OpenAPI 路由：当前在 Coze 兼容层为 fallback；真实运行入口走 `OpenWorkflowsController`（使用 `PatAuthenticationHandler` 启用 PAT 认证），见 [`OpenWorkflowsController.cs`](../src/backend/Atlas.AppHost/Controllers/Open/OpenWorkflowsController.cs)。

### M3 前端 cozelib 壳接入开关

- `WorkspaceWorkflowWorkbenchRoute` 当前默认走 Atlas 自研 + cozelib 工作流编辑器。`@coze-studio/workspace-adapter` 的 `develop`/`library` 与 `@coze-agent-ide/entry-adapter` 的 `BotEditor` 在 `@coze-arch/bot-api` shim 完整对齐之后，再启用 `?shell=coze` 切换。开关位与 alias 已在 `apps/app-web/rsbuild.config.ts` 与 `WorkflowRuntimeBoundary` 中预留。

### M1 新端点 ↔ 上游 Thrift 字段差异说明

| 兼容路由 | 上游 Thrift Method | Atlas 实现差异 |
|---|---|---|
| `POST /api/workflow_api/validate_tree` | `WorkflowService.ValidateTree`（`workflow_svc.thrift`） | Atlas 暂不支持 `bind_project_id` / `bind_bot_id` 真实校验，仅做透传与 schema 结构性校验；`schema` 为空且传 `workflow_id` 时回退使用当前草稿。 |
| `POST /api/workflow_api/node_panel_search` | `WorkflowService.NodePanelSearch` | Atlas 节点目录与上游 `NodePanelPlugin` 不同源，命中条目以 Coze `Workflow` 结构返回 `workflow_id = node_type_code`、`node_type = node_type_code`、`category = WorkflowNodeMetadata.Category`，前端需要据此渲染为节点条目。`page_or_cursor` 视为页码字符串。 |
| `POST /api/workflow_api/history_schema` | `WorkflowService.GetHistorySchema` | 优先级：`execute_id` → `commit_id` → 最新版本 → 当前草稿；`flow_mode/bind_biz_*` 字段为占位值；`commit_id` 同时接受 `WorkflowVersion.Id` 与 `version_number` 两种字符串形式。 |
| `GET /api/workflow_api/get_node_execute_history` | `WorkflowService.GetNodeExecuteHistory` | 字段命名沿用上游 `NodeResult`（`nodeId`、`NodeType` 等大小写）；`extra` 字段在 Atlas 实现中是 JSON 字符串，结构为 `{"input": <inputJson>, "output": <outputJson>, "variables": <ctxSnapshotJson>}`，便于调试面板直接消费上下文变量快照。 |
| `POST /api/workflow_api/get_trace` | `WorkflowService.GetTraceSDK` | spans 在 Atlas 中按 `step.StartedAt` 升序排列，并在头部追加根 span（`is_entry=true`、`type=Workflow`）；`spans[].extra.variables` 为「该步骤完成前累积的执行上下文」JSON 字符串，前端调试面板用于渲染变量快照。 |
| `GET /api/v1/ai-variables/workflows/{workflowId}/variable-tree` | 无对应上游 IDL（Atlas 自有协议） | 返回 `WorkflowVariableTreeDto`：按 `WorkflowVariableScopeKind`（Node=0/Global=1/System=2/Conversation=3/User=4）分组，每个分组含 `fields[]`（`key`/`name`/`dataType`/`description`/`children`）。供节点配置面板与 Prompt 编辑器消费。`nodeKey` 为空时返回完整画布；非空时只返回当前节点上游可见变量。 |

## 写接口安全头基线

- 当前仓库已废止公共 `Idempotency-Key` / `X-CSRF-TOKEN` 机制。
- 所有写接口默认不再要求这两个请求头。
- `GET /api/v1/secure/antiforgery` 已移除。
- 旧版 `.http`、E2E、前端 API client 如仍依赖这两个头，应以当前实现为准完成迁移。

## Workflow V2 API（Coze 40+ 节点复刻）

### 工作流详情读取语义

- `GET /api/v2/workflows/{id}`
- 查询参数：
  - `source=draft|published`
  - `versionId=<WorkflowVersion.Id>`
- 语义：
  - 默认读取当前草稿。
  - `source=published` 读取最新发布版本。
  - `versionId` 优先级高于 `source`，用于只读查看指定版本。
  - 请求发布态但工作流尚未发布时，返回 `404 NotFound`。

### 节点元数据目录

- `GET /api/v2/workflows/node-types`
- 返回内容包含：
  - `key`、`name`、`category`、`description`
  - `ports[]`（方向、数据类型、必填、连接上限）
  - `configSchemaJson`
  - `uiMeta`（icon、color、supportsBatch）

### 节点模板列表

- `GET /api/v2/workflows/node-templates`
- 返回每个节点的真实默认配置模板（用于前端动态表单初始化）
- `Llm` 默认配置至少包含：
  - `provider`
  - `model`
  - `prompt`
  - `systemPrompt`
  - `temperature`
  - `maxTokens`
  - `stream`
  - `outputKey`

### 模型目录来源

- 工作流模型节点前端候选源固定复用 `GET /model-configs/enabled`。
- Coze adapter / playground 内部统一将已启用模型配置映射为 `developer_api.Model` 风格对象：
  - `model_type` = `ModelConfigDto.Id`
  - `name` / `model_name` = `modelId || defaultModel || name`
  - `endpoint_name` / `model_class_name` = `providerType`
  - `model_params` 至少补齐 `temperature`、`max_tokens`、`response_format`
  - `model_ability` 从 `enableTools` / `enableVision` / `enableReasoning` 等能力字段派生
- 工作流节点面板中的模型选择不再要求手填 provider / model 字符串，统一从模型中心已启用配置中选择。

### 执行恢复（流式）

- `POST /api/v2/workflows/executions/{executionId}/stream-resume`
- SSE 事件补充：
  - `execution_resume_start`
  - `node_start`
  - `node_output`
  - `node_complete`
  - `node_failed`
  - `execution_complete`
  - `execution_failed`
  - `execution_interrupted`

### 执行入口（run/stream）source 语义

- `POST /api/v2/workflows/{id}/run`
- `POST /api/v2/workflows/{id}/stream`
- 请求体新增可选字段：`source`
  - `published`：按最新发布版本运行（默认）
  - `draft`：按当前草稿运行
- `source=published` 且 workflow 尚未发布时，返回 `VALIDATION_ERROR`。

### 单节点调试语义

- `POST /api/v2/workflows/{id}/debug-node`
- 请求体新增可选字段：
  - `source=draft|published`
  - `versionId=<WorkflowVersion.Id>`
- 语义：
  - 默认调试当前草稿。
  - `source=published` 调试最新发布版本。
  - `versionId` 用于指定历史发布版本的只读调试。

### 画布模型（CanvasSchema）

- `nodes[]`：支持 `childCanvas`（Batch/子图）
- `connections[]`：支持 `condition`
- `NodeSchema` 扩展：
  - `inputTypes` / `outputTypes`
  - `inputSources` / `outputSources`

### 端点连线约束（Coze 原生画布）

- 连线模型固定为端点级：
  - `fromNode` + `fromPort`
  - `toNode` + `toPort`
- `fromPort` / `toPort` 必须来自 `node-types[].ports[].key`。
- 连线方向必须满足 `Output -> Input`。
- 默认禁止节点自环连接（可在后续策略中显式放开）。
- 同一对端点（`fromNode:fromPort -> toNode:toPort`）不允许重复边。
- 连接上限遵循端口元数据：
  - 出端口：`ports[].maxConnections`
  - 入端口：`ports[].maxConnections`
- 类型兼容遵循严格规则：
  - 同类型直接允许；
  - 或命中显式白名单（`any/json/object/array/unknown` 的可兼容集合）；
  - 未命中白名单即拒绝连接。

### 画布保存前一致性校验

- 节点级：
  - `configSchemaJson` 字段校验（`required/type/enum/range/pattern/items`）
  - `inputMappings` 键必须为节点输入端口键
- 连线级：
  - 端口存在性（缺失端口视为非法）
  - 方向合法性（Output -> Input）
  - 重复边拦截
  - 连接数量上限
  - 类型兼容
- 画布级：
  - 指向不存在节点的悬空连接拦截

### 未保存画布校验

- `POST /api/v2/workflows/{id}/validate`
- 请求体支持两种形式：
  - `canvasJson`
  - `canvas`（结构化 `CanvasSchema`）
- 若请求体未提供画布，则回退校验已保存草稿。
- 返回：
  - `isValid`
  - `errors[]`
  - `errors[].code/message/nodeKey/sourcePort/targetPort`

### 执行 Trace

- `GET /api/v2/workflows/executions/{executionId}/trace`
- 返回：
  - `steps[]`：节点级执行时间线
  - `edgeStatuses[]`：边级运行状态回放
- `edgeStatuses[]` 字段：
  - `sourceNodeKey`
  - `sourcePort`
  - `targetNodeKey`
  - `targetPort`
  - `status`
    - `0 = idle`
    - `1 = success`
    - `2 = skipped`
    - `3 = failed`
    - `4 = incomplete`
  - `reason`

### 历史草稿兼容策略

- 对历史草稿中缺失 `fromPort` / `toPort` 或端口键失效的连接，编辑器加载时执行迁移归一：
  - 输出端口回退到节点默认输出端口；
  - 输入端口回退到节点默认输入端口；
  - 迁移后若形成重复边，保留一条并给出迁移提示。
- 无法归一的连接在保存/发布前由一致性校验阻断，并输出定位信息。

### 节点分类（7 类）

- Flow Control：Start/End/If/Loop/Batch/Break/Continue
- AI：LLM/Intent Detector/Question Answer
- Data：Code/Text/JSON/Variable/Set Variable
- External：Plugin/HTTP/SubWorkflow
- Knowledge：Dataset Search/Dataset Write/LTM
- Database：Query/Insert/Update/Delete/Custom SQL
- Conversation：Conversation CRUD + History + Message CRUD + Input/Output

## AI 资源库与知识库 API

### 适用主机

- `PlatformHost` 与 `AppHost` 均提供同构接口。
- `app-web` 在 `platform` 与 `direct` 两种运行模式下统一消费以下契约。

### 资源库列表

- `GET /api/v1/ai-workspaces/library`
- 查询参数：
  - `keyword`
  - `resourceType`
  - `pageIndex`
  - `pageSize`
- 返回：`ApiResponse<AiLibraryPagedResult>`
- `AiLibraryPagedResult` 字段：
  - `items[]`
  - `totalCount`
  - `pageIndex`
  - `pageSize`
- `items[]` 每项至少包含：
  - `id`
  - `name`
  - `description`
  - `resourceType`
  - `resourceSubType`
  - `status`
  - `documentCount`
  - `chunkCount`
  - `updatedAt`

### 资源库导入 / 导出 / 移动

- `POST /api/v1/ai-workspaces/library/imports`
  - 请求体：`AiLibraryImportRequest`
    - `resourceType`：当前支持 `workflow`、`plugin`、`knowledge-base`、`database`
    - `libraryItemId`
    - `targetAppId?`
    - `targetWorkspaceId?`
  - 返回：`ApiResponse<AiLibraryMutationResult>`
- `POST /api/v1/ai-workspaces/library/exports`
  - 请求体：`AiLibraryMutationRequest`
    - `resourceType`
    - `resourceId`
  - 返回：`ApiResponse<AiLibraryMutationResult>`
- `POST /api/v1/ai-workspaces/library/moves`
  - 请求体：`AiLibraryMutationRequest`
    - `resourceType`
    - `resourceId`
  - 返回：`ApiResponse<AiLibraryMutationResult>`
- `AiLibraryMutationResult` 字段：
  - `resourceId`
  - `resourceType`
  - `libraryItemId`

### Explore / Marketplace 收口语义

- `PlatformHost` 与 `AppHost` 均提供同构接口；`app-web` 直连模式默认命中 `AppHost`。
- 插件市场前端统一消费：
  - `GET /api/v1/ai-marketplace/products`
  - `GET /api/v1/ai-marketplace/products/{id}`
  - `POST /api/v1/ai-marketplace/products/{id}/favorite`
  - `DELETE /api/v1/ai-marketplace/products/{id}/favorite`
  - `POST /api/v1/ai-marketplace/products/{id}/download`
- 插件市场只展示 `productType = Plugin` 且已发布商品；导入到 Studio 时：
  1. 先记录 `download`
  2. 再复用 `POST /api/v1/ai-workspaces/library/imports`
  3. 导入成功后前端跳转到 `studio/plugins/:id`
- 模板市场当前仍消费：
  - `GET /api/v1/templates`
  - `GET /api/v1/templates/{id}`
  - `POST /api/v1/templates/{id}/instantiate`
- 模板市场默认只展示 `TemplateCategory.Flow`；一键创建工作流时：
  1. 先读取模板详情
  2. 调用 `instantiate` 获取 `schemaJson`
  3. 复用 `POST /api/v2/workflows` 创建草稿
  4. 复用 `PUT /api/v2/workflows/{id}/draft` 保存 `canvasJson`
  5. 成功后跳转到 `work_flow/:id/editor` 或 `chat_flow/:id/editor`
- `app-web` 前端收口路由：
  - `/apps/:appKey/explore/plugin`
  - `/apps/:appKey/explore/plugin/:productId`
  - `/apps/:appKey/explore/template`
  - `/apps/:appKey/explore/template/:templateId`

### AI 数据库补充契约

- `POST /api/v1/ai-databases/schema-validations`
  - 用途：在新建数据库前对 `tableSchema` 做即时校验，不依赖已存在的数据库 ID。
  - 请求体：
    - `tableSchema`
  - 返回：
    - `isValid`
    - `errors[]`

### 知识库 CRUD

- `GET /api/v1/knowledge-bases`
- `GET /api/v1/knowledge-bases/{id}`
- `POST /api/v1/knowledge-bases`
- `PUT /api/v1/knowledge-bases/{id}`
- `DELETE /api/v1/knowledge-bases/{id}`
- `KnowledgeBaseCreateRequest` / `KnowledgeBaseUpdateRequest`：
  - `name`
  - `description`
  - `type`，枚举值固定为 `Text`、`Table`、`Image`
- `KnowledgeBaseDto`：
  - `id`
  - `name`
  - `description`
  - `type`
  - `documentCount`
  - `chunkCount`
  - `createdAt`

### 文档管理

- `GET /api/v1/knowledge-bases/{id}/documents`
- `POST /api/v1/knowledge-bases/{id}/documents`
- `DELETE /api/v1/knowledge-bases/{id}/documents/{docId}`
- `GET /api/v1/knowledge-bases/{id}/documents/{docId}/progress`
- `POST /api/v1/knowledge-bases/{id}/documents/{docId}/resegment`
- `POST /api/v1/knowledge-bases/{id}/documents` 支持两种导入方式：
  - `multipart/form-data` 上传 `file`
  - 传入已存在文件的 `fileId`
- `KnowledgeDocumentDto`：
  - `id`
  - `knowledgeBaseId`
  - `fileId`
  - `fileName`
  - `contentType`
  - `fileSizeBytes`
  - `status`
  - `errorMessage`
  - `chunkCount`
  - `createdAt`
  - `processedAt`
- `DocumentProgressDto`：
  - `id`
  - `status`
  - `chunkCount`
  - `errorMessage`
  - `processedAt`
- `DocumentResegmentRequest`：
  - `chunkSize`
  - `overlap`
  - `strategy`

### 分片管理

- `GET /api/v1/knowledge-bases/{id}/documents/{docId}/chunks`
- `POST /api/v1/knowledge-bases/{id}/chunks`
- `PUT /api/v1/knowledge-bases/{id}/chunks/{chunkId}`
- `DELETE /api/v1/knowledge-bases/{id}/chunks/{chunkId}`
- `DocumentChunkDto`：
  - `id`
  - `knowledgeBaseId`
  - `documentId`
  - `chunkIndex`
  - `content`
  - `startOffset`
  - `endOffset`
  - `hasEmbedding`
  - `createdAt`

### 检索测试

- `POST /api/v1/knowledge-bases/{id}/retrieval-test`
- 请求体：`KnowledgeRetrievalTestRequest`
  - `query`
  - `topK`
- 返回：`ApiResponse<RagSearchResult[]>`
- `RagSearchResult`：
  - `knowledgeBaseId`
  - `documentId`
  - `chunkId`
  - `content`
  - `score`
  - `documentName`
  - `documentCreatedAt`

## AI Platform Round 2 落仓约束

### 宿主分工

- `PlatformHost`：
  - 工作台 / 资源库 / 市场 / 设计态 / 发布态 / PAT / OpenAPI 项目管理
- `AppHost`：
  - 对话 / Agent 调试 / Workflow 运行 / Embed Chat / OpenAPI 运行

### 统一资源域

- 智能体：`Agent`
- 应用：`AiApp`
- 工作流：`WorkflowMeta`、`WorkflowDraft`、`WorkflowVersion`
- 插件：`AiPlugin`、`AiPluginApi`
- 知识库：`KnowledgeBase`、`KnowledgeDocument`、`DocumentChunk`
- 数据库：`AiDatabase`、`AiDatabaseRecord`
- 变量：`AiVariable`、`AiVariableInstance`
- 会话：`Conversation`、`ConversationSection`、`ChatMessage`、`ChatRunRecord`

### 结构化设计约束

- 编辑态读草稿，运行态读发布快照。
- 旧 JSON 绑定字段仅保留为兼容缓存，不再作为唯一事实来源。
- 新增绑定事实表：
  - `AgentWorkflowBinding`
  - `AgentDatabaseBinding`
  - `AgentVariableBinding`
  - `AgentPromptBinding`
  - `AiAppResourceBinding`
  - `AiAppConversationTemplate`
  - `AiAppConnectorBinding`
  - `WorkflowReference`
  - `WorkflowPublishedReference`

### 文档索引

- 第 2 轮具体落仓方案见：[plan-coze-atlas-round2.md](./plan-coze-atlas-round2.md)
- 资源关系图见：[ai-platform-er.md](./ai-platform-er.md)

## App Workbench API（AppHost-only）

### AI Assistants 兼容入口（AppHost）

- `AppHost` 提供与平台端兼容的 `GET/POST/PUT/DELETE /api/v1/ai-assistants...` 接口。
- 请求 / 响应结构与 `PlatformHost` 现有 `ai-assistants` 契约保持一致，用于 `app-web` 直连模式。

### Draft Agent 与默认工作流绑定

- `GET /api/v1/draft-agents`
- `GET /api/v1/draft-agents/{id}`
- `POST /api/v1/draft-agents`
- `PUT /api/v1/draft-agents/{id}`
- `POST /api/v1/draft-agents/{id}/workflow-bindings`
- `AgentDetail` / `AgentCreateRequest` / `AgentUpdateRequest` 新增字段：
  - `defaultWorkflowId`
  - `defaultWorkflowName`
  - `avatarUrl`
  - `personaMarkdown`
  - `goals`
  - `replyLogic`
  - `outputFormat`
  - `constraints`
  - `openingMessage`
  - `presetQuestions[]`
  - `knowledgeBaseIds[]`
  - `pluginBindings[]`
- `WorkflowBindingUpdateRequest`
  - `workflowId`
- `WorkflowBindingDto`
  - `workflowId`
  - `workflowName`

### Agent Session 与工作台消息

- `POST /api/v1/agent-sessions`
- `GET /api/v1/agent-sessions/{sessionId}/messages`
- `POST /api/v1/agent-sessions/{sessionId}/messages`
- `POST /api/v1/conversations/{id}/clear-context`
- `POST /api/v1/conversations/{id}/clear-history`
- `DELETE /api/v1/conversations/{id}`
- `ConversationAppendMessageRequest`
  - `role`：`system | user | assistant | tool`
  - `content`
  - `metadata?`
- 工作流显式调用结果以 `tool` 消息追加到同一会话历史，用于 App 端工作台展示与继续追问。

### 工作台工作流执行（AppHost 聚合）

- `POST /api/v1/workflow-playground/{id}/execute`
- 说明：
  - 此接口用于 App 端聊天工作台显式执行已绑定工作流。
  - `app-web` 不再自行拼装 `/api/v2/workflows/{id}/run + process + trace`。
  - AppHost 负责把 incident 描述标准化为运行输入，并一次性返回执行摘要与 trace。
- `WorkflowWorkbenchExecuteRequest`
  - `incident`
  - `source?`：`draft | published`，默认 `draft`
- `WorkflowWorkbenchExecuteResultDto`
  - `execution`
  - `trace?`
- `execution`
  - `executionId`
  - `status`
  - `outputsJson`
  - `errorMessage`
- `trace`
  - `executionId`
  - `status`
  - `startedAt`
  - `completedAt`
  - `durationMs`
  - `steps[]`
- `steps[]`
  - `nodeKey`
  - `status`
  - `nodeType`
  - `durationMs`
  - `errorMessage`

### App Builder 配置与预览运行

- `GET /api/v1/ai-apps/{id}/builder-config`（PlatformHost）
- `PUT /api/v1/ai-apps/{id}/builder-config`（PlatformHost）
- `POST /api/v1/ai-apps/{id}/preview-run`（AppHost）
- `AiAppBuilderConfig`
  - `inputs[]`
  - `outputs[]`
  - `boundWorkflowId`
  - `layoutMode`：`form | chat | hybrid`
- `AiAppPreviewRunRequest`
  - `inputs`：`Record<string, unknown>`
- `AiAppPreviewRunResult`
  - `outputs`
  - `trace?`

### Workflow 依赖查询（PlatformHost / AppHost）

- `GET /api/v2/workflows/{id}/dependencies`
- `PlatformHost` 与 `AppHost` 均提供同构接口；`app-web` 直连模式默认命中 `AppHost`。
- 返回：`ApiResponse<DagWorkflowDependencyDto>`
- `DagWorkflowDependencyDto`
  - `workflowId`
  - `subWorkflows[]`
  - `plugins[]`
  - `knowledgeBases[]`
  - `databases[]`
  - `variables[]`
  - `conversations[]`
- `DagWorkflowDependencyItemDto`
  - `resourceType`
  - `resourceId`
  - `name`
  - `description`
  - `sourceNodeKeys[]`
    - 当前依赖被哪些节点引用
    - `references` 侧栏、问题面板与节点定位联动都依赖该字段

### Agent 配置化绑定

- `AgentDetail` / `AgentCreateRequest` / `AgentUpdateRequest` 扩展：
  - `knowledgeBindings[]`
    - `knowledgeBaseId`
    - `isEnabled`
    - `invokeMode`
    - `topK`
    - `scoreThreshold`
    - `enabledContentTypes[]`
    - `rewriteQueryTemplate`
  - `pluginBindings[]`
    - `pluginId`
    - `sortOrder`
    - `isEnabled`
    - `toolConfigJson`
    - `toolBindings[]`
  - `toolBindings[]`
    - `apiId`
    - `isEnabled`
    - `timeoutSeconds`
    - `failurePolicy`
    - `parameterBindings[]`
  - `parameterBindings[]`
    - `parameterName`
    - `valueSource`
    - `literalValue`
    - `variableKey`
  - `databaseBindings[]`
    - `databaseId`
    - `alias`
    - `accessMode`
    - `tableAllowlist[]`
    - `isDefault`
  - `variableBindings[]`
    - `variableId`
    - `alias`
    - `isRequired`
    - `defaultValueOverride`
- 兼容投影：
  - `knowledgeBaseIds[]` 继续保留
  - `databaseBindingIds[]` 继续保留
  - `variableBindingIds[]` 继续保留
  - `ToolConfigJson` 继续作为数据库列存在，但前端不直接编辑裸 JSON

## Workspace IDE API（PlatformHost / AppHost）

### 工作空间摘要

- `GET /api/v1/workspace-ide/summary`
- 返回：`ApiResponse<WorkspaceIdeSummaryResponse>`
- `WorkspaceIdeSummaryResponse`
  - `appCount`
  - `agentCount`
  - `workflowCount`
  - `chatflowCount`
  - `pluginCount`
  - `knowledgeBaseCount`
  - `databaseCount`
  - `favoriteCount`
  - `recentCount`

### 工作空间统一资源列表

- `GET /api/v1/workspace-ide/resources`
- 查询参数：
  - `keyword`
  - `resourceType`：`agent | app | workflow | chatflow | plugin | knowledge-base | database`
  - `favoriteOnly`
  - `pageIndex`
  - `pageSize`
- 返回：`ApiResponse<PagedResult<WorkspaceIdeResourceCardResponse>>`
- `WorkspaceIdeResourceCardResponse`
  - `resourceType`
  - `resourceId`
  - `name`
  - `description`
  - `icon`
  - `status`
  - `publishStatus`
  - `updatedAt`
  - `isFavorite`
  - `lastOpenedAt`
  - `lastEditedAt`
  - `entryRoute`
  - `badge`
  - `linkedWorkflowId`

### Dashboard 统计与发布聚合

- `GET /api/v1/workspace-ide/dashboard-stats`
- 返回：`ApiResponse<WorkspaceIdeDashboardStatsResponse>`
- `WorkspaceIdeDashboardStatsResponse`
  - `agentCount`
  - `appCount`
  - `workflowCount`
  - `enabledModelCount`
  - `pluginCount`
  - `knowledgeBaseCount`
  - `pendingPublishItems[]`
  - `recentActivities[]`
- `WorkspaceIdePendingPublishItem`
  - `resourceType`：`agent | app | workflow | plugin`
  - `resourceId`
  - `resourceName`
  - `updatedAt`
- `GET /api/v1/workspace-ide/publish-center/items`
- 查询参数：
  - `resourceType?`：`agent | app | workflow | plugin`
- 返回：`ApiResponse<WorkspaceIdePublishCenterItemResponse[]>`
- `WorkspaceIdePublishCenterItemResponse`
  - `resourceType`
  - `resourceId`
  - `resourceName`
  - `currentVersion`
  - `draftVersion`
  - `lastPublishedAt`
  - `status`：`draft | published | outdated`
  - `apiEndpoint`
  - `embedToken?`

### 资源引用关系

- `GET /api/v1/workspace-ide/resources/{resourceType}/{resourceId}/references`
- `resourceType` 支持：`model-config | plugin | knowledge-base | database | variable | workflow | agent`
- 返回：`ApiResponse<WorkspaceIdeResourceReferenceResponse[]>`
- `WorkspaceIdeResourceReferenceResponse`
  - `referrerType`：`agent | app | workflow`
  - `referrerId`
  - `referrerName`
  - `bindingField`

### 工作空间应用复合创建

- `POST /api/v1/workspace-ide/apps`
- 请求体：`WorkspaceIdeCreateAppRequest`
  - `name`
  - `description`
  - `icon`
- 语义：
  - 同时创建 `ai-app`
  - 同时创建一个标准 `workflow`
  - 自动把该 `workflowId` 绑定到 `ai-app`
- 返回：`ApiResponse<WorkspaceIdeCreateAppResult>`
  - `appId`
  - `workflowId`
  - `entryRoute`

### 收藏与最近编辑

- `PUT /api/v1/workspace-ide/favorites/{resourceType}/{resourceId}`
- 请求体：`WorkspaceIdeFavoriteUpdateRequest`
  - `isFavorite`
- `POST /api/v1/workspace-ide/activities`
- 请求体：`WorkspaceIdeActivityCreateRequest`
  - `resourceType`
  - `resourceId`
  - `resourceTitle`
  - `entryRoute`

## 组织概览 API（TenantApp V2）

### 组织概览

- `GET /api/v2/tenant-app-instances/{appId}/organization/overview`
- 返回：`ApiResponse<AppOrganizationOverviewResponse>`
- `AppOrganizationOverviewResponse`
  - `appId`
  - `memberCount`
  - `roleCount`
  - `departmentCount`
  - `positionCount`
  - `projectCount`
  - `uncoveredMemberCount`
  - `recentMembers[]`
  - `recentRoles[]`
  - `recentDepartments[]`
  - `recentPositions[]`
- `AppOrganizationOverviewItem`
  - `id`
  - `title`
  - `subtitle`
  - `meta`

## Coze 平台第三阶段 API（M1：平台/个人级运营内容）

> 草案 → 已落地。完整 mock 协议见 [`docs/mock-api-protocols.md`](mock-api-protocols.md)，
> 后台实现位于 `Atlas.PlatformHost/Controllers/{HomeContent,Community,PlatformGeneral,MarketSummary,MeSettings}Controller.cs`，
> Service 实现 `Atlas.Infrastructure.Services.Coze.InMemory*`，注册在 `PlatformServiceCollectionExtensions`。

### 工作空间首页（PRD 01）

- `GET /api/v1/workspaces/{workspaceId}/home/banner` → `ApiResponse<HomeBannerDto>`
- `GET /api/v1/workspaces/{workspaceId}/home/tutorials` → `ApiResponse<HomeTutorialCardDto[]>`
- `GET /api/v1/workspaces/{workspaceId}/home/announcements?tab=all|notice&keyword&pageIndex&pageSize` → `ApiResponse<PagedResult<HomeAnnouncementItemDto>>`
- `GET /api/v1/workspaces/{workspaceId}/home/recommended-agents` → `ApiResponse<HomeRecommendedAgentDto[]>`
- `GET /api/v1/workspaces/{workspaceId}/home/recent-activities` → `ApiResponse<HomeRecentActivityDto[]>`（按当前用户 + workspaceId）

DTO（位于 `Atlas.Application.Coze.Models`）：

- `HomeBannerDto { heroTitle, heroSubtitle, ctaList[], backgroundImageUrl? }`
- `HomeBannerCtaDto { key:"create"|"tutorial"|"docs", label }`
- `HomeTutorialCardDto { id, title, description, iconKey:"intro"|"quickstart"|"release", link }`
- `HomeAnnouncementItemDto { id, title, summary, publisher, publishedAt, tag?, link }`
- `HomeRecommendedAgentDto { id, name, description, iconUrl?, publisherName, views, likes, link }`
- `HomeRecentActivityDto { id, type:"agent"|"app"|"workflow", name, description?, updatedAt, entryRoute }`

### 作品社区（PRD 02-7.9）

- `GET /api/v1/community/works?keyword&pageIndex&pageSize` → `ApiResponse<PagedResult<CommunityWorkItemDto>>`
- `CommunityWorkItemDto { id, title, summary, authorDisplayName, coverUrl?, likes, views, publishedAt, tags[] }`

### 通用管理（PRD 02-7.12）

- `GET /api/v1/platform/general/notices` → `ApiResponse<PlatformNoticeDto[]>`
- `GET /api/v1/platform/general/branding` → `ApiResponse<PlatformBrandingDto>`
- `PlatformNoticeDto { id, title, message, level:"info"|"warning"|"error", publishedAt }`
- `PlatformBrandingDto { logoUrl?, productName, productSlogan }`

### 模板/插件商店分类摘要（PRD 02-7.7、7.8）

- `GET /api/v1/market/templates/summary?keyword&pageIndex&pageSize` → `ApiResponse<PagedResult<MarketCategorySummaryDto>>`
- `GET /api/v1/market/plugins/summary?keyword&pageIndex&pageSize` → `ApiResponse<PagedResult<MarketCategorySummaryDto>>`
- `MarketCategorySummaryDto { id, name, count, description? }`

完整模板/插件搜索仍走 `TemplatesController` / `AiMarketplaceController`。

### 个人设置（PRD 03 头像入口）

- `GET /api/v1/me/settings/general` → `ApiResponse<MeGeneralSettingsDto>`
- `PATCH /api/v1/me/settings/general` body `MeGeneralSettingsUpdateRequest` → `ApiResponse<MeGeneralSettingsDto>`
- `GET /api/v1/me/settings/publish-channels` → `ApiResponse<MePublishChannelDto[]>`
- `GET /api/v1/me/settings/datasources` → `ApiResponse<MeDataSourceDto[]>`
- `DELETE /api/v1/me/account` → `ApiResponse<{ success: boolean }>`（M1 仅清空偏好，不真删账号）

DTO：

- `MeGeneralSettingsDto { locale:"zh-CN"|"en-US", theme:"light"|"dark"|"system", defaultWorkspaceId? }`
- `MeGeneralSettingsUpdateRequest { locale?, theme?, defaultWorkspaceId? }`
- `MePublishChannelDto { id, name, type:"wechat-personal"|"feishu-personal"|"custom", bound }`
- `MeDataSourceDto { id, name, type:"qdrant"|"minio"|"obs"|"rdbms", bound }`

### 安全与权限

- 全部接口要求 `Authorize`，最低权限 `Permission:ai-workspace:view`。
- `MeSettings*` 隐式作用于 JWT 解出的当前用户，请求体不接受 `userId`，防止越权。
- `Home*` 路径携带 `workspaceId`，Service 内按 `tenantId + workspaceId` 隔离数据。

### 替代旧 mock 的对照表

| 后台 Controller | 替代的前端 mock 文件 |
|---|---|
| `HomeContentController` | `services/mock/api-home-content.mock.ts` |
| `CommunityController` | `services/mock/api-community.mock.ts` |
| `PlatformGeneralController` | `services/mock/api-platform-general.mock.ts` 中 `listPlatformNotices` / `getPlatformBranding` |
| `MarketSummaryController` | `services/mock/api-templates-market.mock.ts` |
| `MeSettingsController` | `services/mock/api-me-settings.mock.ts`（除 `deleteMeAccount` 外）|
| `WorkspaceFoldersController` | `services/mock/api-folders.mock.ts` |
| `WorkspacePublishChannelsController` | `services/mock/api-publish-channels.mock.ts` |

## Coze 平台第三阶段 API（M2：工作空间维度持久化对象）

> 这一批由 SqlSugar 持久化，新表 `WorkspaceFolder` / `WorkspacePublishChannel`
> 已加入 `AtlasOrmSchemaCatalog.RuntimeEntities`，启动时自动 `CodeFirst.InitTables`。
> Service 实现在 `Atlas.Infrastructure/Services/Coze/WorkspaceFolderService.cs`。

### 工作空间项目开发-文件夹（PRD 03-5.4）

- `GET /api/v1/workspaces/{workspaceId}/folders?keyword&pageIndex&pageSize` → `ApiResponse<PagedResult<WorkspaceFolderListItem>>`
- `POST /api/v1/workspaces/{workspaceId}/folders` body `WorkspaceFolderCreateRequest` → `ApiResponse<{ id, folderId }>`
- `PATCH /api/v1/workspaces/{workspaceId}/folders/{folderId}` body `WorkspaceFolderUpdateRequest` → `ApiResponse<{ success }>`
- `DELETE /api/v1/workspaces/{workspaceId}/folders/{folderId}` → `ApiResponse<{ success }>`
- `POST /api/v1/workspaces/{workspaceId}/folders/{folderId}/items` body `{ itemType, itemId }` → `ApiResponse<{ success }>`

DTO：

- `WorkspaceFolderListItem { id, workspaceId, name, description?, itemCount, createdByDisplayName, createdAt, updatedAt }`
- `WorkspaceFolderCreateRequest { name (1..40), description? (max 800) }`
- `WorkspaceFolderUpdateRequest { name?, description? }`
- `WorkspaceFolderItemMoveRequest { itemType:"agent"|"app"|"project", itemId }`

校验：`name` 必填 1~40 字符；`description` 最多 800 字符。第一阶段不做 folder-item 关联表，
仅维护 `itemCount` 计数；后续接入对象绑定时再补 `WorkspaceFolderItem` 表。

### 工作空间发布渠道（PRD 04-4.6）

- `GET /api/v1/workspaces/{workspaceId}/publish-channels?keyword&pageIndex&pageSize` → `ApiResponse<PagedResult<WorkspacePublishChannelDto>>`
- `POST /api/v1/workspaces/{workspaceId}/publish-channels` body `WorkspacePublishChannelCreateRequest` → `ApiResponse<{ id, channelId }>`
- `PATCH /api/v1/workspaces/{workspaceId}/publish-channels/{channelId}` body `WorkspacePublishChannelUpdateRequest` → `ApiResponse<{ success }>`
- `POST /api/v1/workspaces/{workspaceId}/publish-channels/{channelId}/reauth` → `ApiResponse<{ success }>`（标记授权成功并刷新 lastSyncAt）
- `DELETE /api/v1/workspaces/{workspaceId}/publish-channels/{channelId}` → `ApiResponse<{ success }>`

DTO：

- `WorkspacePublishChannelDto { id, workspaceId, name, type, status, authStatus, description?, supportedTargets[], lastSyncAt?, createdAt }`
- `WorkspacePublishChannelCreateRequest { name (1..64), type, description? (max 512), supportedTargets[]? }`
- `WorkspacePublishChannelUpdateRequest { name?, description?, status?, supportedTargets[]? }`

枚举校验（Service 内强约束）：

- `type` ∈ `web-sdk | open-api | wechat | feishu | lark | custom`
- `status` ∈ `active | inactive | pending`
- `supportedTargets` ⊂ `agent | app | workflow`（其它值会被静默剔除）

### 安全与权限（M2）

- 视图操作 `Permission:ai-workspace:view`，写入操作 `Permission:ai-workspace:update`。
- 所有数据按 `tenantId + workspaceId` 双键隔离，路径中 `workspaceId` 不可省略。
- `supportedTargets` 序列化为 JSON 数组存表（`SupportedTargetsJson` 列），白名单校验后写入。

### 替代旧 mock 的对照表（M2 增补）

| 后台 Controller | 替代的前端 mock 文件 |
|---|---|
| `WorkspaceFoldersController` | `services/mock/api-folders.mock.ts` |
| `WorkspacePublishChannelsController` | `services/mock/api-publish-channels.mock.ts` |

## Coze 平台第三阶段 API（M3：任务中心 / 评测 / 测试集）

> 本批以 `Singleton` in-memory Service 落地，让前端任务中心 / 效果评测 /
> 测试集抽屉端到端跑通。第二阶段对接现有 `BatchProcess`、`EvaluationDataset/Task/Result`
> 等持久化模型，本表协议保持不变。

### 任务中心（PRD 02-7.4）

- `GET /api/v1/workspaces/{workspaceId}/tasks?status=pending|running|succeeded|failed&type&keyword&pageIndex&pageSize` → `ApiResponse<PagedResult<WorkspaceTaskItemDto>>`
- `GET /api/v1/workspaces/{workspaceId}/tasks/{taskId}` → `ApiResponse<WorkspaceTaskDetailDto>`（不存在时 404）

DTO：

- `WorkspaceTaskItemDto { id, name, type:"workflow"|"batch"|"evaluation"|"publish", status:int, startedAt, durationMs, ownerDisplayName }`
- `WorkspaceTaskDetailDto`：在 list item 字段基础上增加 `inputJson?`、`outputJson?`、`errorMessage?`、`logs[]`
- `WorkspaceTaskLogEntryDto { timestamp, level:"info"|"warn"|"error", message }`

`status` 在 JSON 中序列化为整数（0/1/2/3）。前端 mock 已做 `number → string` 兼容映射，
后续切换为持久化模型时若改为字符串枚举，前端无需改动。

### 效果评测（PRD 02-7.5）

- `GET /api/v1/workspaces/{workspaceId}/evaluations?keyword&pageIndex&pageSize` → `ApiResponse<PagedResult<EvaluationItemDto>>`
- `GET /api/v1/workspaces/{workspaceId}/evaluations/{evaluationId}` → `ApiResponse<EvaluationDetailDto>`（不存在时 404）

DTO：

- `EvaluationItemDto { id, name, targetType:"workflow"|"agent", targetId, testsetId, status:int, metricSummary, startedAt }`
- `EvaluationDetailDto`：在 list 字段基础上增加 `totalCount, passCount, failCount, reportJson`

### 测试集（PRD 05-4.8）

- `GET /api/v1/workspaces/{workspaceId}/testsets?keyword&pageIndex&pageSize` → `ApiResponse<PagedResult<TestsetItemDto>>`
- `POST /api/v1/workspaces/{workspaceId}/testsets` body `TestsetCreateRequest` → `ApiResponse<{ id, testsetId }>`

DTO：

- `TestsetItemDto { id, name, description?, workflowId?, rowCount, createdAt, updatedAt }`
- `TestsetCreateRequest { name (1..50), description? (max 200), workflowId?, rows[]?: object[] }`

测试集行（`rows`）按"开始节点输入变量"动态生成，第一阶段不做 schema 校验；
第二阶段对接 `EvaluationDataset/EvaluationCase` 后将按工作流节点声明强校验。

### 安全与权限（M3）

- 视图 `Permission:ai-workspace:view`，写入 `Permission:ai-workspace:update`。
- 所有路由路径都强制带 `workspaceId`，Service 内按 `tenantId + workspaceId` 双键隔离。

### 替代旧 mock 的对照表（M3 增补）

| 后台 Controller | 替代的前端 mock 文件 |
|---|---|
| `WorkspaceTasksController` | `services/mock/api-tasks.mock.ts` |
| `WorkspaceEvaluationsController`（含 testsets 子路径） | `services/mock/api-evaluations.mock.ts` |

## Coze 平台第三阶段 API（M4：能力闭环 + 持久化升级）

> 本批将 M1 / M3 的 in-memory 实现升级为持久化版本，并新增 OpenApiKeys 端点。
> 共涉及 1 个新表 `WorkspaceFolderItem`、1 个新表 `PlatformContent`，
> 复用 `EvaluationDataset / EvaluationCase / EvaluationTask`、`PersonalAccessToken` 现有表。
> 全部通过 `AtlasOrmSchemaCatalog.RuntimeEntities` 自动 InitTables。

### M4.1 OpenAPI 密钥（PRD 02-7.10）

- `GET /api/v1/open/api-keys?keyword&pageIndex&pageSize` → `ApiResponse<OpenApiKeyDto[]>`
- `POST /api/v1/open/api-keys` body `OpenApiKeyCreateRequest` → `ApiResponse<OpenApiKeyCreateResponse>`
- `DELETE /api/v1/open/api-keys/{keyId}` → `ApiResponse<{ success }>`

DTO：

- `OpenApiKeyDto { id, alias, prefix, scopes[], createdAt, lastUsedAt?, expiresAt? }`
- `OpenApiKeyCreateRequest { alias (1..64), scopes[]?, expiresAt? }`
- `OpenApiKeyCreateResponse { key, item: OpenApiKeyDto }`（`key` 是 PAT 明文，仅在创建响应中返回一次）

权限：`Permission:pat:view` / `Permission:pat:create` / `Permission:pat:delete`。
后端复用 `IPersonalAccessTokenService`（`PersonalAccessTokenRepository` 持久化），
当前用户隔离：所有列表/创建/删除都按 `tenantId + createdByUserId` 双键过滤，防止越权。

### M4.2 文件夹 ↔ 对象关联表（PRD 03-5.4 增强）

- `MoveItem` 行为升级：写入新表 `WorkspaceFolderItem`，并保证一个 (workspaceId, itemType, itemId) 只属于一个文件夹（先查后写，已存在则迁移）。
- `List` 接口的 `itemCount` 由 `WorkspaceFolderItem` 表 `GROUP BY folderId COUNT(*)` 计算（**一次性批量查询，禁止循环内查库**）。
- `Delete` 文件夹时先清理关联，再删 folder 自身，避免孤儿。
- 表字段：`Id`, `TenantId`, `WorkspaceId`, `FolderId`, `ItemType`, `ItemId`, `AddedAt`。
- `ItemType` 校验：`agent | app | project`，其它值返回 `VALIDATION_ERROR`。

### M4.3 测试集持久化（PRD 05-4.8 持久化版）

- 服务实现切换：`InMemoryWorkspaceTestsetService` → `WorkspaceTestsetService`，底层复用：
  - `EvaluationDataset` 存测试集元数据（`Scene` 字段编码 `coze-testset:{workspaceId}|{workflowId}`）
  - `EvaluationCase` 存测试集每行（`Input` 字段为整行 JSON 序列化）
- 接口契约不变：`GET/POST /api/v1/workspaces/{wsId}/testsets`。
- 列表：`EvaluationDatasetRepository.GetPagedByScenePrefixAsync(scenePrefix="coze-testset:{wsId}|")` + `EvaluationCaseRepository.CountByDatasetIdsAsync` 一次性聚合。
- 创建：写入 1 条 `EvaluationDataset` + N 条 `EvaluationCase`。

### M4.4 任务中心持久化（PRD 02-7.4 持久化版）

- 服务实现切换：`InMemoryWorkspaceTaskService` → `WorkspaceTaskService`，底层从 `EvaluationTask` 读取。
- `WorkspaceTaskItemDto.type` 当前固定为 `"evaluation"`。
- `EvaluationTaskStatus` → `WorkspaceTaskStatus` 映射：
  - `Pending → Pending`、`Running → Running`、`Completed → Succeeded`、`Failed → Failed`
- `DurationMs` = `CompletedAt - StartedAt` 毫秒数（未启动时为 0）。
- 当前限制：`EvaluationTask` 模型无 `WorkspaceId` 字段，**租户内任务对所有工作空间可见**；
  下一轮 schema 演进为 `EvaluationTask` 增加 `WorkspaceId` 列后即可严格按工作空间过滤。
  接 `BatchJobExecution` 与 Hangfire 时同样需要先扩展 schema。

### M4.5 首页内容持久化（PRD 01 持久化版）

- 服务实现切换：`InMemoryHomeContentService` → `PlatformHomeContentService`，底层用新表 `PlatformContent`。
- 表字段：`Id`, `TenantId`, `Slot`, `ContentKey`, `ContentJson`, `Tag`, `OrderIndex`, `IsActive`, `PublishedAt`, `CreatedAt`, `UpdatedAt`。
- `Slot` 取值：`banner | tutorial | announcement | recommended`，`ContentJson` 是对应 DTO（`HomeBannerDto / HomeTutorialCardDto / HomeAnnouncementItemDto / HomeRecommendedAgentDto`）的 JSON 序列化。
- 行为：每个 Slot 优先读 `IsActive=true` 的记录（按 `OrderIndex ASC` + `PublishedAt DESC` 排序）；**当对应 Slot 无激活记录时 fallback 到内置默认数据**，保证空数据库场景仍可用。
- `recent-activities` 仍返回空数组，等 `WorkspaceIdeService.RecordActivity` 接入后再补。
- 运营 CRUD UI 暂未接入（直接 SQL `INSERT INTO PlatformContent` 即可上线运营内容）；下一迭代补 `PlatformContentsController`。

### 替代/升级对照表（M4 总览）

| 类别 | 之前实现 | M4 实现 | 涉及表 |
|---|---|---|---|
| OpenAPI 密钥 | 前端 in-memory mock | `OpenApiKeysController` + `IPersonalAccessTokenService` | `PersonalAccessToken` |
| 文件夹 itemCount | `WorkspaceFolder.ItemCount` 字段累加 | `WorkspaceFolderItem` 关联表 + `GROUP BY` 聚合 | 新增 `WorkspaceFolderItem` |
| 测试集 | `InMemoryWorkspaceTestsetService` | `WorkspaceTestsetService`（持久化） | `EvaluationDataset` + `EvaluationCase` |
| 任务中心 | `InMemoryWorkspaceTaskService`（空集合） | `WorkspaceTaskService`（读 EvaluationTask） | `EvaluationTask` |
| 首页内容 | `InMemoryHomeContentService`（写死常量） | `PlatformHomeContentService`（DB + fallback） | 新增 `PlatformContent` |

### 安全与权限（M4）

- OpenAPI 密钥：`Permission:pat:*` 系列。
- 文件夹 / 测试集 / 任务中心 / 首页：沿用 `Permission:ai-workspace:view|update`。
- 全部接口仍按 `tenantId` 隔离；`PersonalAccessToken` 额外按 `createdByUserId` 隔离防越权。
- 持久化层强约束：所有列表查询走 `RefAsync<int>` 一次分页，禁止循环内查库；
  关联统计走 `GROUP BY` 一次性聚合（`CountByFolderIdsAsync` / `CountByDatasetIdsAsync`）。

## Coze 平台第三阶段 API（M5：运营 CRUD + 统一内容表 + 最近使用对接 + 评测按工作空间过滤）

> 本批进一步把剩余 in-memory 内容服务全部接到 PlatformContent 统一表；
> 给 EvaluationTask 补 WorkspaceId 列，让评测与任务列表按工作空间严格过滤；
> 把首页"最近使用"对接到 WorkspaceIdeService 现有活动数据。
> 不新增表，主要是行为升级与数据源替换。

### M5.3 平台运营内容 CRUD（新 Controller）

- `GET /api/v1/platform/contents?slot=&onlyActive=` → `ApiResponse<PlatformContentItemDto[]>`（视图权限）
- `POST /api/v1/platform/contents` body `PlatformContentCreateRequest` → `ApiResponse<{ id, contentId }>`（SystemAdmin）
- `PATCH /api/v1/platform/contents/{id}` body `PlatformContentUpdateRequest` → `ApiResponse<{ success }>`（SystemAdmin）
- `DELETE /api/v1/platform/contents/{id}` → `ApiResponse<{ success }>`（SystemAdmin）

DTO：

- `PlatformContentItemDto { id, slot, contentKey, contentJson, tag?, orderIndex, isActive, publishedAt, createdAt, updatedAt? }`
- `PlatformContentCreateRequest { slot (1..32), contentKey (1..64), contentJson, tag?, orderIndex, isActive?, publishedAt? }`
- `PlatformContentUpdateRequest { contentJson, tag?, orderIndex, isActive, publishedAt? }`

`Slot` 白名单枚举（`Atlas.Application.Coze.Models.PlatformContentSlots`）：

- `banner | tutorial | announcement | recommended`（首页）
- `community-work`（社区）
- `platform-notice`（通用管理）
- `market-template-summary | market-plugin-summary`（模板/插件摘要）

Service 约束：`ContentJson` 长度 ≤ 32 KB；不合法 Slot 返回 `VALIDATION_ERROR`。

### M5.5 统一内容表（三个 Service 接入 PlatformContent）

以下三个 Service 行为升级（类名保留 `InMemory*` 前缀，内部已改为"PlatformContent 持久化 + 空表 fallback 到默认常量"）：

- `InMemoryCommunityService` → 读 Slot=`community-work`，fallback 到 2 条默认 works
- `InMemoryPlatformGeneralService` → 读 Slot=`platform-notice`（notices），branding 保留常量（单条信息，未来可加 Slot=`branding`）
- `InMemoryMarketSummaryService` → 读 Slot=`market-template-summary` / `market-plugin-summary`，fallback 到默认分类

注意：三个 Service 的 DI 注册由 Singleton 改为 Scoped（依赖 Scoped 的 `PlatformContentRepository`）。API 协议未变。

### M5.4 最近使用对接 WorkspaceIde

- `PlatformHomeContentService.GetRecentActivitiesAsync` 投影 `IWorkspaceIdeService.GetResourcesAsync` 的前 10 条：
  - 过滤 resourceType ∈ `agent | app | workflow | chatflow`；`chatflow` 归并为 `workflow`（前端用 type 做 UI 区分）
  - `updatedAt` = `LastEditedAt ?? LastOpenedAt ?? UpdatedAt`
- 首页 `/home/recent-activities` 现在返回当前用户真实的最近访问/编辑资源列表。

### M5.1 EvaluationTask 新增 WorkspaceId 列

- Domain：`EvaluationTask.WorkspaceId` nullable `string?`，仅新加 `AttachWorkspace(workspaceId)` 行为方法。
- Schema 迁移：`DatabaseInitializerHostedService.EnsureWorkspacePortalSchemaAsync` 增加
  `AddColumnIfMissingAsync("EvaluationTask", "WorkspaceId", "TEXT NULL")`，SQLite 无痛迁移。
- 历史兼容：旧 EvaluationService 创建的 agent 评测任务 WorkspaceId=NULL，不会进入任何 Coze 工作空间视图；原有评测跑批流程不变。

### M5.1 评测列表持久化 `WorkspaceEvaluationService`

- `GET /api/v1/workspaces/{workspaceId}/evaluations` 改为读 `EvaluationTask` WHERE `WorkspaceId = {workspaceId}`
- `GET /api/v1/workspaces/{workspaceId}/evaluations/{evaluationId}` 严格按 workspace 校验；不匹配时返回 404
- `EvaluationDetailDto.PassCount / FailCount` 从 `EvaluationResult` 按 `TaskId` 一次性聚合（`GetByTaskAsync`），单次 SQL 查询

### M5.2 任务中心按工作空间过滤

- `WorkspaceTaskService.ListAsync` 从 `GetPagedAsync(tenantId)` → `GetPagedByWorkspaceAsync(tenantId, workspaceId, keyword)`
- `WorkspaceTaskService.GetAsync` 严格校验 `entity.WorkspaceId == workspaceId`
- `keyword` 下推到 SQL 层（`Name.Contains`），不再内存过滤
- 多源聚合（`BatchJobExecution` + `PersistedExecutionPointer` + Hangfire）延至 M6，需要先给相关 Entity 扩 WorkspaceId

### 替代/升级对照表（M5 总览）

| 类别 | M4 实现 | M5 实现 |
|---|---|---|
| 首页 `RecentActivities` | 空数组 | 对接 `WorkspaceIdeService.GetResourcesAsync`（最近访问/编辑） |
| 社区 / 平台公告 / 模板插件摘要 | in-memory 常量 | PlatformContent 统一表（+ 默认常量 fallback） |
| PlatformContent 维护 | 仅能 SQL 手工 `INSERT` | `PlatformContentsController` CRUD（SystemAdmin） |
| 评测列表 | in-memory 空集合 | `WorkspaceEvaluationService` 读 EvaluationTask（按 WorkspaceId 过滤）+ 聚合 Pass/Fail |
| 任务列表 | 按 tenantId 过滤（跨工作空间混杂） | 按 `(tenantId, workspaceId)` 严格过滤；keyword 下推 SQL |

### 安全与权限（M5）

- `PlatformContentsController`：读沿用 `Permission:ai-workspace:view`，写（POST/PATCH/DELETE）需 `Permission:system:admin`
- Evaluation / Tasks 等接口继续使用 `Permission:ai-workspace:view|update`
- 所有查询遵守"循环内零查库"原则：`GROUP BY` / `GetByTaskAsync` / `GetPagedByWorkspaceAsync` 都走单次批量查询

## 12. 系统初始化与迁移控制台（Setup Console）

> 升级原 `/app-setup` 单步原子向导为常驻 `/setup-console`：可重入、可断点续跑、ORM 优先跨库迁移；
> 永久免登录，由"恢复密钥 + BootstrapAdmin 凭证"双因子保护，所有写操作单独审计。
>
> 本章为 M1 草案 = 后端最终契约（M5 落地）；前端 mock 实现见 `src/frontend/apps/app-web/src/services/mock/api-setup-console.mock.ts` 等 4 个文件。
> 状态机定义见 `src/frontend/apps/app-web/src/app/setup-console-state-machine.ts`，后端镜像 `src/backend/Atlas.Domain/Setup/SetupConsoleStateMachine.cs`（M5）。

### 12.1 总体设计

- 路由前缀：`/api/v1/setup-console/*`
- 入口：`/setup-console`（前端永久免登录路由，`SetupModeMiddleware` M5 放行 `auth/recover` + 带 `X-Setup-Console-Token` 请求头的全部端点）
- 二次认证：恢复密钥（首装时一次性下发的 24 字符 base32 字符串）或 BootstrapAdmin 凭证
- 控制台 token：30 分钟过期，每次写操作 + 周期性自动 `refreshAuth`
- 写操作审计：每次状态机转换、迁移命令均通过新的 `SetupConsoleAuditWriter` 写入 `AuditRecord`
- 真理来源：后端 `AtlasOrmSchemaCatalog.AllRuntimeEntityTypes`（当前 211 个实体）→ 控制台聚合为 6 大类展示

### 12.2 状态机

系统级状态（`SystemSetupState`，14 个）：

- `not_started` / `precheck_passed` / `schema_initializing` / `schema_initialized` /
  `seed_initializing` / `seed_initialized` / `migration_pending` / `migration_running` /
  `migration_partially_completed` / `migration_completed` / `validation_running` /
  `completed` / `failed` / `dismissed`

工作空间级状态（`WorkspaceSetupState`，4 个）：

- `workspace_init_pending` / `workspace_init_running` / `workspace_init_completed` / `workspace_init_failed`

数据迁移状态（`DataMigrationState`，9 个，与现有 `AppMigrationTaskStatuses` 对齐，仅命名风格转 kebab-case）：

- `pending` / `prechecking` / `ready` / `running` / `validating` /
  `cutover-ready` / `cutover-completed` / `failed` / `rolled-back`

合法转移矩阵的权威实现见前端 `setup-console-state-machine.ts` 与对应 Vitest `setup-console-state-machine.spec.ts`。

### 12.3 控制台总览 + 二次认证

- `GET /api/v1/setup-console/overview` → `ApiResponse<SetupConsoleOverviewDto>`
- `POST /api/v1/setup-console/auth/recover` body `ConsoleAuthChallengeRequest` → `ApiResponse<ConsoleAuthTokenDto>`
- `POST /api/v1/setup-console/auth/refresh` body `{ consoleToken }` → `ApiResponse<ConsoleAuthTokenDto>`
- `GET /api/v1/setup-console/system/state` → `ApiResponse<SystemSetupStateDto>`
- `GET /api/v1/setup-console/catalog/entities?category=` → `ApiResponse<SetupConsoleCatalogSummaryDto>`

DTO 参考前端 [`api-setup-console.ts`](../src/frontend/apps/app-web/src/services/api-setup-console.ts)：

- `SetupConsoleOverviewDto { system, workspaces[], activeMigration | null, catalogSummary }`
- `SystemSetupStateDto { state, version, lastUpdatedAt, failureMessage, recoveryKeyConfigured, steps[] }`
- `SetupStepRecordDto { step, state, startedAt, endedAt, attemptCount, errorMessage }`（`step ∈ precheck|schema|seed|bootstrap-user|default-workspace|complete`）
- `WorkspaceSetupStateDto { workspaceId, workspaceName, state, seedBundleVersion, lastUpdatedAt }`
- `SetupConsoleCatalogSummaryDto { totalEntities, totalCategories, missingCriticalTables[], categories[] }`
- `ConsoleAuthChallengeRequest { recoveryKey?, bootstrapAdminUsername?, bootstrapAdminPassword? }`
- `ConsoleAuthTokenDto { consoleToken, issuedAt, expiresAt, permissions[] }`

错误码扩充：`RECOVERY_KEY_INVALID`、`CONSOLE_TOKEN_EXPIRED`、`SETUP_VERSION_LOCKED`、`MIGRATION_FINGERPRINT_DUPLICATED`、`SETUP_INVALID_TRANSITION`。

### 12.4 系统级初始化（6 步）

每个端点返回 `ApiResponse<SetupStepResultDto>`，`bootstrapUser` 额外返回一次性 `recoveryKey`：

- `POST /api/v1/setup-console/system/precheck` body `SystemPrecheckRequest` → 校验数据库连通性、磁盘空间、必备扩展
- `POST /api/v1/setup-console/system/schema` body `SystemSchemaRequest { dryRun? }` → 调用 `AtlasOrmSchemaCatalog.EnsureRuntimeSchema`
- `POST /api/v1/setup-console/system/seed` body `SystemSeedRequest { bundleVersion?, forceReapply? }` → 角色 / 权限 / 菜单 / 字典 / 模型占位 / 审批模板等
- `POST /api/v1/setup-console/system/bootstrap-user` body `SystemBootstrapUserRequest` → 默认管理员，`generateRecoveryKey=true` 时一次性返回 `recoveryKey`
- `POST /api/v1/setup-console/system/default-workspace` body `SystemDefaultWorkspaceRequest` → 默认工作空间 + Owner / Editor / Viewer 角色 + 默认渠道
- `POST /api/v1/setup-console/system/complete` → 终态切换，关闭后续 `SetupModeMiddleware` 拦截
- `POST /api/v1/setup-console/system/retry/{step}` → 显式将该步从 `failed`/`succeeded` 重置为 `running`
- `POST /api/v1/setup-console/system/reopen` → 把控制台从 `dismissed` 拉回 `not_started`，便于重做

幂等约束（M5 强制）：

- 每步开始前先 upsert `SetupStepRecord(StepState=Running)`，结束后 upsert 为 `Succeeded` / `Failed`
- 已 `Succeeded` 的步骤再次调用不会重复执行写库操作（仅刷新 `attemptCount`）
- 已 `completed` 的系统状态再调任意 `system/*` 接口直接返回 `succeeded`，不会回退状态机
- `version` 字段用于种子包升级（`v1`/`v2`）；新版本可在已 completed 后通过 `system/seed` + `forceReapply=true` 增量补种

### 12.5 工作空间级初始化

- `GET /api/v1/setup-console/workspaces` → `ApiResponse<WorkspaceSetupStateDto[]>`
- `POST /api/v1/setup-console/workspaces/{wsId}/init` body `WorkspaceInitRequest { workspaceName, seedBundleVersion, applyDefaultRoles, applyDefaultPublishChannels }` → `ApiResponse<WorkspaceSetupStateDto>`
- `POST /api/v1/setup-console/workspaces/{wsId}/seed-bundle` body `WorkspaceSeedBundleRequest { bundleVersion, forceReapply? }` → 升级或补种空间级种子
- `POST /api/v1/setup-console/workspaces/{wsId}/complete` → 把空间状态置 `workspace_init_completed`

幂等约束：相同 `seedBundleVersion` 不重复执行（除非 `forceReapply=true`）；已 `completed` 的空间再次 `init` 直接返回当前状态。

### 12.6 数据迁移（ORM 优先跨库）

- `POST /api/v1/setup-console/migration/test-connection` body `MigrationTestConnectionRequest` → `ApiResponse<MigrationTestConnectionResponse>`
- `POST /api/v1/setup-console/migration/jobs` body `DataMigrationJobCreateRequest` → `ApiResponse<DataMigrationJobDto>`
- `POST /api/v1/setup-console/migration/jobs/{id}/precheck` → 推进到 `prechecking` → `ready`
- `POST /api/v1/setup-console/migration/jobs/{id}/start` → 推进到 `running`
- `GET /api/v1/setup-console/migration/jobs/{id}/progress` → 实时轮询，返回当前实体 / 批次 / 复制行数
- `POST /api/v1/setup-console/migration/jobs/{id}/validate` → 行数 + 抽样字段 + 外键校验，生成 `DataMigrationReportDto`
- `POST /api/v1/setup-console/migration/jobs/{id}/cutover` body `DataMigrationCutoverRequest { keepSourceReadonlyForDays }` → 写 `appsettings.runtime.json` 切主
- `POST /api/v1/setup-console/migration/jobs/{id}/rollback` → 回滚（仅未 cutover 时可用）
- `POST /api/v1/setup-console/migration/jobs/{id}/retry` → 失败 / 已回滚任务从最后 checkpoint 续跑
- `GET /api/v1/setup-console/migration/jobs/{id}/report` → 校验报告
- `GET /api/v1/setup-console/migration/jobs/{id}/logs?level=&pageIndex&pageSize` → 分页日志

DTO 关键字段：

- `DbConnectionConfig { driverCode, dbType: SQLite|MySql|PostgreSQL|SqlServer, mode: raw|visual, connectionString?, visualConfig? }`
- `DataMigrationJobCreateRequest { source, target, mode, moduleScope, allowReExecute }`
- `DataMigrationModuleScope { categories: ["all" | category...], entityNames? }`
- `DataMigrationJobDto { id, state, mode, source, target, sourceFingerprint, targetFingerprint, moduleScope, totalEntities, completedEntities, failedEntities, totalRows, copiedRows, progressPercent, currentEntityName?, currentBatchNo?, startedAt?, finishedAt?, errorSummary?, createdAt, updatedAt }`
- `DataMigrationReportDto { jobId, totalEntities, passedEntities, failedEntities, rowDiff[], samplingDiff[], overallPassed, generatedAt }`

防重复机制：

- `sourceFingerprint = hash(driverCode + dbType + connectionString-canonical + max(id) per critical table)`
- `targetFingerprint` 同理
- 同 `(sourceFingerprint, targetFingerprint)` 已存在 `cutover-completed` 任务时，必须 `allowReExecute=true` 才能创建新任务
- 单个 `JobId` 内部用 `DataMigrationCheckpoint(JobId, EntityName)` 唯一约束，重启时从最后 batch + 1 续跑

ORM 优先实现要点（M6）：

- 源库 / 目标库各开一个 `SqlSugarScope`，用同一份 `EntityType[]` 双向 IO；类型差异由 SqlSugar 自动适配（`DateTimeOffset → DATETIME / TIMESTAMPTZ` 等）
- 拓扑排序：基于 `[Navigate]` / 命名约定 `XxxId` 推断，`Tenant → UserAccount → Role → UserRole → Workspace → Agent → ...`
- 大表分批：`Skip(page * batch).Take(batch).ToList()` + `Insertable(rows).ExecuteCommand()`，默认 batch=500
- 校验：行数差 + 5% 抽样字段哈希 + 外键存在性

### 12.7 新增元数据表（8 张）

归属 `Atlas.Domain.Setup`，M5 通过 `AtlasOrmSchemaCatalog.AllRuntimeEntityTypes` 统一建表：

- `SystemSetupState`：单例（每租户 1 行），`State / Version / RecoveryKeyHash / LastUpdatedAt / FailureMessage`
- `WorkspaceSetupState`：每工作空间 1 行，`WorkspaceId / State / Version / SeedBundleVersion`
- `SetupStepRecord`：明细，`Tenant / Step / State / StartedAt / EndedAt / AttemptCount / ErrorMessage / PayloadJson`
- `DataMigrationJob`：跨库任务，`State / Mode / SourceConnectionString(加密) / SourceDbType / TargetConnectionString(加密) / TargetDbType / SourceFingerprint / TargetFingerprint / ModuleScopeJson / Counters`
- `DataMigrationBatch`：每实体每批次记录
- `DataMigrationCheckpoint`：唯一索引 `(JobId, EntityName)`，断点续跑核心
- `DataMigrationLog`：明细审计日志
- `DataMigrationReport`：校验汇总报告

### 12.8 安全与权限（控制台）

- `/api/v1/setup-console/auth/recover` 永远 `[AllowAnonymous]`，但每次失败计入审计并按 IP 限流
- 其余端点必须带 `X-Setup-Console-Token`，由 M5 新增的 `SetupConsoleAuthMiddleware` 校验
- 控制台 token 与 JWT 完全独立，互不影响
- 任何写操作（schema/seed/bootstrap-user/default-workspace/complete/retry/reopen/migration/*）由 `SetupConsoleAuditWriter` 写 `AuditRecord`，`Action` 为 `setup-console.{step}`
- 数据迁移连接串持久化时使用 `IDataProtector` 加密（M6）
- 恢复密钥仅在 `bootstrap-user` 阶段一次性下发，密文用 PBKDF2 存 `SystemSetupState.RecoveryKeyHash`

### 12.9 与现有资产的关系

- `/api/v1/setup/*`（旧 `SetupController`）M5 全部加 `[Obsolete]` + 响应头 `Deprecation: true`，6 个月窗口期保兼容
- `DatabaseInitializerHostedService.RunInitializationAsync` 不删，M5 拆出 6 个 public 幂等方法供 `SetupConsoleService` 调度
- `AppMigrationService`（应用维度迁移）保持原职责，新 `OrmDataMigrationService`（控制台跨库迁移）共享 `AppMigrationTaskStatuses` 命名约定，但用独立的 `DataMigrationJob` 表
- `BootstrapAdminOptions` / `appsettings.runtime.json` 持久化机制 100% 复用

## 低代码应用 UI Builder（M01-M20）

> 本章节为 docx 「Coze 低代码全量复刻」实施计划的契约总目录。各子契约规格分散在 `docs/lowcode-*-spec.md` 系列文档中，并由 PLAN.md 的 20 里程碑逐步落地。
>
> 设计期与运行时的端点严格分两套前缀，**禁止混用**：
>
> | 前缀 | Host | 端口 | 用途 |
> | --- | --- | --- | --- |
> | `/api/v1/lowcode/*` | PlatformHost | 5001 | 设计态写操作（schema / pages / variables / versions / publish / templates / faq / prompt-templates / plugins / overrides 等） |
> | `/api/runtime/*` | AppHost | 5002 | 运行时只读 / 状态变更（schema / events:dispatch / workflows:invoke / chatflows:invoke / files / triggers + webhooks/triggers/{id} + triggers/events/{name}:raise + triggers/{id}:rotate-webhook-secret / sessions / webview-domains / message-log / traces / publish artifacts / versions:archive\|rollback / plugins:invoke 等） |
>
> SignalR Hub 路径：
> - `/hubs/lowcode-debug`（M13，AppHost）
> - `/hubs/lowcode-collab`（M16，AppHost，SignalR + 自定义 Yjs provider）
> - `/hubs/lowcode-preview`（M08，**PlatformHost + AppHost** 同时挂载，HMR 推送由设计态写入服务直接调用 `ILowCodePreviewSignal` 触发）

### 契约文档索引

- 总览：`docs/contracts.md` 本章节 + 后续各小节
- 协议层：`docs/lowcode-runtime-spec.md`（M01-M03 + M08-M14 接口与协议）
- 模式 A/B 黄金样本：`docs/lowcode-binding-matrix.md`（M09）
- 组件 6 维矩阵：`docs/lowcode-component-spec.md`（M06）
- 发布契约：`docs/lowcode-publish-spec.md`（M17）
- 协同契约：`docs/lowcode-collab-spec.md`（M16）
- 智能体复刻契约：`docs/lowcode-assistant-spec.md`（M18）
- 快捷键清单：`docs/lowcode-shortcut-spec.md`（M04 / M07）
- 消息日志统一视图契约：`docs/lowcode-message-log-spec.md`（M11 / M13 / M18）
- 弹性策略契约：`docs/lowcode-resilience-spec.md`（M09 / M11 / M19）
- 编排哲学与有状态运行：`docs/lowcode-orchestration-spec.md`（M19 / M20）
- 内容参数 6 类独立机制：`docs/lowcode-content-params-spec.md`（M05 / M06）
- 插件全域：`docs/lowcode-plugin-spec.md`（M18）

### 强约束

- **标准化协议唯一桥梁**：UI 禁止直调 workflow / chatflow / files / triggers / sessions / plugins 零散 API；所有运行时事件必须经 `POST /api/runtime/events/dispatch`（M13 落地）。前端 CI 静态扫描守门，仅允许 `lowcode-action-runtime` 与 `lowcode-debug-client` 内部出现 dispatch 调用入口。
- **作用域变量隔离**：page / app / system 三作用域禁止跨作用域 setVariable；component / event / workflow.outputs / chatflow.outputs 四作用域禁止写入。M02 表达式引擎 + M03 action-runtime 双层校验。
- **元数据驱动禁硬编码**：组件实现禁止 fetch / import workflow client / 硬写业务逻辑；ComponentMeta 注册期校验（M06）。
- **写接口安全基线**：沿用现行无 `Idempotency-Key` / `X-CSRF-TOKEN` 机制；所有写接口必须经 `IAuditWriter` 审计。
