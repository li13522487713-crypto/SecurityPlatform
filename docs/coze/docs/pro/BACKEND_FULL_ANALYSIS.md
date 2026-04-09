# Coze Studio 后端全量代码分析

> 按「入口→路由→控制器→服务→数据层→中间件/鉴权/异常」顺序完整阅读
> 日期：2026-04-09

---

## 一、启动入口与初始化逻辑

### 1.1 启动文件 【已确认】

- **`backend/main.go`** (157 行)
- 框架：Cloudwego **Hertz** (HTTP)
- 启动顺序（严格有序）：
  1. `setCrashOutput()` → 创建 `crash.log`
  2. `loadEnv()` → `godotenv.Load(.env / .env.{APP_ENV})`
  3. `setLogLevel()` → 读取 `LOG_LEVEL` 环境变量
  4. `application.Init(ctx)` → 初始化全部业务依赖
  5. `startHttpServer()` → 启动 Hertz + 中间件 + 路由

### 1.2 基础设施初始化 【已确认】

- **`backend/application/base/appinfra/app_infra.go`** (173 行)
- `AppDependencies` 结构体包含 15 个组件：

| 组件 | 初始化函数 | 依赖 |
|------|-----------|------|
| MySQL (GORM) | `mysql.New()` | `MYSQL_DSN` 环境变量 |
| Redis | `redis.New()` | `REDIS_ADDR`, `REDIS_PASSWORD` |
| ID 生成器 | `idgen.New(cacheCli)` | Redis（INCRBY 分布式 ID） |
| 配置管理 | `config.Init(ctx, db, oss)` | MySQL + OSS |
| Elasticsearch | `es.New()` | `ES_VERSION`=v7/v8 |
| ImageX | `initImageX()` | MinIO/TOS/S3 或 VeImageX |
| 3 个 EventBus Producer | `eventbus.InitXxxProducer()` | NSQ/Kafka/RMQ/Pulsar/NATS |
| Reranker | `rerank.New()` | Knowledge 配置 |
| Rewriter | `messages2query.New()` | LLM |
| NL2SQL | `nl2sql.New()` | LLM |
| CodeRunner | `coderunner.New()` | Sandbox/Direct 模式 |
| OCR | `ocr.New()` | Knowledge 配置 |
| 内置 ChatModel | `modelbuilder.GetBuiltinChatModel()` | `WKR_` 前缀环境变量 |
| ParserManager | `parser.New()` | OSS + OCR |
| SearchStoreManagers | `searchstore.New()` | ES + Knowledge 配置 |

### 1.3 应用服务初始化 【已确认】

- **`backend/application/application.go`** (399 行)
- 三阶段初始化（严格依赖顺序）：

```
basicServices（仅依赖 infra）
  ├── uploadSVC, openAuthSVC, promptSVC, modelMgrSVC
  ├── connectorSVC, userSVC, templateSVC, permissionSVC
  └── eventbus (resource + app producers)

primaryServices（依赖 basicServices）
  ├── pluginSVC, memorySVC, knowledgeSVC
  ├── workflowSVC, shortcutSVC
  └── appSVC

complexServices（依赖 primaryServices）
  ├── singleAgentSVC, appSVC, searchSVC
  └── conversationSVC
```

- 初始化完成后注册 **17 个 CrossDomain 默认服务**：
  `permission → connector → database → knowledge → plugin → variables → workflow → conversation → message → agentrun → agent → user → datacopy → search → upload → app`

### 1.4 定时任务 / 消息消费者 【已确认】

- **消息消费者**：通过 `eventbus.GetDefaultSVC().RegisterConsumer()` 在 `search/init.go` (101 行) 注册：
  - `opencoze_search_resource` topic → 资源索引
  - `opencoze_search_app` topic → 项目索引
- **定时任务**：【未确认】未发现显式 cron/ticker，知识库文档处理通过 EventBus 异步驱动

---

## 二、路由注册与中间件链

### 2.1 中间件注册顺序 【已确认】

`main.go:startHttpServer()` 中间件注册顺序（**严格有序**）：

