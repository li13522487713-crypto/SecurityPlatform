# Coze Atlas Round 2 落仓实施说明

## 目标

本文件将第 2 轮 Coze Studio Atlas 化方案正式落入仓库，作为后续实现的唯一骨架说明。该方案基于当前 `SecurityPlatform` 已存在的以下事实推进，而不是另起一套平行系统：

- 后端已存在 `Atlas.Application.AiPlatform`、`Atlas.Domain.AiPlatform`、`Atlas.Infrastructure.Services.AiPlatform`
- 控制面已存在 `Atlas.PlatformHost.Controllers` 中的 `AiAppsController`、`AiWorkspacesController`、`AiPluginsController`、`AiDatabasesController`、`AiVariablesController`、`WorkflowV2Controller`、`WorkspaceIdeController`
- 运行面已存在 `Atlas.AppHost.Controllers` 中的 `ConversationsController`、`DraftAgentsController`、`Open/OpenBotsController`、`Open/OpenChatController`、`Open/OpenWorkflowsController`
- 前端已存在 `app-web + module-studio-react + module-explore-react + module-workflow-react + workflow-editor-react + project-ide + agent-ide`

## 落仓原则

### 1. 宿主分工不变

- `PlatformHost` 负责设计态、资源态、发布态、市场态、PAT/OpenAPI 管理态
- `AppHost` 负责运行态、调试态、SSE 对话态、Workflow 执行态、对外 OpenAPI 调用态

### 2. `AiPlatform` 仍然是唯一业务主域

- 不新增新的顶层 csproj
- 优先在 `Atlas.Application.AiPlatform`、`Atlas.Domain.AiPlatform` 和 `Atlas.Infrastructure.Services.AiPlatform` 内部重组
- 当前以模型文件平铺的结构，逐步向 `Core / Design / Runtime / OpenApi / Marketplace` 五个子域收束

### 3. 资源聚合统一

所有 Coze 风格资源统一沉淀到 `Atlas.Domain.AiPlatform`：

- 智能体：`Agent`
- 应用：`AiApp`
- 工作流：`WorkflowMeta`、`WorkflowDraft`、`WorkflowVersion`、`WorkflowExecution`
- 插件：`AiPlugin`、`AiPluginApi`
- 知识库：`KnowledgeBase`、`KnowledgeDocument`、`DocumentChunk`
- 数据库：`AiDatabase`、`AiDatabaseRecord`
- 变量与记忆：`AiVariable`、`AiVariableInstance`、`LongTermMemory`
- 会话运行态：`Conversation`、`ConversationSection`、`ChatMessage`、`ChatRunRecord`、`ChatRunEvent`
- 发布态：`AgentPublication`、`AiAppPublishRecord`、`AiPluginPublishRecord`、`AiAppConnectorBinding`
- 搜索态：`AiRecentEdit`、`WorkspaceIdeFavorite`、`AiMarketplaceFavorite`、`AiMarketplaceProduct`

### 4. 编辑态与运行态分离

- 编辑器永远读取草稿态
- 发布后生成快照与版本记录
- 运行态仅读发布态快照
- 旧 JSON 绑定字段仅作兼容缓存，不再作为唯一事实来源

## 后端落仓地图

### `Atlas.Application.AiPlatform`

建议固定为：

- `Abstractions/Core`
- `Abstractions/Design`
- `Abstractions/Runtime`
- `Abstractions/OpenApi`
- `Abstractions/Marketplace`

- `Models/Core`
- `Models/Design`
- `Models/Runtime`
- `Models/OpenApi`
- `Models/Marketplace`

### `Atlas.Domain.AiPlatform.Entities`

建议按聚合分目录：

- `Entities/Agent`
- `Entities/App`
- `Entities/Workflow`
- `Entities/Plugin`
- `Entities/Knowledge`
- `Entities/Database`
- `Entities/VariablesMemory`
- `Entities/ConversationRuntime`
- `Entities/WorkspaceSearch`

### `Atlas.Infrastructure.Services.AiPlatform`

建议按职责分目录：

- `Core`
- `Design`
- `Runtime`
- `OpenApi`
- `Marketplace`

## 前端落仓地图

### `app-web`

在现有 `app-web` 路由层优先固化新的组织-工作空间门户；旧 `/apps/:appKey/*` 仅保留兼容跳转，不再作为主壳：

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
- `/org/:orgId/workspaces/:workspaceId/apps/:id`
- `/org/:orgId/workspaces/:workspaceId/apps/:id/publish`
- `/org/:orgId/workspaces/:workspaceId/agents/:id`
- `/org/:orgId/workspaces/:workspaceId/agents/:id/publish`
- `/org/:orgId/workspaces/:workspaceId/plugins/:id`
- `/org/:orgId/workspaces/:workspaceId/knowledge-bases/:id`
- `/org/:orgId/workspaces/:workspaceId/knowledge-bases/:id/upload`
- `/org/:orgId/workspaces/:workspaceId/databases/:id`
- `/explore/plugins`
- `/explore/templates`
- `/explore/search`
- `/workflows`
- `/chatflows`
- `/workflows/:id/editor`
- `/chatflows/:id/editor`

### `module-studio-react`

从当前单文件页面集合逐步收束到：

- `workspace`
- `assistant`
- `app-ide`
- `library`
- `publish`
- `shared`

### `module-explore-react`

逐步收束到：

- `plugin-market`
- `template-market`
- `search`

### `module-workflow-react`

逐步收束到：

- `list`
- `editor`
- `resource-ide`
- `publish`
- `runtime`

### 内核包复用原则

以下包继续作为内核包复用，不在 Atlas 业务壳里复制实现：

- `agent-ide/*`
- `project-ide/*`
- `workflow/*`
- `studio/workspace/*`
- `community/explore`
- `workflow-editor-react`

## 表结构落地顺序

### 第一批：新增绑定表与运行表

优先新增，不改旧字段含义：

- `AgentWorkflowBinding`
- `AgentDatabaseBinding`
- `AgentVariableBinding`
- `AgentPromptBinding`
- `AgentConversationProfile`
- `AiAppResourceBinding`
- `AiAppConversationTemplate`
- `AiAppConnectorBinding`
- `AiPluginPublishRecord`
- `AiPluginOAuthGrant`
- `KnowledgeReview`
- `KnowledgeSlice`
- `KnowledgeImportTask`
- `AiVariableInstance`
- `ConversationSection`
- `ChatRunRecord`
- `ChatRunEvent`
- `WorkflowReference`
- `WorkflowPublishedReference`
- `WorkflowConversationTemplateLink`

### 第二批：扩展旧实体

在不破坏现有调用前提下，为以下实体补充未来字段：

- `Agent`
- `AiApp`
- `AiPlugin`
- `AiDatabase`
- `AiVariable`

## 下一阶段执行顺序

1. 先补实体与绑定表
2. 再补 Abstractions 和 Service 接口收束
3. 再补 PlatformHost / AppHost 缺失接口
4. 再补 `app-web` 路由与 `module-*` 宿主
5. 最后补 E2E 主链与 OpenAPI 验证
