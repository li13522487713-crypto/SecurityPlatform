# Coze Studio 功能复刻实施跟踪清单

> 目标：在 Atlas Security Platform (.NET + Vue3) 上逐条复刻 Coze Studio 全部功能  
> 参考文档：`coze-studio-tech-stack-analysis.md`、`coze-studio-api-inventory.md`、`coze-studio-feature-inventory.md`、`coze-studio-project-cognitive-map.md`  
> 状态标记：`[ ]` 待做 | `[~]` 进行中 | `[x]` 已完成 | `[-]` 跳过/不适用

---

## Phase 0：基础设施与架构准备

### 0.1 LLM Provider 抽象层

| # | 任务 | 后端 | 前端 | 状态 |
|---|------|------|------|------|
| 0.1.1 | 定义 `ILlmProvider` 接口（Chat / Stream / Embedding） | `Atlas.Application` | — | [x] |
| 0.1.2 | 实现 OpenAI Provider（Chat + Embedding） | `Atlas.Infrastructure` | — | [x] |
| 0.1.3 | 实现 DeepSeek Provider | `Atlas.Infrastructure` | — | [x] |
| 0.1.4 | 实现 Ollama Provider（本地模型） | `Atlas.Infrastructure` | — | [x] |
| 0.1.5 | 实现 Claude Provider | `Atlas.Infrastructure` | — | [ ] |
| 0.1.6 | 实现 Gemini Provider | `Atlas.Infrastructure` | — | [ ] |
| 0.1.7 | 实现 Qwen Provider | `Atlas.Infrastructure` | — | [ ] |
| 0.1.8 | 实现 Ark (ByteDance) Provider | `Atlas.Infrastructure` | — | [ ] |
| 0.1.9 | Provider 工厂：根据配置动态选择 Provider | `Atlas.Infrastructure` | — | [x] |
| 0.1.10 | LLM 配置管理（appsettings / 数据库） | `Atlas.Infrastructure` | — | [x] |

### 0.2 向量存储抽象层

| # | 任务 | 后端 | 前端 | 状态 |
|---|------|------|------|------|
| 0.2.1 | 定义 `IVectorStore` 接口（Insert / Search / Delete） | `Atlas.Application` | — | [x] |
| 0.2.2 | 实现 SQLite 内存向量存储（余弦相似度） | `Atlas.Infrastructure` | — | [x] |
| 0.2.3 | 实现 Milvus 向量存储（可选） | `Atlas.Infrastructure` | — | [ ] |
| 0.2.4 | 实现 Elasticsearch 向量存储（可选） | `Atlas.Infrastructure` | — | [ ] |

### 0.3 文档解析管道

| # | 任务 | 后端 | 前端 | 状态 |
|---|------|------|------|------|
| 0.3.1 | 定义 `IDocumentParser` 接口 | `Atlas.Application` | — | [x] |
| 0.3.2 | 实现 TXT 解析器 | `Atlas.Infrastructure` | — | [x] |
| 0.3.3 | 实现 PDF 解析器（PdfPig / iText7） | `Atlas.Infrastructure` | — | [x] |
| 0.3.4 | 实现 DOCX 解析器 | `Atlas.Infrastructure` | — | [x] |
| 0.3.5 | 实现 Markdown 解析器 | `Atlas.Infrastructure` | — | [x] |
| 0.3.6 | 实现 CSV / XLSX 解析器 | `Atlas.Infrastructure` | — | [x] |
| 0.3.7 | 实现 JSON 解析器 | `Atlas.Infrastructure` | — | [x] |
| 0.3.8 | 文档分块服务（固定长度 + overlap） | `Atlas.Infrastructure` | — | [x] |
| 0.3.9 | 文档分块服务（语义分块，二期） | `Atlas.Infrastructure` | — | [ ] |

### 0.4 事件总线

| # | 任务 | 后端 | 前端 | 状态 |
|---|------|------|------|------|
| 0.4.1 | 定义 `IEventBus` 接口（Publish / Subscribe） | `Atlas.Application` | — | [x] |
| 0.4.2 | 实现内存事件总线（Channel-based） | `Atlas.Infrastructure` | — | [x] |
| 0.4.3 | 实现 RabbitMQ / Redis Stream 事件总线（可选） | `Atlas.Infrastructure` | — | [ ] |

### 0.5 对象存储抽象

| # | 任务 | 后端 | 前端 | 状态 |
|---|------|------|------|------|
| 0.5.1 | 复用现有 `IFileStorageService` | — | — | [x] |
| 0.5.2 | 扩展支持 MinIO / S3（可选） | `Atlas.Infrastructure` | — | [ ] |

---

## Phase 1：用户认证（复用现有）

> Coze 对应：Passport 模块（7 个接口）

| # | 功能 | Coze 接口 | Atlas 现状 | 状态 |
|---|------|-----------|-----------|------|
| 1.1 | 邮箱注册 | `POST /api/passport/web/email/register/v2/` | 已有用户注册 | [-] |
| 1.2 | 邮箱登录 | `POST /api/passport/web/email/login/` | `POST /api/v1/auth/token` JWT | [-] |
| 1.3 | 登出 | `GET /api/passport/web/logout/` | `POST /api/v1/auth/logout` | [-] |
| 1.4 | 密码重置 | `GET /api/passport/web/email/password/reset/` | 已有密码策略 | [-] |
| 1.5 | 账号信息 | `POST /api/passport/account/info/v2/` | `ICurrentUserAccessor` | [-] |
| 1.6 | 资料更新 | `POST /api/user/update_profile` | `UsersController` | [-] |
| 1.7 | 头像上传 | `POST /api/web/user/update/upload_avatar/` | `FilesController` | [-] |
| 1.8 | 资料预检查 | `POST /api/user/update_profile_check` | 需评估是否需要 | [ ] |
| 1.9 | Session 校验中间件 | `SessionAuthMW` | JWT 中间件已有 | [-] |

---

## Phase 2：模型管理

> Coze 对应：Admin Config / ModelMgr 模块

### 2.1 后端