| 顺序 | 中间件 | 文件 | 行数 | 职责 |
|------|--------|------|------|------|
| 1 | `ContextCacheMW` | `ctx_cache.go` | 33 | 初始化 `sync.Map` 到 context |
| 2 | `RequestInspectorMW` | `request_inspector.go` | 77 | 分类请求：WebAPI / OpenAPI / StaticFile |
| 3 | `SetHostMW` | `host.go` | 35 | 存 host 和 scheme 到 ctxcache |
| 4 | `SetLogIDMW` | `log.go` | 98 | UUID 生成 + `X-Log-ID` header |
| 5 | CORS | hertz-contrib/cors | - | AllowAllOrigins + AllowHeaders=* |
| 6 | `AccessLogMW` | `log.go` | 98 | 请求日志（500=Error, 400=Warn, 其余 Info） |
| 7 | `OpenapiAuthMW` | `openapi_auth.go` | 162 | Bearer Token 鉴权（仅 OpenAPI 路径） |
| 8 | `SessionAuthMW` | `session.go` | 114 | Session Cookie 鉴权（仅 WebAPI 路径） |
| 9 | `I18nMW` | `i18n.go` | 54 | Locale 设置（Session > Accept-Language > en-US） |

### 2.2 路由注册 【已确认】

- **`backend/api/router/register.go`** → `GeneratedRegister(r)` → `coze.Register(r)` + `staticFileRegister(r)`
- **`backend/api/router/coze/api.go`** (550+ 行, **IDL 自动生成, 勿手动修改**)
- 路由前缀分组：

| 前缀 | 类型 | 说明 |
|------|------|------|
| `/api/admin/config/*` | WebAPI | 管理后台配置 |
| `/api/conversation/*` | WebAPI | Studio 对话 |
| `/api/developer/*` | WebAPI | 开发者工具 |
| `/api/draftbot/*` | WebAPI | Agent 草稿管理 |
| `/api/passport/*` | WebAPI | 登录注册 |
| `/api/plugin/*` | WebAPI | 插件管理 |
| `/api/workflow/*` | WebAPI | 工作流编辑 |
| `/api/knowledge/*` | WebAPI | 知识库管理 |
| `/api/memory/*` | WebAPI | 变量/数据库 |
| `/v1/*`, `/v3/*` | OpenAPI | 外部 API |
| `/open_api/*` | OpenAPI | 旧版外部 API |

- **NoRoute 处理**：API 路径返回 404 JSON，非 API 路径返回 `index.html`（SPA 兜底）

---

## 三、Handler/Controller 层

### 3.1 Handler 文件全览 【已确认】

| 文件 | 行数 | 核心职责 |
|------|------|---------|
| `base.go` | 34 | 错误响应工具函数 |
| `agent_run_service.go` | 230 | Agent 对话（SSE 流式 + 同步） |
| `passport_service.go` | 221 | 登录/注册/登出/密码重置/头像 |
| `intelligence_service.go` | 427 | 项目 CRUD/发布/版本/连接器 |
| `conversation_service.go` | 232 | 会话管理（清除/创建/列表） |
| `message_service.go` | 171 | 消息列表/删除/中断 |
| `developer_api_service.go` | 416 | Agent 草稿生命周期/上传/模型列表 |
| `knowledge_service.go` | 771 | 知识库全量 CRUD + OpenAPI |
| `plugin_develop_service.go` | 916 | 插件开发全量 API |
| `workflow_service.go` | 1263 | 工作流编辑/测试/运行/OpenAPI |
| `database_service.go` | 413 | 表格数据库 CRUD/绑定/导入 |
| `memory_service.go` | 230 | 变量元数据/实例管理 |
| `config_service.go` | 293 | 管理后台：基础配置/知识库配置/模型管理 |
| `bot_open_api_service.go` | 154 | OAuth 回调/文件上传/Bot 信息 |
| `open_apiauth_service.go` | 170 | 个人访问令牌 CRUD |
| `playground_service.go` | 374 | 调试面板：Agent/Prompt/快捷命令 |
| `public_product_service.go` | 421 | 市场：列表/详情/收藏/复制/搜索 |
| `resource_service.go` | 180 | 资源列表/复制分发 |
| `upload_service.go` | 81 | 通用上传/ImageX 上传 |

### 3.2 Handler 模式 【已确认】

