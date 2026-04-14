# Atlas Security Platform Contracts

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
- 返回项映射为：
  - `provider`
  - `providerType`
  - `model`
  - `label`
  - `systemPrompt`
  - `temperature`
  - `maxTokens`
  - `enableStreaming`

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

### 端点连线约束（workflow-editor-react）

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

### Workflow 依赖查询（PlatformHost）

- `GET /api/v2/workflows/{id}/dependencies`
- 返回：`ApiResponse<WorkflowV2DependencyDto>`
- `WorkflowV2DependencyDto`
  - `workflowId`
  - `subWorkflows[]`
  - `plugins[]`
  - `knowledgeBases[]`
  - `databases[]`
  - `variables[]`
  - `conversations[]`
- `WorkflowV2DependencyItemDto`
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