| # | 功能 | Coze 参考 | 后端文件 | 状态 |
|---|------|-----------|----------|------|
| 2.1.1 | 模型配置实体（Provider / Model / ApiKey / BaseUrl） | `bizpkg/config/modelmgr` | `Atlas.Domain.Agent/ModelConfig.cs` | [x] |
| 2.1.2 | 模型配置 CRUD 服务 | `GetProviderModelList`, `CreateModel`, `DeleteModel` | `IModelConfigService` | [x] |
| 2.1.3 | 模型连通性测试（创建时验证） | `CreateModel` + modelbuilder | `ModelConfigService.TestConnection()` | [x] |
| 2.1.4 | 模型模板管理（预置模板 YAML/JSON） | `conf/model/template/` 22 个 YAML | `ModelTemplates.json` 嵌入资源 | [ ] |
| 2.1.5 | `ModelConfigsController` | `GET/POST/DELETE /api/admin/config/model/*` | `GET/POST/PUT/DELETE /api/v1/model-configs` | [x] |

### 2.2 前端

| # | 功能 | Coze 参考 | 前端文件 | 状态 |
|---|------|-----------|----------|------|
| 2.2.1 | 模型管理页面（Admin） | `/admin` 模型管理 | `pages/ai/ModelConfigPage.vue` | [x] |
| 2.2.2 | 新建模型 Modal（选 Provider + 填 ApiKey + 测试） | Admin 模型创建 | 同上 | [x] |
| 2.2.3 | 模型 API 封装 | — | `services/model-config-api.ts` | [x] |

---

## Phase 3：Agent 管理

> Coze 对应：DraftBot / SingleAgent 模块（16 个接口）

### 3.1 后端

| # | 功能 | Coze 接口 | 后端文件 | 状态 |
|---|------|-----------|----------|------|
| 3.1.1 | Agent 实体（Name / SystemPrompt / Model / KnowledgeBaseId） | `application/singleagent/` | `Atlas.Domain.Agent/Agent.cs` | [x] |
| 3.1.2 | Agent CRUD 服务（Command + Query） | `Create/Update/Delete/GetAgentBotInfo` | `IAgentCommandService`, `IAgentQueryService` | [x] |
| 3.1.3 | 创建 Agent（自动设置默认模型） | `CreateSingleAgentDraft` | `AgentCommandService.Create()` | [x] |
| 3.1.4 | 更新 Agent（名称/模型/Prompt/知识库关联） | `UpdateSingleAgentDraft` | `AgentCommandService.Update()` | [x] |
| 3.1.5 | 删除 Agent | `DeleteAgentDraft` | `AgentCommandService.Delete()` | [x] |
| 3.1.6 | 复制 Agent（含关联资源） | `DuplicateDraftBot` | `AgentCommandService.Duplicate()` | [x] |
| 3.1.7 | 获取 Agent 完整信息 | `GetAgentBotInfo` | `AgentQueryService.GetById()` | [x] |
| 3.1.8 | Agent 列表（分页/搜索） | `GetDraftIntelligenceList` | `AgentQueryService.List()` | [x] |
| 3.1.9 | Agent 展示信息 Get/Update | `Get/UpdateDraftBotDisplayInfo` | 合并到 CRUD | [x] |
| 3.1.10 | `AgentsController` | `/api/draftbot/*` | `GET/POST/PUT/DELETE /api/v1/agents` | [x] |

### 3.2 Agent 发布

| # | 功能 | Coze 接口 | 后端文件 | 状态 |
|---|------|-----------|----------|------|
| 3.2.1 | 发布 Agent（版本快照） | `PublishAgent` | `AgentCommandService.Publish()` | [x] |
| 3.2.2 | 发布历史列表 | `ListAgentPublishHistory` | `AgentQueryService.ListPublishHistory()` | [ ] |
| 3.2.3 | 发布渠道列表 | `GetPublishConnectorList` | `AgentQueryService.ListConnectors()` | [ ] |
| 3.2.4 | 发布预检查 | `CheckDraftBotCommit` | `AgentCommandService.PrePublishCheck()` | [ ] |
| 3.2.5 | Agent 在线信息（Open API） | `GetAgentOnlineInfo`, `OpenGetBotInfo` | `GET /api/v1/agents/{id}/online` | [ ] |

### 3.3 前端

| # | 功能 | Coze 参考 | 前端文件 | 状态 |
|---|------|-----------|----------|------|
| 3.3.1 | Agent 列表页 | `/space/:id/develop` | `pages/ai/AgentListPage.vue` | [x] |
| 3.3.2 | 创建 Agent Modal | DraftBotCreate | 同上 | [x] |
| 3.3.3 | Agent 编辑页（IDE 风格） | `/space/:id/bot/:bot_id` Agent IDE | `pages/ai/AgentEditorPage.vue` | [x] |
| 3.3.4 | Prompt 编辑器 | `agent-ide` PromptEditor | 同上 | [x] |
| 3.3.5 | 模型选择器 | `agent-ide/model-manager` | 同上 | [x] |
| 3.3.6 | 知识库绑定 | Agent BindDatabase | 同上 | [x] |
| 3.3.7 | Agent 发布页 | `/space/:id/bot/:bot_id/publish` | `pages/ai/AgentPublishPage.vue` | [ ] |
| 3.3.8 | Agent API 封装 | — | `services/agent-api.ts` | [x] |

---

## Phase 4：对话系统

> Coze 对应：Conversation / Message 模块（17 个接口）

### 4.1 后端