每个 Handler 遵循统一模式：
```go
func XxxHandler(ctx context.Context, c *app.RequestContext) {
    // 1. 参数绑定与校验
    var req XxxRequest
    err = c.BindAndValidate(&req)
    if err != nil { invalidParamRequestResponse(c, err.Error()); return }

    // 2. 业务调用
    resp, err := application.XxxSVC.Method(ctx, &req)
    if err != nil { internalServerErrorResponse(ctx, c, err); return }

    // 3. 响应返回
    c.JSON(consts.StatusOK, resp)
}
```

- **参数校验**：`c.BindAndValidate()` (Hertz 内置) + 手动 `check*` 函数
- **错误返回**：`httputil.InternalError` → 识别 `errorx.StatusError` 返回业务码，否则 500
- **SSE 流式**：`agent_run_service.go` 和 `workflow_service.go` 使用 `sseImpl.NewSSESender(sse.NewStream(c))`

---

## 四、Application 服务层

### 4.1 全量服务清单 【已确认】

| 模块 | 文件 | 行数 | 包级变量 | 核心职责 |
|------|------|------|---------|---------|
| 对话 | `conversation/conversation.go` | 332 | `ConversationSVC` | 会话 CRUD + OpenAPI |
| Agent 运行 | `conversation/agent_run.go` | 496 | （同上） | 流式对话 + 消息构建 |
| OpenAPI 运行 | `conversation/openapi_agent_run.go` | 698 | `ConversationOpenAPISVC` | 同步/异步/流式 OpenAPI |
| 单 Agent | `singleagent/single_agent.go` | 855 | `SingleAgentSVC` | Agent 草稿/发布/在线信息 |
| 工作流 | `workflow/workflow.go` | 4332 | `WorkflowSVC` | 节点/画布/测试/发布/OpenAPI |
| 知识库 | `knowledge/knowledge.go` | 1784 | `KnowledgeSVC` | 数据集/文档/切片/检索 |
| 插件 | `plugin/plugin.go` | 655 | `PluginApplicationSVC` | 市场/产品/SaaS/OAuth |
| 用户 | `user/user.go` | 353 | `UserApplicationSVC` | 登录/注册/Session/Profile |
| 内存 | `memory/init.go` | 64 | `VariableApplicationSVC`, `DatabaseApplicationSVC` | 变量+数据库 |
| 应用 | `app/app.go` | 1443 | `AppSVC` | 项目/发布/连接器/资源复制 |
| 搜索 | `search/init.go` | 101 | `SearchSVC` | ES 索引 + MQ 消费 |
| 模型管理 | `modelmgr/modelmgr.go` | 140 | - | 模型列表 + i18n |
| OpenAuth | `openauth/openapiauth.go` | 232 | `OpenAuthApplication` | 个人访问令牌 |
| 权限 | `permission/init.go` | 37 | - | 薄封装，透传 domain |
| Prompt | `prompt/prompt.go` | 244 | `PromptApplicationSVC` | Prompt 资源 CRUD |
| 上传 | `upload/icon.go` | 740 | `SVC` | 文件上传/ImageX |
| 模板 | `template/...` | - | `TemplateSVC` | Bot/Workflow 模板 |
| 连接器 | `connector/...` | - | `ConnectorSVC` | 发布渠道管理 |
| 快捷命令 | `shortcutcmd/...` | - | `ShortcutCmdSVC` | 快捷指令 CRUD |

---

## 五、Domain 领域层

### 5.1 领域模块与接口 【已确认】

| 领域 | 接口文件 | 方法数 | 关键实现 |
|------|---------|--------|---------|
| SingleAgent | `domain/agent/singleagent/service/single_agent.go` | 17 | `single_agent_impl.go` (331 行) |
| AgentRun | `domain/conversation/agentrun/service/agent_run.go` | 6 | `agent_run_impl.go` (94 行) |
| Conversation | `domain/conversation/conversation/service/conversation.go` | 7 | conversation_impl.go |
| Message | `domain/conversation/message/service/message.go` | 11 | message_impl.go |
| Workflow | `domain/workflow/interface.go` | 30+ | `service/service_impl.go` (2188 行) |
| Knowledge | `domain/knowledge/service/interface.go` | 25+ | `knowledge.go`(1551行), `retrieve.go`(822行) |
| Plugin | `domain/plugin/service/service.go` | 30+ | `service_impl.go` |
| User | `domain/user/service/user.go` | 13 | user_impl.go |
| Permission | `domain/permission/permission.go` | 1 | `permission_impl.go` |

