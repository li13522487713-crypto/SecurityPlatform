# Coze Studio 交互链路深度分析（第 4 批）

> 基于核心源码全量阅读
> 分析日期：2026-04-09
> 说明：本文是上游 Coze Studio 交互链分析快照。文中旧 `/api/workflow_api/*`、`/api/playground_api/*` 路由仅作历史参考，不代表当前 Atlas 运行期入口。

---

## 一、Agent 聊天完整链路

### 1.1 请求入口

```
POST /api/conversation/chat → handler/coze/agent_run_service.go :: AgentRun()
POST /v3/chat              → handler/coze/agent_run_service.go :: ChatV3()
```

### 1.2 Handler 层（`agent_run_service.go`）

| 步骤 | 代码位置 | 说明 |
|------|----------|------|
| 1 | `AgentRun` | BindAndValidate → checkParams（BotID、Scene 必填） |
| 2 | `sseImpl.NewSSESender(sse.NewStream(c))` | 创建 SSE 流式发送器 |
| 3 | `conversation.ConversationSVC.Run(ctx, sseSender, &req)` | 委托 Application 层 |
| 4 | 错误时发送 `RunEventError` SSE 事件 | JSON Marshal → `sseSender.Send` |

**ChatV3（OpenAPI 入口）**：
- 支持 `stream=true`（SSE）和 `stream=false`（同步 JSON）两种模式
- `preprocessChatV3Parameters` 将 `parameters` 从 JSON Object 转为 String
- 非流式调用 `ConversationOpenAPISVC.OpenapiAgentRunSync`

### 1.3 Application 层（`application/conversation/agent_run.go`）

```
ConversationApplicationService.Run()
  ├── checkAgent()          → SingleAgentDomainSVC.GetSingleAgent()
  ├── checkConversation()   → ConversationDomainSVC.GetCurrentConversation / Create
  ├── buildAgentRunRequest() → 构建 AgentRunMeta
  │    ├── buildMultiContent()   → 支持 text/image/file/audio/video/mix
  │    ├── buildTools()          → 快捷命令工具转换
  │    └── buildDisplayContent() → 非文本内容保留原始 Query
  ├── AgentRunDomainSVC.AgentRun(ctx, agentRunMeta) → 返回 StreamReader
  └── pullStream()          → 从 StreamReader 拉取事件，通过 SSE 推送到前端
```

**pullStream 事件处理**：

| 事件类型 | 处理逻辑 |
|---------|---------|
| `RunEventCreated/InProgress/Completed` | 状态变更（不发送给前端） |
| `RunEventError` | 构建错误消息，发送 `RunEventMessage` |
| `RunEventStreamDone` | 发送 `RunEventDone` 事件 |
| `RunEventAck` | 首条消息确认（包含 ConversationID），发送给前端 |
| `RunEventMessageDelta/Completed` | 流式消息块，实时发送给前端 |

### 1.4 Domain 层 — AgentRun（`domain/conversation/agentrun/internal/`）

核心文件：**12 个文件**

| 文件 | 行数 | 职责 |
|------|------|------|
| `run.go` | 229 | `AgentRuntime` 结构体 + `Run()` 编排入口 |
| `singleagent_run.go` | 302 | `AgentStreamExecute()` — 单 Agent 流式执行 |
| `chatflow_run.go` | 252 | `ChatflowRun()` — ChatFlow 模式（调用工作流） |
| `message_builder.go` | 558 | 消息构建器（Ack、Answer、错误消息、历史对话） |
| `message_event.go` | 447 | 消息事件发送器（SendMsgEvent、各种 handler） |
| `run_process_event.go` | 123 | 运行记录处理（创建/更新） |
| `agent_info.go` | 52 | Agent 信息获取 |
| `dal/dao.go` | 209 | 数据访问层（run_record 表 CRUD） |

#### 1.4.1 SingleAgent 执行流