| # | 功能 | Coze 接口 | 后端文件 | 状态 |
|---|------|-----------|----------|------|
| 4.1.1 | Conversation 实体 | `conversation/` | `Atlas.Domain.Agent/Conversation.cs` | [ ] |
| 4.1.2 | Message 实体（Role / Content / Metadata） | `conversation/message.thrift` | `Atlas.Domain.Agent/Message.cs` | [ ] |
| 4.1.3 | 创建会话 | `CreateConversation` | `ConversationService.Create()` | [ ] |
| 4.1.4 | 会话列表 | `ListConversation` | `ConversationService.List()` | [ ] |
| 4.1.5 | 更新会话 | `UpdateConversation` | `ConversationService.Update()` | [ ] |
| 4.1.6 | 删除会话 | `DeleteConversation` | `ConversationService.Delete()` | [ ] |
| 4.1.7 | 获取会话 | `RetrieveConversation` | `ConversationService.GetById()` | [ ] |
| 4.1.8 | Agent 对话（同步） | `POST /api/conversation/chat` | `AgentChatService.Chat()` | [ ] |
| 4.1.9 | Agent 对话（SSE 流式） | `POST /api/conversation/chat` SSE | `AgentChatService.ChatStream()` | [ ] |
| 4.1.10 | 消息列表 | `GetMessageList` | `MessageService.List()` | [ ] |
| 4.1.11 | 删除消息 | `DeleteMessage` | `MessageService.Delete()` | [ ] |
| 4.1.12 | 中断消息生成 | `BreakMessage` | `AgentChatService.Cancel()` | [ ] |
| 4.1.13 | 清除上下文 | `CreateSection` | `ConversationService.ClearContext()` | [ ] |
| 4.1.14 | 清除历史 | `ClearHistory` | `ConversationService.ClearHistory()` | [ ] |
| 4.1.15 | `ConversationsController` | `/api/conversation/*` | `POST /api/v1/agents/{id}/chat[/stream]` | [ ] |
| 4.1.16 | `ConversationsController` REST | `/v1/conversations/*` | `GET/POST/PUT/DELETE /api/v1/conversations` | [ ] |

### 4.2 前端

| # | 功能 | Coze 参考 | 前端文件 | 状态 |
|---|------|-----------|----------|------|
| 4.2.1 | Agent Chat 页面（左侧会话列表 + 右侧消息区） | `common/chat-area` | `pages/ai/AgentChatPage.vue` | [ ] |
| 4.2.2 | 消息气泡组件 | `chat-area` message | `components/ai/ChatMessage.vue` | [ ] |
| 4.2.3 | SSE 流式渲染 | `hertz-contrib/sse` | `composables/useStreamChat.ts` | [ ] |
| 4.2.4 | 新建会话 | CreateConversation | 同上 | [ ] |
| 4.2.5 | 切换会话 | ListConversation | 同上 | [ ] |
| 4.2.6 | 清除上下文 / 历史 | ClearHistory | 同上 | [ ] |
| 4.2.7 | Markdown 渲染 | 消息内容渲染 | `components/ai/MarkdownRenderer.vue` | [ ] |
| 4.2.8 | Chat API 封装 | — | `services/chat-api.ts` | [ ] |

---

## Phase 5：知识库 & RAG

> Coze 对应：Knowledge 模块（36 个接口）

### 5.1 后端 - 知识库

| # | 功能 | Coze 接口 | 后端文件 | 状态 |
|---|------|-----------|----------|------|
| 5.1.1 | KnowledgeBase 实体（Name / Type / Config） | `data/knowledge/` | `Atlas.Domain.Rag/KnowledgeBase.cs` | [ ] |
| 5.1.2 | 创建知识库 | `CreateKnowledge` | `KnowledgeBaseService.Create()` | [ ] |
| 5.1.3 | 知识库列表 | `ListKnowledge` | `KnowledgeBaseService.List()` | [ ] |
| 5.1.4 | 知识库详情 | `DatasetDetail` | `KnowledgeBaseService.GetById()` | [ ] |
| 5.1.5 | 更新知识库 | `UpdateKnowledge` | `KnowledgeBaseService.Update()` | [ ] |
| 5.1.6 | 删除知识库 | `DeleteKnowledge` | `KnowledgeBaseService.Delete()` | [ ] |
| 5.1.7 | `KnowledgeBasesController` | `/api/knowledge/*` | `GET/POST/PUT/DELETE /api/v1/knowledge-bases` | [ ] |

### 5.2 后端 - 文档管理

| # | 功能 | Coze 接口 | 后端文件 | 状态 |
|---|------|-----------|----------|------|
| 5.2.1 | Document 实体（FileId / Status / ExtractedContent） | `data/knowledge/document.thrift` | `Atlas.Domain.Rag/Document.cs` | [ ] |
| 5.2.2 | 创建文档（上传 + 触发解析） | `CreateDocument` | `DocumentService.Create()` | [ ] |
| 5.2.3 | 文档列表 | `ListDocument` | `DocumentService.List()` | [ ] |
| 5.2.4 | 删除文档 | `DeleteDocument` | `DocumentService.Delete()` | [ ] |
| 5.2.5 | 更新文档 | `UpdateDocument` | `DocumentService.Update()` | [ ] |
| 5.2.6 | 文档处理进度 | `GetDocumentProgress` | `DocumentService.GetProgress()` | [ ] |
| 5.2.7 | 重新分段 | `Resegment` | `DocumentService.Resegment()` | [ ] |
| 5.2.8 | `DocumentsController` | `/api/knowledge/document/*` | `GET/POST/DELETE /api/v1/knowledge-bases/{id}/documents` | [ ] |

### 5.3 后端 - 片段 & 向量化

| # | 功能 | Coze 接口 | 后端文件 | 状态 |
|---|------|-----------|----------|------|
| 5.3.1 | DocumentChunk 实体 | `knowledge/slice.*` | `Atlas.Domain.Rag/DocumentChunk.cs` | [ ] |
| 5.3.2 | Embedding 实体 | 向量存储 | `Atlas.Domain.Rag/ChunkEmbedding.cs` | [ ] |
| 5.3.3 | 创建片段 | `CreateSlice` | `ChunkService.Create()` | [ ] |
| 5.3.4 | 片段列表 | `ListSlice` | `ChunkService.List()` | [ ] |
| 5.3.5 | 更新片段 | `UpdateSlice` | `ChunkService.Update()` | [ ] |
| 5.3.6 | 删除片段 | `DeleteSlice` | `ChunkService.Delete()` | [ ] |
| 5.3.7 | 自动分块 + 向量化管道（上传后异步执行） | `eventbus/knowledgeEventHandler` | `DocumentProcessingService` | [ ] |
| 5.3.8 | RAG 检索（根据问题检索 TopK 片段） | `KnowledgeRetriever` 工作流节点 | `RagRetrievalService.Search()` | [ ] |
| 5.3.9 | RAG 上下文注入 Chat | Agent 对话 + RAG | `AgentChatService.ChatWithRag()` | [ ] |
| 5.3.10 | `ChunksController` | `/api/knowledge/slice/*` | `GET/POST/PUT/DELETE /api/v1/knowledge-bases/{id}/chunks` | [ ] |