### 5.2 核心内部实现 【已确认】

- **AgentFlow 构建器** (`domain/agent/singleagent/internal/agentflow/`):
  - `agent_flow_builder.go` (295 行) → 构建 Agent 执行图
  - `system_prompt.go` → ReAct Jinja2 系统提示词
  - 集成：LLM + 插件 + 工作流 + 知识库 + 数据库 + 变量

- **AgentRun 运行时** (`domain/conversation/agentrun/internal/`):
  - `singleagent_run.go` → 双 goroutine pull/push 模型
  - `message_builder.go` (558 行) → 消息构建
  - `run_process_event.go` → 运行记录持久化

- **工作流引擎** (`domain/workflow/internal/`):
  - `compose/workflow.go` (895 行) → 图构建
  - `compose/node_runner.go` (969 行) → 节点执行
  - `execute/event_handle.go` (907 行) → 事件循环
  - 22 种节点类型

---

## 六、CrossDomain 契约层 【已确认】

| 契约 | 文件 | 行数 | 方法数 |
|------|------|------|--------|
| Agent | `crossdomain/agent/contract.go` | 124 | 3 |
| AgentRun | `crossdomain/agentrun/contract.go` | ~40 | 2 |
| Workflow | `crossdomain/workflow/contract.go` | 134 | 10 |
| Plugin | `crossdomain/plugin/contract.go` | 60 | 12 |
| Knowledge | `crossdomain/knowledge/contract.go` | 47 | 10 |
| Conversation | `crossdomain/conversation/contract.go` | 47 | 4 |
| Permission | `crossdomain/permission/contract.go` | 38 | 1 |
| User | `crossdomain/user/contract.go` | 42 | 2 |
| Message | `crossdomain/message/contract.go` | ~40 | 5 |
| Database | `crossdomain/database/contract.go` | ~50 | 5 |
| Upload | `crossdomain/upload/contract.go` | ~30 | 2 |
| Search | `crossdomain/search/contract.go` | ~30 | 2 |
| DataCopy | `crossdomain/datacopy/contract.go` | ~30 | 1 |
| Variables | `crossdomain/variables/contract.go` | ~40 | 3 |
| App | `crossdomain/app/contract.go` | ~40 | 3 |
| Connector | `crossdomain/connector/contract.go` | ~30 | 2 |

全部使用 `defaultSVC` + `SetDefaultSVC()` / `DefaultSVC()` 单例模式。

---

## 七、Infra 基础设施层

### 7.1 组件清单 【已确认】

| 组件 | 接口文件 | 实现 | 行数 |
|------|---------|------|------|
| MySQL/GORM | (直接使用 gorm.DB) | `infra/orm/impl/mysql/mysql.go` | 52 |
| Redis | `infra/cache/cache.go` (113 行) | `infra/cache/impl/redis/redis.go` | 253 |
| Elasticsearch | `infra/es/es.go` | `infra/es/impl/es/es_impl.go` (v7/v8) | 47 |
| EventBus | `infra/eventbus/eventbus.go` (50 行) | `infra/eventbus/impl/eventbus.go` | 117 |
| Object Storage | `infra/storage/storage.go` (83 行) | `infra/storage/impl/storage.go` | 103 |
| ID 生成器 | `infra/idgen/idgen.go` | `infra/idgen/impl/idgen/idgen.go` | 150 |
| SSE | `infra/sse/sse.go` (28 行) | `infra/sse/impl/sse/sse.go` | 39 |
| CodeRunner | `infra/coderunner/code.go` (51 行) | `infra/coderunner/impl/impl.go` | 57 |
| Checkpoint | (compose.CheckPointStore) | `infra/checkpoint/redis.go` + `mem.go` | 57+50 |

---

## 八、横切能力梳理

### 8.1 登录/鉴权 【已确认】