```
AgentStreamExecute(ctx, imagex)
  ├── 构建 crossagent.AgentRuntime
  │    ├── AgentVersion, SpaceID, AgentID, IsDraft
  │    ├── Input: transMessageToSchemaMessage(当前输入)
  │    ├── HistoryMsg: transMessageToSchemaMessage(历史对话)
  │    └── ResumeInfo: parseResumeInfo(中断恢复)
  ├── crossagent.DefaultSVC().StreamExecute(ctx, ar)  → 返回 StreamReader
  ├── goroutine 1: pull() — 从 Agent StreamReader 读取事件 → mainChan
  └── goroutine 2: push() — 从 mainChan 消费事件，处理并发送

push() 事件处理:
  ├── MessageTypeFunctionCall → handlerFunctionCall（工具调用中）
  ├── MessageTypeToolResponse → handlerToolResponse（工具返回）
  ├── MessageTypeKnowledge    → handlerKnowledge（知识检索）
  ├── MessageTypeToolMidAnswer → 工具中间回答（流式）
  ├── MessageTypeToolAsAnswer  → 工具作为最终回答（流式）
  ├── MessageTypeAnswer        → 模型回答（流式，支持 Reasoning Content）
  ├── MessageTypeFlowUp        → 推荐问题（Suggest）
  └── MessageTypeInterrupt     → 中断事件（用户确认）
```

#### 1.4.2 ChatFlow 执行流

```
ChatflowRun(ctx, imagex)
  ├── 获取 WorkflowID（从 Agent.LayoutInfo.WorkflowId）
  ├── 构建 ExecuteConfig（ID、Operator、Mode=Release、BizType=Agent）
  ├── 如果有 ResumeInfo → crossworkflow.DefaultSVC().StreamResume()
  ├── 否则              → crossworkflow.DefaultSVC().StreamExecute()
  └── pullWfStream() — 消费工作流流式事件
       ├── StateMessage → 处理状态、Usage、中断事件
       └── DataMessage(Answer) → 构建消息块 → 发送给前端
```

### 1.5 Domain 层 — AgentFlow Builder（`singleagent/internal/agentflow/`）

核心文件：**18 个文件**

| 文件 | 行数 | 职责 |
|------|------|------|
| `agent_flow_builder.go` | 295 | **Agent 执行图构建入口** |
| `agent_flow_runner.go` | ~200 | Agent 执行运行器 |
| `callback_reply_chunk.go` | ~150 | 回调处理（流式回复块） |
| `system_prompt.go` | 66 | **ReAct 系统提示词模板**（Jinja2 格式） |
| `suggest_prompt.go` | 43 | 推荐问题提示词模板 |
| `node_tool_plugin.go` | ~150 | 插件工具节点构建 |
| `node_tool_workflow.go` | 65 | 工作流工具节点构建 |
| `node_tool_knowledge.go` | ~100 | 知识库工具节点构建 |
| `node_tool_variables.go` | 131 | 变量工具节点（setKeywordMemory） |
| `node_tool_database.go` | ~100 | 数据库工具节点 |
| `node_tool_pre_retriever.go` | 139 | 预检索工具（快捷命令预调用） |

**Agent 执行图核心架构**：

```
BuildAgent(ctx, conf)
  ├── 1. 构建 LLM ChatModel（通过 modelbuilder.BuildModelBySettings）
  ├── 2. 构建工具集：
  │    ├── Plugin Tools（via crossplugin）
  │    ├── Workflow Tools（via crossworkflow.WorkflowAsModelTool）
  │    ├── Knowledge Retrieve（via crossknowledge）
  │    ├── Database Tools（via crossdatabase）
  │    └── Variable Tools（setKeywordMemory）
  ├── 3. 构建系统提示词（REACT_SYSTEM_PROMPT_JINJA2 模板填充）
  │    ├── agent_name, persona, time
  │    ├── knowledge（检索结果）
  │    ├── memory_variables（变量值）
  │    └── tools_pre_retriever（预检索结果）
  ├── 4. 组装 eino compose.Graph（Agent 执行图）
  │    ├── ChatModel 节点
  │    ├── Tool 节点（支持中断/恢复）
  │    └── Knowledge Retrieval 节点
  └── 5. 返回 AgentFlowRunner（可 StreamExecute）
```

**系统提示词（system_prompt.go）**关键结构：
- 安全约束（禁止暴力/仇恨/成人内容）
- 人设（Persona）
- 变量（Memory Variables）
- 知识库（Knowledge）— 支持图片标签解析
- 预检索结果（Pre toolCall）

---

## 二、工作流执行引擎