### 5.4 后端 - 表 Schema & 审核

| # | 功能 | Coze 接口 | 后端文件 | 状态 |
|---|------|-----------|----------|------|
| 5.4.1 | 表 Schema 获取/校验 | `GetTableSchema`, `ValidateTableSchema` | `TableSchemaService` | [ ] |
| 5.4.2 | 文档审核（创建/获取/保存） | `CreateDocumentReview`, `MGetDocumentReview`, `SaveDocumentReview` | `DocumentReviewService` | [ ] |

### 5.5 后端 - 图片知识库

| # | 功能 | Coze 接口 | 后端文件 | 状态 |
|---|------|-----------|----------|------|
| 5.5.1 | 图片列表 | `ListPhoto` | `PhotoService.List()` | [ ] |
| 5.5.2 | 图片详情 | `PhotoDetail` | `PhotoService.GetById()` | [ ] |
| 5.5.3 | 更新图片描述 | `UpdatePhotoCaption` | `PhotoService.UpdateCaption()` | [ ] |
| 5.5.4 | AI 提取图片描述 | `ExtractPhotoCaption` | `PhotoService.ExtractCaption()` | [ ] |
| 5.5.5 | OCR 服务 | `infra/document/ocr` | `OcrService` | [ ] |

### 5.6 前端

| # | 功能 | Coze 参考 | 前端文件 | 状态 |
|---|------|-----------|----------|------|
| 5.6.1 | 知识库列表页 | `data/knowledge` | `pages/ai/KnowledgeBaseListPage.vue` | [ ] |
| 5.6.2 | 创建知识库 Modal（类型：文本/表格/图片） | 知识库类型选择 | 同上 | [ ] |
| 5.6.3 | 知识库详情页（文档管理） | `/space/:id/knowledge/:dataset_id` | `pages/ai/KnowledgeBaseDetailPage.vue` | [ ] |
| 5.6.4 | 文档上传（拖拽 + 文件选择） | 文档上传 | 同上 | [ ] |
| 5.6.5 | 文档处理进度条 | `GetDocumentProgress` | 同上 | [ ] |
| 5.6.6 | 片段预览列表 | `ListSlice` | 同上 | [ ] |
| 5.6.7 | 片段编辑 Modal | `UpdateSlice` | 同上 | [ ] |
| 5.6.8 | Knowledge API 封装 | — | `services/knowledge-api.ts` | [ ] |

---

## Phase 6：工作流引擎

> Coze 对应：Workflow 模块（52 个接口，47 种节点）

### 6.1 后端 - 核心

| # | 功能 | Coze 接口 | 后端文件 | 状态 |
|---|------|-----------|----------|------|
| 6.1.1 | Workflow 实体（Name / Canvas JSON / Status） | `domain/workflow/` | `Atlas.Domain.AiWorkflow/Workflow.cs` | [ ] |
| 6.1.2 | 创建工作流 | `CreateWorkflow` | `WorkflowService.Create()` | [ ] |
| 6.1.3 | 保存工作流（画布 + 节点配置） | `SaveWorkflow` | `WorkflowService.Save()` | [ ] |
| 6.1.4 | 获取画布信息 | `GetCanvasInfo` | `WorkflowService.GetCanvas()` | [ ] |
| 6.1.5 | 工作流详情 | `GetWorkflowDetail` | `WorkflowService.GetById()` | [ ] |
| 6.1.6 | 工作流列表 | `GetWorkFlowList` | `WorkflowService.List()` | [ ] |
| 6.1.7 | 更新工作流元数据 | `UpdateWorkflowMeta` | `WorkflowService.UpdateMeta()` | [ ] |
| 6.1.8 | 删除工作流 | `DeleteWorkflow` | `WorkflowService.Delete()` | [ ] |
| 6.1.9 | 批量删除 | `BatchDeleteWorkflow` | `WorkflowService.BatchDelete()` | [ ] |
| 6.1.10 | 复制工作流 | `CopyWorkflow` | `WorkflowService.Copy()` | [ ] |
| 6.1.11 | 校验 DAG 结构 | `ValidateTree` | `WorkflowService.Validate()` | [ ] |
| 6.1.12 | 引用查询 | `GetWorkflowReferences` | `WorkflowService.GetReferences()` | [ ] |
| 6.1.13 | `WorkflowsController` | `/api/workflow_api/*` | `GET/POST/PUT/DELETE /api/v1/workflows` | [ ] |

### 6.2 后端 - 发布

| # | 功能 | Coze 接口 | 后端文件 | 状态 |
|---|------|-----------|----------|------|
| 6.2.1 | 发布工作流 | `PublishWorkflow` | `WorkflowService.Publish()` | [ ] |
| 6.2.2 | 已发布工作流列表 | `GetReleasedWorkflows` | `WorkflowService.ListPublished()` | [ ] |
| 6.2.3 | 发布记录 | `ListPublishWorkflow` | `WorkflowService.ListPublishRecords()` | [ ] |

### 6.3 后端 - 运行 & 调试

| # | 功能 | Coze 接口 | 后端文件 | 状态 |
|---|------|-----------|----------|------|
| 6.3.1 | 工作流执行引擎（DAG 调度器） | `domain/workflow/internal/execute/` | `WorkflowEngine` | [ ] |
| 6.3.2 | 测试运行 | `WorkFlowTestRun` | `WorkflowEngine.TestRun()` | [ ] |
| 6.3.3 | 测试恢复（从断点） | `WorkFlowTestResume` | `WorkflowEngine.TestResume()` | [ ] |
| 6.3.4 | 取消运行 | `CancelWorkFlow` | `WorkflowEngine.Cancel()` | [ ] |
| 6.3.5 | 获取运行进度 | `GetWorkFlowProcess` | `WorkflowEngine.GetProgress()` | [ ] |
| 6.3.6 | 节点执行历史 | `GetNodeExecuteHistory` | `WorkflowEngine.GetNodeHistory()` | [ ] |
| 6.3.7 | 单节点调试 | `WorkflowNodeDebugV2` | `WorkflowEngine.DebugNode()` | [ ] |
| 6.3.8 | 检查点存储（Redis / 内存） | `infra/checkpoint/` | `ICheckpointStore` | [ ] |