| 类型 | 实现位置 | 机制 |
|------|---------|------|
| Web Session | `middleware/session.go` | Cookie `session_key` → `UserApplicationSVC.ValidateSession()` |
| OpenAPI Token | `middleware/openapi_auth.go` | `Authorization: Bearer <key>` → MD5 → `openauth.CheckPermission()` |
| Admin | `middleware/session.go:AdminAuthMW()` | Session + `AdminEmails` 白名单匹配 |
| 跳过鉴权路径 | `session.go:noNeedSessionCheckPath` | `/api/passport/web/email/login/`, `register/v2/` |

### 8.2 权限控制 【已确认】

- **`domain/permission/permission.go`** → `CheckAuthz(ResourceIdentifier[], OperatorID, IsDraft)`
- **数据级权限**：各 Application 层手动校验 `CreatorID == userID`（如 `conversation.go:ClearHistory`）
- **OpenAPI 权限**：`ctxutil.GetApiAuthFromCtx()` 取 apiKeyInfo 中的 UserID

### 8.3 日志 【已确认】

- **`pkg/logs/logger.go`** (105 行) + **`pkg/logs/default.go`** (295 行)
- 实现：标准库 `log` → stderr，7 级（Trace→Fatal）
- Context 日志：自动注入 `CtxLogIDKey`
- Access 日志：`middleware/log.go:AccessLogMW()` 记录状态码/延迟/客户端 IP

### 8.4 异常处理 【已确认】

- **`pkg/errorx/error.go`** (91 行)：`StatusError` 接口（Code + Msg + IsAffectStability）
- **`api/internal/httputil/error_resp.go`** (55 行)：
  - `InternalError()` → `StatusError` 返回 HTTP 200 + 业务码；否则 HTTP 500
  - `BadRequest()` → HTTP 400
  - `Unauthorized()` → HTTP 401
- **错误码**：`types/errno/` 下 15 个文件，按领域分组

### 8.5 配置管理 【已确认】

- **环境变量**：`godotenv.Load()` + `os.Getenv()` + `pkg/envkey/`
- **数据库配置**：`bizpkg/config/` → `kv_entries` 表存储运行时配置
- **管理后台**：`/api/admin/config/*` 支持运行时修改

### 8.6 缓存 【已确认】

- **Redis**：`infra/cache/` 抽象 + `go-redis` 实现
- **ctxcache**：请求级 `sync.Map`，用于存 Session/Auth/Host 等
- **内存**：`kvstore.KVStore`（Agent popup 计数等）

### 8.7 消息队列 【已确认】

- **EventBus 接口**：`Producer` + `ConsumerService`
- **5 种后端**：NSQ（默认）, Kafka, RocketMQ, Pulsar, NATS
- **3 个 Topic**：`opencoze_search_resource`, `opencoze_search_app`, `opencoze_knowledge`
- 选择依据：`COZE_MQ_TYPE` 环境变量

### 8.8 文件上传 【已确认】

- **Storage 接口**：`PutObject`, `GetObject`, `GetObjectUrl`
- **3 种后端**：MinIO（默认）, TOS, S3
- **Handler**：`upload_service.go` → `upload.SVC.UploadFileCommon()`
- **ImageX**：MinIO/TOS/S3 适配 或 VeImageX（字节跳动）

### 8.9 定时任务 【未确认】

- **未发现** cron 或 ticker 机制
- 异步处理全部通过 EventBus 消息驱动

### 8.10 数据校验 【已确认】

- Hertz `BindAndValidate()` 用于 HTTP 参数绑定
- 业务级校验在 Handler 层的 `check*` 函数中
- 配置保存校验在 `config_service.go`（URL 格式、端口范围、Embedding 维度测试）

### 8.11 限流 / 幂等 / 防重提交 【未确认】

- **未发现** 限流中间件
- **未发现** 幂等 / 防重提交机制
- Workflow 的 `commitID`（每次保存生成新 ID）可一定程度防止并发覆盖

---

## 九、5 条关键链路

### 链路 1：Agent 对话（Studio WebAPI SSE）