### 2.1 核心架构

```
backend/domain/workflow/internal/
  ├── compose/          → 工作流编排层
  │    ├── workflow.go        (895 行) — Workflow 结构体 + 图构建
  │    ├── workflow_run.go    (314 行) — WorkflowRunner + Prepare
  │    ├── node_builder.go    (120 行) — 节点工厂
  │    ├── node_runner.go     (969 行) — 节点运行器（Lambda 包装）
  │    ├── state.go           (729 行) — 状态管理（GenState）
  │    ├── stream.go          (177 行) — 流式字段源计算
  │    ├── field_fill.go      (319 行) — 字段填充
  │    └── workflow_from_node.go (86 行) — 单节点调试构建
  ├── execute/          → 执行上下文层
  │    ├── context.go         (435 行) — 执行上下文（Root/Sub/Node 三级）
  │    ├── event_handle.go    (907 行) — 事件处理（状态持久化、中断）
  │    ├── callback.go        — 回调注册
  │    ├── stream_container.go — 流容器
  │    └── collect_token.go   — Token 统计
  └── nodes/            → 22 种节点类型
```

### 2.2 节点类型全表（22 种）

| 目录名 | 节点类型 | 说明 |
|--------|---------|------|
| `llm` | LLM | 大语言模型调用（1352 行，最复杂） |
| `code` | Code | Python/JS 代码执行 |
| `plugin` | Plugin | 插件工具调用 |
| `knowledge` | Knowledge | 知识库检索 |
| `database` | Database | 数据库查询 |
| `httprequester` | HTTPRequester | HTTP 请求 |
| `qa` | QuestionAnswer | 问答交互（支持中断） |
| `intentdetector` | IntentDetector | 意图识别 |
| `selector` | Selector | 条件分支 |
| `json` | JSON | JSON 处理 |
| `textprocessor` | TextProcessor | 文本处理 |
| `variableaggregator` | VariableAggregator | 变量聚合 |
| `variableassigner` | VariableAssigner | 变量赋值 |
| `entry` | Entry | 入口节点 |
| `exit` | Exit | 出口节点 |
| `emitter` | Emitter | 输出发射器（OutputEmitter） |
| `receiver` | Receiver | 输入接收器（InputReceiver） |
| `subworkflow` | SubWorkflow | 子工作流 |
| `batch` | Batch | 批量执行（复合节点） |
| `loop` | Loop | 循环（含 break/continue） |
| `conversation` | Conversation | 对话节点 |

### 2.3 WorkflowRunner 执行流程

```
WorkflowRunner.Prepare(ctx)
  ├── 1. 生成 ExecuteID（或从 ResumeRequest 恢复）
  ├── 2. 创建 eventChan（事件管道）
  ├── 3. 如果是恢复执行：
  │    ├── 获取 InterruptEvent
  │    ├── GenStateModifierByEventType（状态恢复）
  │    ├── 处理嵌套路径（复合节点/子工作流）
  │    └── TryLockWorkflowExecution
  ├── 4. 如果是新执行：
  │    └── CreateWorkflowExecution（持久化）
  ├── 5. 启动超时控制（前台/后台不同超时）
  ├── 6. 启动事件处理协程：
  │    └── execute.HandleExecuteEvent(ctx, executeID, eventChan, ...)
  └── 7. 返回 (cancelCtx, executeID, composeOpts, lastEventChan)

实际执行：
  workflow.Runner.Stream(cancelCtx, input, composeOpts...)
    → eino compose.Workflow 驱动节点执行
    → 每个节点通过 NodeRunner 包装
    → 事件通过 eventChan 发送到 HandleExecuteEvent
    → HandleExecuteEvent 写入 StreamWriter → 前端
```

### 2.4 执行上下文体系（`execute/context.go`）

三级上下文：

```
Context
  ├── RootCtx              → 根工作流信息
  │    ├── RootWorkflowBasic
  │    ├── RootExecuteID
  │    ├── ResumeEvent
  │    └── ExeCfg
  ├── SubWorkflowCtx       → 子工作流信息
  │    ├── SubWorkflowBasic
  │    └── SubExecuteID
  ├── NodeCtx              → 当前节点信息
  │    ├── NodeKey, NodeExecuteID
  │    ├── NodeName, NodeType
  │    ├── NodePath（嵌套路径）
  │    ├── TerminatePlan
  │    └── ResumingEvent
  ├── BatchInfo            → 批量执行信息
  ├── TokenCollector       → Token 统计（支持层级汇总）
  ├── AppVarStore          → 应用变量存储（线程安全）
  └── CheckPointID         → 检查点 ID（支持中断恢复）
```