### 6.4 后端 - 节点类型（47 种，按优先级分批）

**一期：核心节点（10 种）**

| # | 节点 | Coze NodeType | 后端文件 | 状态 |
|---|------|---------------|----------|------|
| 6.4.1 | Entry（开始） | `NodeTypeEntry = 1` | `Nodes/EntryNode.cs` | [ ] |
| 6.4.2 | Exit（结束） | `NodeTypeExit = 2` | `Nodes/ExitNode.cs` | [ ] |
| 6.4.3 | LLM（大模型调用） | `NodeTypeLLM = 3` | `Nodes/LlmNode.cs` | [ ] |
| 6.4.4 | Plugin（插件/API） | `NodeTypePlugin = 4` | `Nodes/PluginNode.cs` | [ ] |
| 6.4.5 | CodeRunner（代码执行） | `NodeTypeCodeRunner = 5` | `Nodes/CodeRunnerNode.cs` | [ ] |
| 6.4.6 | KnowledgeRetriever（知识检索） | `NodeTypeKnowledgeRetriever = 6` | `Nodes/KnowledgeRetrieverNode.cs` | [ ] |
| 6.4.7 | Selector（条件分支） | `NodeTypeSelector = 8` | `Nodes/SelectorNode.cs` | [ ] |
| 6.4.8 | TextProcessor（文本处理） | `NodeTypeTextProcessor = 15` | `Nodes/TextProcessorNode.cs` | [ ] |
| 6.4.9 | HTTPRequester（HTTP 请求） | `NodeTypeHTTPRequester = 45` | `Nodes/HttpRequesterNode.cs` | [ ] |
| 6.4.10 | OutputEmitter（输出消息） | `NodeTypeOutputEmitter = 13` | `Nodes/OutputEmitterNode.cs` | [ ] |

**二期：逻辑控制节点（7 种）**

| # | 节点 | Coze NodeType | 状态 |
|---|------|---------------|------|
| 6.4.11 | SubWorkflow（子工作流） | `9` | [ ] |
| 6.4.12 | Loop（循环） | `21` | [ ] |
| 6.4.13 | Batch（批处理） | `28` | [ ] |
| 6.4.14 | Break | `19` | [ ] |
| 6.4.15 | Continue | `29` | [ ] |
| 6.4.16 | IntentDetector（意图识别） | `22` | [ ] |
| 6.4.17 | QuestionAnswer（用户提问） | `18` | [ ] |

**三期：数据节点（9 种）**

| # | 节点 | Coze NodeType | 状态 |
|---|------|---------------|------|
| 6.4.18 | DatabaseQuery | `43` | [ ] |
| 6.4.19 | DatabaseInsert | `46` | [ ] |
| 6.4.20 | DatabaseUpdate | `42` | [ ] |
| 6.4.21 | DatabaseDelete | `44` | [ ] |
| 6.4.22 | DatabaseCustomSQL | `12` | [ ] |
| 6.4.23 | VariableAssigner | `40` | [ ] |
| 6.4.24 | VariableAssignerWithinLoop | `20` | [ ] |
| 6.4.25 | VariableAggregator | `32` | [ ] |
| 6.4.26 | KnowledgeIndexer | `27` | [ ] |

**四期：对话/消息节点（10 种）**

| # | 节点 | Coze NodeType | 状态 |
|---|------|---------------|------|
| 6.4.27 | CreateConversation | `39` | [ ] |
| 6.4.28 | ConversationUpdate | `51` | [ ] |
| 6.4.29 | ConversationDelete | `52` | [ ] |
| 6.4.30 | ConversationList | `53` | [ ] |
| 6.4.31 | ConversationHistory | `54` | [ ] |
| 6.4.32 | ClearConversationHistory | `38` | [ ] |
| 6.4.33 | MessageList | `37` | [ ] |
| 6.4.34 | CreateMessage | `55` | [ ] |
| 6.4.35 | EditMessage | `56` | [ ] |
| 6.4.36 | DeleteMessage | `57` | [ ] |

**五期：工具节点（5 种）**

| # | 节点 | Coze NodeType | 状态 |
|---|------|---------------|------|
| 6.4.37 | InputReceiver | `30` | [ ] |
| 6.4.38 | JsonSerialization | `58` | [ ] |
| 6.4.39 | JsonDeserialization | `59` | [ ] |
| 6.4.40 | KnowledgeDeleter | `60` | [ ] |
| 6.4.41 | Comment | `31` | [ ] |

### 6.5 后端 - 工作流辅助

| # | 功能 | Coze 接口 | 状态 |
|---|------|-----------|------|
| 6.5.1 | 节点类型查询 | `QueryWorkflowNodeTypes` | [ ] |
| 6.5.2 | 节点模板列表 | `NodeTemplateList` | [ ] |
| 6.5.3 | 节点面板搜索 | `NodePanelSearch` | [ ] |
| 6.5.4 | LLM 函数调用设置 | `GetLLMNodeFCSettingDetail/Merged` | [ ] |
| 6.5.5 | 示例工作流列表 | `GetExampleWorkFlowList` | [ ] |
| 6.5.6 | ChatFlow 角色 CRUD | `Create/Delete/GetChatFlowRole` | [ ] |
| 6.5.7 | 项目对话定义 CRUD | `Create/Update/Delete/ListApplicationConversationDef` | [ ] |
| 6.5.8 | Trace 追踪 | `GetTraceSDK`, `ListRootSpans` | [ ] |
| 6.5.9 | 历史 Schema | `GetHistorySchema` | [ ] |
| 6.5.10 | API 详情 | `GetApiDetail` | [ ] |

### 6.6 后端 - Open API 工作流