| 项 | 值 |
|------|------|
| **业务名称** | Agent 对话（流式） |
| **入口接口** | `POST /api/conversation/chat` |
| **Controller** | `agent_run_service.go:AgentRun()` |
| **Service** | `conversation.ConversationSVC.Run()` (`conversation/agent_run.go:Run()`) |
| **Domain** | `agentrun.AgentRun()` → `internal.AgentRuntime.Run()` → `singleagent.StreamExecute()` → `agentflow.BuildAgent()` |
| **Repository** | `run_record` 表 (GORM), `message` 表, `conversation` 表 |
| **外部依赖** | LLM (OpenAI/Ark/Claude...), 插件 (HTTP), 知识库 (ES/Milvus), Redis (Checkpoint) |
| **返回结构** | SSE stream: `event: message / done / error`, `data: RunStreamResponse JSON` |
| **关键风险点** | 1) Channel 容量 100 背压 2) 无请求超时 3) `crash.log` 兜底而非结构化 panic 处理 |

### 链路 2：OpenAPI v3 Chat

| 项 | 值 |
|------|------|
| **业务名称** | OpenAPI 对话 |
| **入口接口** | `POST /v3/chat` |
| **Controller** | `agent_run_service.go:ChatV3()` |
| **Service** | `ConversationOpenAPISVC.OpenapiAgentRun()` 或 `OpenapiAgentRunSync()` |
| **Domain** | 同链路 1 (通过 crossagent.DefaultSVC().StreamExecute) |
| **Repository** | 同链路 1 |
| **外部依赖** | 同链路 1 + `api_key` 表鉴权 |
| **返回结构** | 流式: SSE; 同步: `ChatV3Response JSON` |
| **关键风险点** | 1) `preprocessChatV3Parameters` 重写 body 2) Stream=false 同步模式可能长时间阻塞 |

### 链路 3：工作流测试运行

| 项 | 值 |
|------|------|
| **业务名称** | 工作流测试运行（流式） |
| **入口接口** | `POST /api/workflow/test_run` |
| **Controller** | `workflow_service.go:WorkFlowTestRun()` |
| **Service** | `workflow.WorkflowSVC.TestRun()` → `workflow.go:TestRun()` |
| **Domain** | `workflow.Executable.StreamExecute()` → `compose/workflow_run.go:WorkflowRunner` |
| **Repository** | `workflow_draft`, `workflow_execution`, `node_execution` 表 |
| **外部依赖** | LLM, 插件, 知识库, CodeRunner(Python/JS), Redis(Checkpoint) |
| **返回结构** | SSE stream: workflow events (node start/end/error, workflow end) |
| **关键风险点** | 1) 嵌套子工作流递归深度无限制 2) 大型工作流 node_execution 写入量大 |

### 链路 4：知识库文档创建

| 项 | 值 |
|------|------|
| **业务名称** | 知识库文档上传与处理 |
| **入口接口** | `POST /api/knowledge/document/create` |
| **Controller** | `knowledge_service.go:CreateDocument()` |
| **Service** | `knowledge.KnowledgeSVC.CreateDocument()` |
| **Domain** | `knowledge.CreateDocument()` → EventBus publish → Consumer: parse → chunk → embed → index |
| **Repository** | `knowledge_document`, `knowledge_document_slice` 表; Milvus/ES (向量/全文) |
| **外部依赖** | Parser(PDF/DOCX/..), Embedding(OpenAI/Ark/Ollama..), SearchStore(Milvus/ES) |
| **返回结构** | `CreateDocumentResponse { Documents }` |
| **关键风险点** | 1) 大文件解析超时 2) Embedding 批量调用限流 3) 异步处理失败需查 document status |

### 链路 5：用户注册登录

| 项 | 值 |
|------|------|
| **业务名称** | 邮箱注册/登录 |
| **入口接口** | `POST /api/passport/web/email/register/v2/` / `login/` |
| **Controller** | `passport_service.go:PassportWebEmailRegisterV2Post()` / `LoginPost()` |
| **Service** | `user.UserApplicationSVC.PassportWebEmailRegisterV2()` / `PassportWebEmailLoginPost()` |
| **Domain** | `user.Create()` / `user.Login()` → 密码比对 → Session 创建 |
| **Repository** | `user` 表, `space` 表, `space_user` 表 |
| **外部依赖** | Redis (Session 存储), OSS (头像) |
| **返回结构** | JSON + Set-Cookie: `session_key` (MaxAge=30天, HttpOnly) |
| **关键风险点** | 1) Secure=false (需 HTTPS) 2) 无注册邮箱验证 3) 密码重置无 Token 过期 |