### 2.5 事件处理系统（`execute/event_handle.go`）

`HandleExecuteEvent` 是工作流执行的核心事件循环：

| 事件类型 | 处理 |
|---------|------|
| `NodeStart` | 记录节点开始时间 |
| `NodeEnd` | 发送节点执行结果 → StreamWriter |
| `NodeError` | 记录错误、判断是否终止 |
| `InterruptEvent` | 保存中断状态、通知前端 |
| `WorkflowEnd` | 汇总结果、更新 WorkflowExecution 状态 |
| `CancelEvent` | 取消执行 |

---

## 三、知识库检索链路

### 3.1 核心文件

| 文件 | 行数 | 职责 |
|------|------|------|
| `service/interface.go` | 378 | 知识库领域接口（65+ 个请求/响应类型） |
| `service/retrieve.go` | 822 | **检索核心逻辑** |
| `service/knowledge.go` | 1551 | 知识库 CRUD 实现 |
| `service/event_handle.go` | 929 | 事件驱动的文档处理 |
| `service/sheet.go` | — | 表格知识库特殊处理 |
| `service/rdb.go` | — | 关系型数据库操作 |

### 3.2 检索流程（`retrieve.go`）

```
Retrieve(ctx, request)
  ├── 1. 构建 RetrieveContext
  │    ├── OriginQuery（原始查询）
  │    ├── KnowledgeIDs（知识库 ID 集合）
  │    ├── ChatHistory（对话历史）
  │    └── Strategy（检索策略）
  ├── 2. 查询关联文档信息
  │    └── Documents = ListDocumentsByKnowledgeIDs
  ├── 3. Query 改写（如果需要）
  │    └── MessagesToQuery（对话历史 → 独立查询）
  ├── 4. 向量检索
  │    ├── GetSearchStore(collectionName)
  │    └── SearchStore.Retrieve(ctx, query, options...)
  ├── 5. Rerank（如果配置）
  │    └── Reranker.Rerank(ctx, query, documents)
  └── 6. 返回 RetrieveResponse
```

### 3.3 基础设施层

| 组件 | 接口文件 | 实现 |
|------|---------|------|
| **SearchStore** | `infra/document/searchstore/searchstore.go` | `indexer.Indexer` + `retriever.Retriever` + `Delete` |
| **Manager** | `infra/document/searchstore/manager.go` | `Create/Drop/GetType/GetSearchStore` |
| **实现** | Milvus、Elasticsearch、VikingDB、OceanBase | 4 种向量/文本搜索后端 |
| **Embedder** | `infra/embedding/embedding.go` | `Embedder` 接口 + `EmbedStringsHybrid` |
| **Embedder 实现** | Ark、OpenAI、Ollama、Gemini、HTTP | 5 种嵌入模型 |
| **Parser** | `infra/document/parser/parser.go` | `parser.Parser`（eino 标准接口） |
| **Parser Manager** | `infra/document/parser/manager.go` | `GetParser/IsAutoAnnotationSupported` |
| **Parser 实现** | Builtin（14 种格式）、PPStructure（OCR） | PDF/TXT/DOC/DOCX/MD/CSV/XLSX/JSON/JPG/PNG |

### 3.4 事件驱动处理（`event_handle.go`）

通过 EventBus（NSQ）异步处理：
- 文档上传 → EventBus 发送事件 → Consumer 消费
- Consumer 执行：解析 → 分片 → 向量化 → 存入 SearchStore
- 支持重新分片（Resegment）

---

## 四、插件工具调用链路

### 4.1 调用方式分发

```
PluginService.ExecuteTool(ctx, req)
  → exec_tool.go 中实现
  → 根据插件类型选择 Invocation 实现
```

### 4.2 四种调用实现