| # | 功能 | Coze 接口 | 状态 |
|---|------|-----------|------|
| 6.6.1 | 工作流同步运行 | `POST /v1/workflow/run` | [ ] |
| 6.6.2 | 工作流流式运行 | `POST /v1/workflow/stream_run` | [ ] |
| 6.6.3 | 工作流流式恢复 | `POST /v1/workflow/stream_resume` | [ ] |
| 6.6.4 | 运行历史 | `GET /v1/workflow/get_run_history` | [ ] |
| 6.6.5 | 创建工作流会话 | `POST /v1/workflow/conversation/create` | [ ] |
| 6.6.6 | ChatFlow 运行 | `POST /v1/workflows/chat` | [ ] |
| 6.6.7 | 获取工作流信息 | `GET /v1/workflows/:workflow_id` | [ ] |

### 6.7 前端 - 工作流编辑器

| # | 功能 | Coze 参考 | 前端文件 | 状态 |
|---|------|-----------|----------|------|
| 6.7.1 | 工作流列表页 | `studio/workspace` | `pages/ai/WorkflowListPage.vue` | [ ] |
| 6.7.2 | 工作流画布（拖拽节点 + 连线） | `workflow/canvas` FlowGram | `pages/ai/WorkflowEditorPage.vue` | [ ] |
| 6.7.3 | 节点面板（左侧节点库） | `workflow/nodes` | 同上 | [ ] |
| 6.7.4 | 节点配置表单 | `workflow/nodes/setters` | 同上 | [ ] |
| 6.7.5 | 变量系统（输入/输出变量） | `workflow/variable` | 同上 | [ ] |
| 6.7.6 | 测试运行面板 | `workflow/test-run` | 同上 | [ ] |
| 6.7.7 | 运行日志查看 | `workflow/test-run` log viewer | 同上 | [ ] |
| 6.7.8 | 发布工作流 | `workflow/publish` | 同上 | [ ] |
| 6.7.9 | Workflow API 封装 | — | `services/workflow-api.ts` | [ ] |

---

## Phase 7：数据库 & 变量系统

> Coze 对应：Memory/Database 模块（28 个接口）

### 7.1 后端 - 数据库

| # | 功能 | Coze 接口 | 状态 |
|---|------|-----------|------|
| 7.1.1 | Database 实体 | `data/database/` | [ ] |
| 7.1.2 | 数据库 CRUD | `Add/Update/Delete/ListDatabase` | [ ] |
| 7.1.3 | 绑定/解绑到 Bot | `BindDatabase`, `UnBindDatabase` | [ ] |
| 7.1.4 | 记录查询 | `ListDatabaseRecords` | [ ] |
| 7.1.5 | 记录更新（新增/修改/删除） | `UpdateDatabaseRecords` | [ ] |
| 7.1.6 | 表 Schema 获取/校验 | `GetDatabaseTableSchema`, `ValidateDatabaseTableSchema` | [ ] |
| 7.1.7 | 数据导入（异步） | `SubmitDatabaseInsertTask` | [ ] |
| 7.1.8 | 导入进度 | `DatabaseFileProgressData` | [ ] |
| 7.1.9 | 模板导出 | `GetDatabaseTemplate` | [ ] |
| 7.1.10 | `DatabasesController` | `/api/memory/database/*` | [ ] |

### 7.2 后端 - 变量

| # | 功能 | Coze 接口 | 状态 |
|---|------|-----------|------|
| 7.2.1 | Variable 实体（KV 存储） | `data/variable/` | [ ] |
| 7.2.2 | 系统变量配置 | `GetSysVariableConf` | [ ] |
| 7.2.3 | 项目变量 CRUD | `GetProjectVariablesMeta`, `UpdateProjectVariable` | [ ] |
| 7.2.4 | KV 变量 Upsert/Get/Delete | `SetKvMemory`, `GetPlayGroundMemory`, `DelProfileMemory` | [ ] |
| 7.2.5 | 变量元数据 | `GetVariableMeta` | [ ] |
| 7.2.6 | `VariablesController` | `/api/memory/variable/*` | [ ] |

### 7.3 前端

| # | 功能 | 状态 |
|---|------|------|
| 7.3.1 | 数据库列表页 | [ ] |
| 7.3.2 | 数据库详情页（记录管理） | [ ] |
| 7.3.3 | 数据导入 Modal | [ ] |
| 7.3.4 | 变量管理面板 | [ ] |

---

## Phase 8：插件系统

> Coze 对应：Plugin 模块（27 个接口，22 个内置插件）

### 8.1 后端

| # | 功能 | Coze 接口 | 状态 |
|---|------|-----------|------|
| 8.1.1 | Plugin 实体 | `plugin/plugin_develop.thrift` | [ ] |
| 8.1.2 | 插件注册 | `RegisterPlugin`, `RegisterPluginMeta` | [ ] |
| 8.1.3 | 插件 CRUD | `GetPluginInfo`, `UpdatePlugin`, `DelPlugin` | [ ] |
| 8.1.4 | 插件 API CRUD（Create/Update/Delete/Batch） | `CreateAPI`, `UpdateAPI`, `DeleteAPI`, `BatchCreateAPI` | [ ] |
| 8.1.5 | 插件调试 | `DebugAPI` | [ ] |
| 8.1.6 | 插件发布 | `PublishPlugin` | [ ] |
| 8.1.7 | OpenAPI 转换 | `Convert2OpenAPI` | [ ] |
| 8.1.8 | OAuth 流程（Schema/Status/AuthCode） | OAuth 相关 | [ ] |
| 8.1.9 | 编辑锁 | `CheckAndLockPluginEdit`, `UnlockPluginEdit` | [ ] |
| 8.1.10 | Bot 默认参数 | `GetBotDefaultParams`, `UpdateBotDefaultParams` | [ ] |
| 8.1.11 | 内置插件元数据（22 个） | `conf/plugin/pluginproduct/plugin_meta.yaml` | [ ] |
| 8.1.12 | `PluginsController` | `/api/plugin_api/*` | [ ] |

### 8.2 前端

| # | 功能 | 状态 |
|---|------|------|
| 8.2.1 | 插件列表页 | [ ] |
| 8.2.2 | 插件详情页（API 管理） | [ ] |
| 8.2.3 | 插件工具编辑页 | [ ] |
| 8.2.4 | 插件调试面板 | [ ] |