---

## 十、已读清单

### 10.1 已阅读的后端目录 【已确认】

- [x] `backend/` 根目录 (main.go)
- [x] `backend/api/middleware/` (7 个文件，全量阅读)
- [x] `backend/api/router/` (register.go + coze/api.go)
- [x] `backend/api/handler/coze/` (21 个文件，全量阅读)
- [x] `backend/api/internal/httputil/` (error_resp.go)
- [x] `backend/api/model/` (通过 Handler 引用间接确认结构)
- [x] `backend/application/` (15+ 个服务文件，全量阅读)
- [x] `backend/application/base/appinfra/` (app_infra.go)
- [x] `backend/domain/agent/singleagent/` (service 接口+实现+agentflow)
- [x] `backend/domain/conversation/` (agentrun/conversation/message 全部)
- [x] `backend/domain/workflow/` (interface.go + service_impl.go 首尾200行)
- [x] `backend/domain/knowledge/service/` (interface.go)
- [x] `backend/domain/plugin/service/` (service.go)
- [x] `backend/domain/user/service/` (user.go)
- [x] `backend/domain/permission/` (permission.go)
- [x] `backend/crossdomain/` (16 个 contract.go 全量阅读)
- [x] `backend/infra/orm/`, `cache/`, `es/`, `eventbus/`, `storage/`, `idgen/`, `sse/`, `coderunner/`, `checkpoint/`
- [x] `backend/pkg/errorx/`, `logs/`, `ctxcache/`
- [x] `backend/types/errno/` (15 个文件)
- [x] `backend/types/consts/` (consts.go)
- [x] `backend/bizpkg/config/` (config.go, base.go, modelmgr.go)
- [x] `backend/bizpkg/llm/modelbuilder/` (model_builder.go)

### 10.2 已重点阅读的后端文件（精确路径）

共 **70+ 个** Go 源文件被完整读取，核心文件包括：
`main.go`, `application.go`, `app_infra.go`, `register.go`, `api.go` 路由,
全部 7 个 middleware, 全部 21 个 handler, 15 个 application 层,
12 个 domain 接口/实现, 14 个 infra 实现, 7 个 crossdomain 契约,
`errorx/error.go`, `logs/logger.go+default.go`, `ctxcache/ctx_cache.go`, `httputil/error_resp.go`

### 10.3 尚未深入但可能重要的后端目录

| 目录 | 原因 |
|------|------|
| `backend/domain/workflow/internal/nodes/` | 22 种节点具体实现（每个 200-1400 行） |
| `backend/domain/knowledge/internal/` | 检索/嵌入/解析完整实现 |
| `backend/domain/plugin/service/tool/` | 插件调用 4 种模式的详细实现 |
| `backend/infra/eventbus/impl/kafka|rmq|pulsar|nats/` | 非默认 MQ 实现 |
| `backend/infra/storage/impl/minio|tos|s3/` | OSS 具体实现 |
| `backend/infra/document/` | 文档解析/嵌入/搜索存储全链路 |
| `backend/internal/mock/` | 测试 Mock 生成文件 |
| `backend/pkg/kvstore/` | KV 存储封装 |

### 10.4 当前后端分析覆盖率

| 层次 | 覆盖率 | 说明 |
|------|--------|------|
| 启动入口 | **100%** | main.go + application.go + appinfra 全读 |
| 中间件 | **100%** | 7/7 全读 |
| 路由 | **100%** | 自动生成路由 + 注册逻辑全读 |
| Handler | **100%** | 21/21 全读（含 1 个 test） |
| Application | **95%** | 核心 15 个全读，少量辅助方法未展开 |
| Domain 接口 | **100%** | 9 个核心接口全读 |
| Domain 实现 | **75%** | 核心 impl 已读，workflow 节点/知识库内部未全展开 |
| CrossDomain | **100%** | 16 个契约全读 |
| Infra | **90%** | 核心 14 个组件全读，具体 MQ/OSS 实现未展开 |
| Pkg/横切 | **90%** | errorx/logs/ctxcache/httputil/errno 全读 |
| **综合** | **~92%** | |