| 文件 | 类型 | 说明 |
|------|------|------|
| `tool/invocation.go` | 接口定义 | `Invocation.Do(ctx, args) → (request, resp, err)` |
| `tool/invocation_http.go` | HTTP 调用 | 387 行 — 构建 HTTP 请求、注入认证、发送 |
| `tool/invocation_saas.go` | SaaS 调用 | 200 行 — Coze SaaS API 代理调用 |
| `tool/invocation_custom_call.go` | 自定义调用 | 50 行 — 注册制，按 ToolID 分发 |
| `tool/invocation_mcp.go` | MCP 调用 | 34 行 — 仅调试场景（TODO） |
| `tool/invocation_args.go` | 参数构建 | 515 行 — **最核心** |

### 4.3 参数构建流程（`invocation_args.go`）

```
NewInvocationArgs(ctx, builder)
  ├── 1. json2Map(ArgsInJson) → 解析 LLM 输出的 JSON 参数
  ├── 2. groupedKeysByLocation() → 按 OpenAPI3 Schema 分组
  │    ├── Header / Path / Query / Cookie / Body
  │    └── 识别文件类型字段（x-assist-type）
  ├── 3. groupedRequestArgs() → 将参数分配到各组
  ├── 4. setCommonParams() → 注入插件公共参数
  └── 5. setDefaultValues() → 注入默认值
       └── 支持 x-variable-ref（变量引用 → crossvariables）
```

### 4.4 HTTP 调用流程（`invocation_http.go`）

```
httpCallImpl.Do(ctx, args)
  ├── 1. buildHTTPRequest()
  │    ├── buildHTTPRequestURL() — Path 参数替换 + Query 参数
  │    ├── buildRequestBody() — Body 构建（支持 content-type）
  │    └── buildHTTPRequestHeader() — 注入 LogID、Connector 信息
  ├── 2. injectAuthInfo()
  │    ├── None → 跳过
  │    ├── Service API Token → Header/Query 注入
  │    └── OAuth → AccessToken 注入（支持中断授权流程）
  ├── 3. resty.Send() — 发送请求
  └── 4. 返回 (requestStr, responseStr)
```

**特殊机制 — OAuth 中断**：
当插件需要 OAuth 授权但用户未授权时，触发 `compose.NewInterruptAndRerunErr`，工作流中断，前端弹出授权页面。

### 4.5 SaaS 调用流程（`invocation_saas.go`）

- 目标：通过 Coze SaaS API 代理执行第三方插件
- 请求格式：`{ "tool_name": "xxx", "arguments": {...} }`
- 认证：使用 `COZE_SAAS_API_KEY` 注入 Bearer Token
- 响应：解析 `{ code, msg, data: { result } }`

---

## 五、基础设施层

### 5.1 SSE 实现

```
infra/sse/sse.go         → SSender 接口
infra/sse/impl/sse/sse.go → SSenderImpl
  └── Send(ctx, event) → ss.Publish(event)
```

基于 `hertz-contrib/sse` 库，`sse.Stream` 绑定到 Hertz `RequestContext`：
- `sse.NewStream(c)` 创建流
- `Stream.Publish(event)` 发送事件
- 响应头自动设置 `text/event-stream`

### 5.2 EventBus（消息队列）

```
infra/eventbus/eventbus.go → Producer + ConsumerService 接口
infra/eventbus/impl/eventbus.go → 路由层（按 COZE_MQ_TYPE 环境变量选择）
```

| MQ 类型 | 实现文件 | 说明 |
|---------|---------|------|
| `nsq` | `impl/nsq/producer.go` + `consumer.go` | **默认**（Docker Compose 配置） |
| `kafka` | `impl/kafka/` | Kafka 支持 |
| `rmq` | `impl/rmq/` | RocketMQ 支持 |
| `pulsar` | `impl/pulsar/` | Pulsar 支持 |
| `nats` | `impl/nats/` | NATS 支持（支持 JetStream） |

**三个 Topic**：
- `opencoze_search_resource` — 资源搜索事件
- `opencoze_search_app` — 应用搜索事件
- `opencoze_knowledge` — 知识库文档处理事件

### 5.3 CodeRunner（代码执行器）

```
infra/coderunner/code.go → Runner 接口
  ├── Run(ctx, RunRequest) → RunResponse
  └── 支持 Python / JavaScript
```