---

## Phase 9：应用/项目管理

> Coze 对应：Intelligence/App 模块（15 个接口）

| # | 功能 | Coze 接口 | 状态 |
|---|------|-----------|------|
| 9.1 | App 实体（包含 Agent + Workflow + Plugin + Knowledge） | `app/intelligence.thrift` | [ ] |
| 9.2 | 草稿项目 CRUD | `DraftProjectCreate/Update/Delete/Copy` | [ ] |
| 9.3 | 项目内任务列表 | `DraftProjectInnerTaskList` | [ ] |
| 9.4 | 项目发布 | `PublishProject` | [ ] |
| 9.5 | 版本检查 | `CheckProjectVersionNumber` | [ ] |
| 9.6 | 发布记录 | `GetPublishRecordList/Detail` | [ ] |
| 9.7 | 发布渠道 | `ProjectPublishConnectorList` | [ ] |
| 9.8 | 在线 App 数据 | `GetOnlineAppData` | [ ] |
| 9.9 | Project IDE 页面（VS Code 风格） | `project-ide` | [ ] |
| 9.10 | 资源复制（派发/详情/重试/取消） | `ResourceCopyDispatch/Detail/Retry/Cancel` | [ ] |

---

## Phase 10：Prompt 管理

> Coze 对应：Prompt 模块（4 个接口）

| # | 功能 | Coze 接口 | 状态 |
|---|------|-----------|------|
| 10.1 | Prompt 实体 | `playground/prompt_resource.thrift` | [ ] |
| 10.2 | 创建/更新 Prompt | `UpsertPromptResource` | [ ] |
| 10.3 | Prompt 详情 | `GetPromptResourceInfo` | [ ] |
| 10.4 | 官方 Prompt 列表 | `GetOfficialPromptResourceList` | [ ] |
| 10.5 | 删除 Prompt | `DeletePromptResource` | [ ] |
| 10.6 | Prompt 库页面（前端） | `common/prompt-kit` | [ ] |
| 10.7 | Prompt 编辑/插入组件 | Prompt library / recommend | [ ] |

---

## Phase 11：市场 / 探索

> Coze 对应：Marketplace 模块（10 个接口）

| # | 功能 | Coze 接口 | 状态 |
|---|------|-----------|------|
| 11.1 | 产品列表 | `PublicGetProductList` | [ ] |
| 11.2 | 产品详情 | `PublicGetProductDetail` | [ ] |
| 11.3 | 搜索产品 | `PublicSearchProduct` | [ ] |
| 11.4 | 搜索建议 | `PublicSearchSuggest` | [ ] |
| 11.5 | 分类列表 | `PublicGetProductCategoryList` | [ ] |
| 11.6 | 收藏/取消收藏 | `PublicFavoriteProduct` | [ ] |
| 11.7 | 收藏列表 | `PublicGetUserFavoriteList` | [ ] |
| 11.8 | 复制产品到工作区 | `PublicDuplicateProduct` | [ ] |
| 11.9 | 插件商店页面（前端） | `/explore/plugin` | [ ] |
| 11.10 | 模板商店页面（前端） | `/explore/template` | [ ] |

---

## Phase 12：上传系统

> Coze 对应：Upload 模块（3 个接口 + 多处复用）

| # | 功能 | Coze 接口 | Atlas 现状 | 状态 |
|---|------|-----------|-----------|------|
| 12.1 | 通用文件上传 | `CommonUpload` | `FilesController` 已有 | [-] |
| 12.2 | 分片上传（Init/Part/Complete） | `PartUploadFile*` | 需新增 | [ ] |
| 12.3 | 图片上传（Apply/Commit） | `ApplyImageUpload`, `CommitImageUpload` | 需新增 | [ ] |
| 12.4 | Open API 文件上传 | `POST /v1/files/upload` | 需新增 | [ ] |
| 12.5 | 签名 URL | `SignImageURL` | 需新增 | [ ] |

---

## Phase 13：权限 / PAT

> Coze 对应：Permission/OpenAuth 模块（7 个接口）

| # | 功能 | Coze 接口 | 状态 |
|---|------|-----------|------|
| 13.1 | PAT 实体（Token + Permission + Expiry） | `permission/openapiauth.thrift` | [ ] |
| 13.2 | 创建 PAT | `CreatePersonalAccessTokenAndPermission` | [ ] |
| 13.3 | PAT 列表 | `ListPersonalAccessTokens` | [ ] |
| 13.4 | PAT 详情 | `GetPersonalAccessTokenAndPermission` | [ ] |
| 13.5 | 更新 PAT | `UpdatePersonalAccessTokenAndPermission` | [ ] |
| 13.6 | 删除 PAT | `DeletePersonalAccessTokenAndPermission` | [ ] |
| 13.7 | PAT 权限校验中间件 | `OpenapiAuthMW` | [ ] |
| 13.8 | 模拟用户 | `ImpersonateCozeUserAccessToken` | [ ] |
| 13.9 | OAuth 授权码 | `OauthAuthorizationCode` | [ ] |
| 13.10 | PAT 管理页面（前端） | Account Settings | [ ] |

---

## Phase 14：管理后台

> Coze 对应：Admin 模块（7 个接口）

| # | 功能 | Coze 接口 | Atlas 现状 | 状态 |
|---|------|-----------|-----------|------|
| 14.1 | 基础配置 Get/Save | `GetBasicConfiguration`, `SaveBasicConfiguration` | 部分已有 SystemConfigs | [ ] |
| 14.2 | 知识库配置 Get/Save | `GetKnowledgeConfig`, `UpdateKnowledgeConfig` | 需新增 | [ ] |
| 14.3 | 模型管理（列表/创建/删除） | `GetModelList`, `CreateModel`, `DeleteModel` | 见 Phase 2 | [-] |
| 14.4 | Admin 权限中间件（Email 白名单） | `AdminAuthMW` | 可复用 RBAC | [-] |
| 14.5 | 管理后台页面（前端） | `/admin` | 已有 Admin 布局 | [ ] |

---

## Phase 15：搜索系统

> Coze 对应：Search + EventBus 模块