| 模式 | 实现 | 说明 |
|------|------|------|
| **Sandbox** | `impl/sandbox/runner.go` | 通过 Python `sandbox.py` 脚本执行，支持权限控制 |
| **Direct** | `impl/direct/runner.go` | 直接 `exec.Command` 执行 Python |

Sandbox 配置项：AllowEnv、AllowRead、AllowWrite、AllowNet、AllowRun、AllowFFI、NodeModulesDir、TimeoutSeconds、MemoryLimitMB

### 5.4 LLM ModelBuilder

```
bizpkg/llm/modelbuilder/model_builder.go → Service 接口
  ├── Build(ctx, LLMParams) → ToolCallingChatModel
  └── 7 种 Provider：
       ├── Ark（火山引擎）
       ├── OpenAI
       ├── Claude
       ├── DeepSeek
       ├── Gemini
       ├── Ollama
       └── Qwen（通义千问）
```

构建入口：
- `BuildModelBySettings(ctx, appSettings)` — 从 Agent 配置构建
- `BuildModelByID(ctx, modelID, params)` — 按模型 ID 构建
- `BuildModelWithConf(ctx, model)` — 从配置构建

---

## 六、端到端链路图

### 6.1 Agent 聊天（Web 端）

```
[浏览器] fetchStream(SSE)
    ↓
[Nginx] /api/conversation/chat → proxy_pass http://coze-server:8888
    ↓
[Hertz] RequestInspectorMW → SessionAuthMW → LogMW → I18nMW → CtxCacheMW
    ↓
[Handler] AgentRun → BindAndValidate → checkParams
    ↓
[Application] ConversationSVC.Run
    ├── checkAgent → SingleAgentDomainSVC.GetSingleAgent
    ├── checkConversation → 创建或获取对话
    ├── buildAgentRunRequest → 构建执行请求
    ├── AgentRunDomainSVC.AgentRun
    │    ├── AgentRuntime.Run
    │    │    ├── 创建 RunRecord（DB 持久化）
    │    │    ├── 构建 AckMessage → 发送给前端
    │    │    └── AgentStreamExecute / ChatflowRun
    │    │         ├── [SingleAgent 模式]
    │    │         │    ├── crossagent.StreamExecute
    │    │         │    │    ├── BuildAgent（构建执行图）
    │    │         │    │    │    ├── LLM Model（via ModelBuilder）
    │    │         │    │    │    ├── Plugin Tools（via PluginService）
    │    │         │    │    │    ├── Workflow Tools
    │    │         │    │    │    ├── Knowledge Retrieve
    │    │         │    │    │    └── Database Tools
    │    │         │    │    └── AgentFlowRunner.StreamExecute
    │    │         │    │         └── eino compose 驱动执行
    │    │         │    ├── pull（Agent → mainChan）
    │    │         │    └── push（mainChan → SSE）
    │    │         └── [ChatFlow 模式]
    │    │              ├── crossworkflow.StreamExecute
    │    │              └── pullWfStream → SSE
    │    └── 返回 StreamReader
    └── pullStream → SSE 事件推送
         ↓
[浏览器] onmessage 回调处理
```

### 6.2 工作流测试运行

```
[浏览器] fetchStream(SSE)
    ↓
[Hertz] POST /api/workflow_api/test_run
    ↓
[Handler] WorkflowTestRun
    ↓
[Application] WorkflowApplicationSVC.TestRun
    ├── 加载 WorkflowSchema
    ├── 构建 Workflow（compose.NewWorkflow）
    │    ├── 遍历节点 → node_builder.New → 创建节点
    │    ├── 连接节点（Connections）
    │    └── Compile（编译执行图）
    ├── WorkflowRunner.Prepare
    │    ├── 生成 ExecuteID
    │    ├── 创建 WorkflowExecution（DB）
    │    └── 启动事件处理协程
    └── Runner.Stream(ctx, input, opts...)
         ├── eino compose 驱动节点按 DAG 执行
         ├── 每个节点 → NodeRunner
         │    ├── PrepareNodeExeCtx
         │    ├── 节点具体 Build → 执行
         │    ├── 发送事件到 eventChan
         │    └── 更新 Token 统计
         └── HandleExecuteEvent
              ├── NodeEnd → StreamWriter → 前端
              ├── InterruptEvent → 中断 → 前端弹窗
              └── WorkflowEnd → 更新状态 → 完成
```