| # | 功能 | Coze 接口 | 状态 |
|---|------|-----------|------|
| 15.1 | 全局搜索服务 | `GetDraftIntelligenceList` | [ ] |
| 15.2 | 资源搜索（按类型） | `LibraryResourceList`, `ProjectResourceList` | [ ] |
| 15.3 | 最近编辑 | `GetUserRecentlyEditIntelligence` | [ ] |
| 15.4 | 资源事件 → ES 索引同步 | `eventbus/handler_resource` | [ ] |
| 15.5 | 项目事件 → ES 索引同步 | `eventbus/handler_project` | [ ] |
| 15.6 | 搜索页面（前端） | `/search/:word` | [ ] |

---

## Phase 16：工作空间

> Coze 对应：Space / Develop / Library

| # | 功能 | Coze 参考 | 状态 |
|---|------|-----------|------|
| 16.1 | Space 实体与管理 | `application/user/GetSpaceListV2` | [ ] |
| 16.2 | 开发列表页（Agent + App + Project） | `/space/:id/develop` | [ ] |
| 16.3 | 资源库页（Plugin/Workflow/Knowledge/Prompt/Database） | `/space/:id/library` | [ ] |
| 16.4 | 全局布局（侧边栏 + 顶栏） | `foundation/layout` | [ ] |

---

## Phase 17：DevOps / 调试

| # | 功能 | Coze 参考 | 状态 |
|---|------|-----------|------|
| 17.1 | 测试集管理（创建/编辑/删除） | `devops/testset` | [ ] |
| 17.2 | Mock 集管理 | `devops/mockset` | [ ] |
| 17.3 | 调试面板（Trace 树 / Span 详情） | `devops/debug` | [ ] |
| 17.4 | JSON / PDF / 图片预览 | `devops/json-link-preview` | [ ] |

---

## Phase 18：代码沙箱

| # | 功能 | Coze 参考 | 状态 |
|---|------|-----------|------|
| 18.1 | Python 直接执行（exec） | `infra/coderunner/impl/direct` | [ ] |
| 18.2 | Python 沙箱执行（隔离环境） | `infra/coderunner/impl/sandbox` | [ ] |
| 18.3 | JavaScript 执行（Coze 未完成） | 预留 | [ ] |
| 18.4 | 第三方模块白名单管理 | `config.yaml` SupportThirdPartModules | [ ] |

---

## Phase 19：快捷命令 & Playground

| # | 功能 | Coze 接口 | 状态 |
|---|------|-----------|------|
| 19.1 | 快捷命令 CRUD | `CreateUpdateShortcutCommand` | [ ] |
| 19.2 | Onboarding | `GetOnboarding` | [ ] |
| 19.3 | 行为上报 | `ReportUserBehavior` | [ ] |
| 19.4 | Bot 弹窗信息 | `Get/UpdateBotPopupInfo` | [ ] |

---

## Phase 20：Open Platform / SDK

| # | 功能 | Coze 参考 | 状态 |
|---|------|-----------|------|
| 20.1 | Chat v3 API（SSE / 同步） | `POST /v3/chat` | [ ] |
| 20.2 | Chat 取消 | `POST /v3/chat/cancel` | [ ] |
| 20.3 | Chat 检索 | `GET /v3/chat/retrieve` | [ ] |
| 20.4 | Chat 消息列表 | `GET /v3/chat/message/list` | [ ] |
| 20.5 | Knowledge Open API（CRUD） | `/v1/datasets/*`, `/open_api/knowledge/document/*` | [ ] |
| 20.6 | Bot Open API | `GET /v1/bots/:bot_id` | [ ] |
| 20.7 | App Open API | `GET /v1/apps/:app_id` | [ ] |
| 20.8 | File Open API | `POST /v1/files/upload` | [ ] |
| 20.9 | Web Chat SDK 集成 | `studio/open-platform` | [ ] |
| 20.10 | SDK 接入引导页 | `studio/workspace/publish` Web SDK Guide | [ ] |

---

## 统计总览

| Phase | 模块 | 后端任务 | 前端任务 | 总计 |
|-------|------|----------|----------|------|
| 0 | 基础设施 | 22 | 0 | 22 |
| 1 | 认证（复用） | 0 | 0 | 0 |
| 2 | 模型管理 | 5 | 3 | 8 |
| 3 | Agent 管理 | 15 | 8 | 23 |
| 4 | 对话系统 | 16 | 8 | 24 |
| 5 | 知识库 & RAG | 27 | 8 | 35 |
| 6 | 工作流引擎 | 61 | 9 | 70 |
| 7 | 数据库/变量 | 16 | 4 | 20 |
| 8 | 插件系统 | 12 | 4 | 16 |
| 9 | 应用/项目 | 10 | 0 | 10 |
| 10 | Prompt | 7 | 0 | 7 |
| 11 | 市场/探索 | 8 | 2 | 10 |
| 12 | 上传系统 | 4 | 0 | 4 |
| 13 | 权限/PAT | 10 | 0 | 10 |
| 14 | 管理后台 | 2 | 1 | 3 |
| 15 | 搜索系统 | 5 | 1 | 6 |
| 16 | 工作空间 | 1 | 3 | 4 |
| 17 | DevOps | 0 | 4 | 4 |
| 18 | 代码沙箱 | 4 | 0 | 4 |
| 19 | 快捷命令 | 4 | 0 | 4 |
| 20 | Open Platform | 10 | 0 | 10 |
| **合计** | | **~239** | **~55** | **~294** |

---

## 推荐实施顺序

```
Phase 0 (基础设施)
  → Phase 2 (模型管理)
    → Phase 3 (Agent)
      → Phase 4 (对话)
        → Phase 5 (知识库 RAG) ← 首个完整 MVP
          → Phase 6 (工作流) ← 核心差异化
            → Phase 7-8 (数据库 + 插件)
              → Phase 9-20 (其余模块)
```

**首个 MVP（Phase 0-5）**：约 112 个任务，覆盖 Auth + 模型 + Agent + Chat + RAG  
**核心平台（Phase 0-8）**：约 218 个任务，覆盖工作流 + 数据库 + 插件  
**完整复刻（Phase 0-20）**：294 个任务