---

## 七、已读清单更新

### 第 4 批新增已读文件

| 区域 | 文件数 | 关键文件 |
|------|--------|---------|
| AgentRun internal | 12 | `singleagent_run.go`, `chatflow_run.go`, `message_builder.go`, `run.go` |
| AgentFlow builder | 18 | `agent_flow_builder.go`, `system_prompt.go`, `node_tool_*.go` |
| Workflow compose | 8 | `workflow.go`, `workflow_run.go`, `node_builder.go`, `node_runner.go` |
| Workflow execute | 6 | `context.go`, `event_handle.go`, `callback.go` |
| Workflow nodes | 已列全 22 种 | `llm.go`（1352行） |
| Knowledge service | 14 | `retrieve.go`(822行), `event_handle.go`(929行), `knowledge.go`(1551行) |
| Plugin service | 20 | `invocation_http.go`, `invocation_args.go`, `invocation_saas.go` |
| Infra SSE | 2 | `sse.go`, `impl/sse/sse.go` |
| Infra EventBus | 17 | `eventbus.go`, `impl/eventbus.go`, `nsq/*.go` |
| Infra CodeRunner | 4 | `code.go`, `impl/direct/runner.go`, `impl/sandbox/runner.go` |
| Infra SearchStore | 22 | `searchstore.go`, `manager.go`, 4 种实现 |
| Infra Embedding | 9 | `embedding.go`, 5 种实现 |
| Infra Parser | 28 | `parser.go`, `manager.go`, builtin (14 格式) |
| LLM ModelBuilder | 1 | `model_builder.go` (7 种 Provider) |
| Agent Entity | 1 | `single_agent.go` |

**第 4 批总计新增约 160+ 个文件的阅读**

### 累计覆盖率

| 区块 | 覆盖率 | 说明 |
|------|--------|------|
| 启动与入口层 | 100% | ✅ |
| 配置层 | 95% | ✅ |
| 路由/控制层 | 100% | ✅ |
| 业务服务层 | 90% | ✅ 核心链路全部已读 |
| 数据访问层 | 60% | ⚠️ 具体 DAL 实现部分已读 |
| 前端页面层 | 80% | ✅ 核心页面已读 |
| 前端组件层 | 30% | ⚠️ 待第 5 批深入 |
| 状态管理层 | 70% | ✅ 入口已读，内部逻辑部分 |
| API/网络请求层 | 75% | ✅ |
| 公共工具层 | 60% | ⚠️ |
| 权限/中间件层 | 100% | ✅ |
| 类型定义/Schema | 50% | ⚠️ 按需查阅 |

---

## 八、风险点与注意事项

### 8.1 性能风险
- `workflow.go`（4333 行）和 `knowledge.go`（1551 行）为项目最大文件，复杂度高
- Agent 执行使用双 goroutine pull/push 模型，需注意 channel 阻塞
- 工作流支持嵌套子工作流，需警惕递归深度

### 8.2 安全风险
- `direct/runner.go` 中的 `exec.Command(python3, "-c", code)` 标记了 `ignore_security_alert RCE`
- OAuth 中断恢复流程需确保 state 参数防 CSRF
- Session Cookie 未设置 Secure=true（`SessionAuthMW` 中 `false, true` → HttpOnly=true, Secure=false）

### 8.3 架构风险
- MCP 调用（`invocation_mcp.go`）仅返回 "not implemented"，功能未完成
- JavaScript CodeRunner 在 Sandbox 模式下返回 "js not supported yet"
- `SSender` 接口和 `SSenderImpl` 签名不一致（接口多一个 `*sse.Stream` 参数）

### 8.4 可扩展性
- EventBus 支持 5 种 MQ 后端（NSQ/Kafka/RocketMQ/Pulsar/NATS），通过环境变量切换
- SearchStore 支持 4 种后端（Milvus/ES/VikingDB/OceanBase），通过配置切换
- Embedding 支持 5 种（Ark/OpenAI/Ollama/Gemini/HTTP），通过模型配置切换
- LLM 支持 7 种 Provider，通过 `ModelClass` 映射
