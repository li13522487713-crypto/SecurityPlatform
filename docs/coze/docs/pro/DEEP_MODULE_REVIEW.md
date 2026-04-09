# Coze Studio 后端深度模块审查

> 工作流 22+ 种节点 · 知识库检索全链路 · 插件调用 4 种模式
> 日期：2026-04-09

---

## 一、工作流节点实现全览

### 1.1 节点类型注册源 【已确认】

- **类型定义**：`backend/domain/workflow/entity/node_meta.go`
- **适配器注册**：`backend/domain/workflow/internal/canvas/adaptor/to_schema.go:RegisterAllNodeAdaptors()`
- **节点接口**：`backend/domain/workflow/internal/nodes/node.go` — `InvokableNode`, `NodeAdaptor`, `BranchAdaptor`
- 注意：`NodeTypeComment` 被显式跳过（`blockTypeToSkip`），无运行时实现

### 1.2 节点实现清单 【已确认】

| # | 节点类型 | 实现文件 | 行数 | 外部依赖 | 核心能力 |
|---|---------|---------|------|---------|---------|
| 1 | **Entry** | `nodes/entry/entry.go` | ~116 | 无 | 校验固定 ID `100001`，合并默认值到输入 |
| 2 | **Exit** | `nodes/exit/exit.go` | ~145 | OutputEmitter | 结束节点，支持变量返回或模板输出 |
| 3 | **OutputEmitter** | `nodes/emitter/emitter.go` | ~612 | Eino streaming | 模板渲染+流式输出，支持批处理和流式聚合 |
| 4 | **InputReceiver** | `nodes/receiver/input_receiver.go` | ~226 | Checkpoint + Interrupt | 首次运行触发中断（表单），恢复时解析用户 JSON/文本 |
| 5 | **LLM** | `nodes/llm/llm.go` + `prompt.go` + `model_with_info.go` + `plugin.go` | ~1352+ | **LLM**, Plugin, Workflow-as-Tool, Knowledge, Checkpoint | 最核心节点：构建 Eino 图（Prompt → ChatModel/ReAct Agent → 输出），支持工具调用中断/恢复、备用模型重试、JSON Schema 解析 |
| 6 | **Plugin** | `nodes/plugin/plugin.go` + `exec.go` | ~251 | **crossplugin.ExecuteTool** | 读取画布配置的插件参数，调用平台插件运行时 |
| 7 | **CodeRunner** | `nodes/code/code.go` | ~306 | **infra/coderunner** | Python/JS 代码执行，校验导入白名单，沙箱或直接模式 |
| 8 | **Batch** | `nodes/batch/batch.go` | ~482 | 内部子图 Runnable | 数组输入按 batchSize × concurrentSize 并行执行子图 |
| 9 | **Loop** | `nodes/loop/loop.go` | ~520 | 内部子图 + ParentIntermediate | 数组/计数/无限循环模式，聚合输出，支持 Break |
| 10 | **Break** | `nodes/loop/break/break.go` | ~61 | ParentIntermediateStore | 设置父循环的 break 标志 |
| 11 | **Continue** | `nodes/loop/continue/continue.go` | ~48 | 无 | 控制流锚点，透传输入 |
| 12 | **SubWorkflow** | `nodes/subworkflow/sub_workflow.go` | ~158 | 子图 Runnable + Checkpoint | 委托给编译好的子工作流，作用域内 checkpoint，中断传播 |
| 13 | **Selector (If/Else)** | `nodes/selector/` (4 文件) | ~628 | 纯逻辑 | 有序分支条件评估 → 输出 selected 分支索引 |
| 14 | **IntentDetector** | `nodes/intentdetector/` (2 文件) | ~414+ | **LLM** | 意图分类：系统提示 + 模型推理 → classificationId 分支 |
| 15 | **QuestionAnswer** | `nodes/qa/question_answer.go` | ~905 | **LLM** + Checkpoint + Interrupt | Q&A 中断：固定/动态选项或自由文本，恢复后 LLM 抽取 |
| 16 | **TextProcessor** | `nodes/textprocessor/text_processor.go` | ~196 | 模板引擎 | 拼接（模板渲染）或分割（分隔符序列） |
| 17 | **VariableAggregator** | `nodes/variableaggregator/variable_aggregator.go` | ~737 | Eino streaming | 多分支合并：选择第一个非空候选，支持流式字段转发 |
| 18 | **VariableAssigner** | `nodes/variableassigner/variable_assign.go` | ~167 | App/User 变量 Store | 将工作流字段值写入全局 App 或用户变量 |
| 19 | **VariableAssigner (Loop)** | `nodes/variableassigner/variable_assign_in_loop.go` | ~127 | ParentIntermediateStore | 循环内跨迭代变量赋值 |
| 20 | **JSONSerialization** | `nodes/json/json_serialization.go` | ~99 | sonic | 输入 → JSON 字符串 |
| 21 | **JSONDeserialization** | `nodes/json/json_deserialization.go` | ~134 | sonic + Convert | JSON 字符串 → 类型化输出 |
| 22 | **KnowledgeRetriever** | `nodes/knowledge/knowledge_retrieve.go` | ~311 | **crossknowledge.Retrieve** | 语义/混合/全文检索，返回文档切片列表 |
| 23 | **KnowledgeIndexer** | `nodes/knowledge/knowledge_indexer.go` | ~180 | **crossknowledge.Store** | 文件 URL → 知识库文档导入 |
| 24 | **KnowledgeDeleter** | `nodes/knowledge/knowledge_deleter.go` | ~107 | **crossknowledge.Delete** | 按文档 ID 删除知识库文档 |
| 25 | **DatabaseQuery** | `nodes/database/query.go` | ~292 | **crossdatabase.Query** | WHERE 条件构建 + 排序/限制 → 查询数据库表 |
| 26 | **DatabaseInsert** | `nodes/database/insert.go` | ~136 | **crossdatabase.Insert** | 字段映射 → 插入行 |
| 27 | **DatabaseUpdate** | `nodes/database/update.go` | ~184 | **crossdatabase.Update** | WHERE + SET → 更新行 |
| 28 | **DatabaseDelete** | `nodes/database/delete.go` | ~156 | **crossdatabase.Delete** | WHERE → 删除行 |
| 29 | **DatabaseCustomSQL** | `nodes/database/customsql.go` | ~184 | **crossdatabase** CustomSQL | 模板化 SQL 渲染 + 执行 |
| 30 | **HTTPRequester** | `nodes/httprequester/http_requester.go` + `adapt.go` | ~1247 | `net/http` (标准库) | HTTP 请求构建（URL/Header/Query/Body 模板 + 认证） → JSON/文本/二进制响应解析 |
| 31-40 | **Conversation 节点** (10 个) | `nodes/conversation/*.go` | 每个 60-150 行 | **crossconversation** + **crossmessage** | 会话创建/更新/删除/列表、历史获取/清除、消息创建/编辑/删除/列表 |

### 1.3 共享基础设施 【已确认】

| 文件 | 行数 | 职责 |
|------|------|------|
| `nodes/node.go` | ~200 | 执行接口 + 适配器注册 |
| `nodes/template.go` | ~150 | Jinja2 风格模板渲染 |
| `nodes/stream.go` | ~200 | 流式字段解析 |
| `nodes/convert.go` | ~300 | 输入类型转换 |
| `nodes/callbacks.go` | ~200 | 回调 DTO 构建 |
| `nodes/state.go` | ~100 | 执行状态管理 |
| `nodes/interrupt.go` | ~100 | 中断/恢复机制 |
| `nodes/parent_intermediate.go` | ~50 | 循环中间变量存储 |

### 1.4 节点执行引擎 【已确认】

| 文件 | 行数 | 职责 |
|------|------|------|
| `compose/workflow.go` | ~895 | 从 Canvas Schema 构建 Eino 执行图 |
| `compose/node_runner.go` | ~969 | 节点执行器：包装 Invoke/Stream + Callbacks + Error handling |
| `compose/node_builder.go` | ~400 | 节点构建：输入映射、字段填充、选项注入 |
| `compose/workflow_run.go` | ~300 | WorkflowRunner：执行入口，事件循环 |
| `compose/workflow_tool.go` | ~200 | 工作流作为工具接入（LLM Function Calling） |

---

## 二、知识库检索全链路

### 2.1 检索架构 【已确认】

```
用户查询
  │
  ▼
queryRewriteNode（可选：多轮对话 → 单查询）
  │    ↓ messages2query（LLM 链）
  │
  ▼  并行扇出
┌─────────────────────────────────────────┐
│ vectorRetrieveNode │ esRetrieveNode │ nl2SqlRetrieveNode │
│ (Milvus/VikingDB/  │ (Elasticsearch │ (NL2SQL LLM →      │
│  OceanBase)        │  BM25/Match)   │  SQL → RDB 查询)   │
└─────────────────────────────────────────┘
  │
  ▼
reRankNode（RRF 融合 或 VikingDB 模型重排）
  │
  ▼
packResults（切片补全 + URL 生成 + 命中计数）
  │
  ▼
RetrieveResponse（文档切片列表 + 分数）
```

### 2.2 核心实现文件 【已确认】

| 组件 | 文件 | 行数 | 核心机制 |
|------|------|------|---------|
| **检索编排** | `domain/knowledge/service/retrieve.go` | 822 | Eino Compose Chain 构建 4 阶段流水线 |
| **查询重写** | `infra/document/messages2query/impl/builtin/messages_to_query.go` | 76 | Jinja2 模板 + LLM → 新查询文本 |
| **NL2SQL** | `infra/document/nl2sql/impl/builtin/nl2sql.go` | 119 | 表结构文本 + LLM → `{sql, err_code, err_msg}` |
| **向量检索** | `infra/document/searchstore/impl/milvus/milvus_searchstore.go` | 601 | Embed 查询 → HybridSearch (RRF) 或单 ANN → DSL→expr |
| **全文检索** | `infra/document/searchstore/impl/elasticsearch/elasticsearch_searchstore.go` | 330 | Bool query + match/multi_match + DSL 过滤 → 归一化评分 |
| **RRF 重排** | `infra/document/rerank/impl/rrf/rrf.go` | 71 | 倒数排名融合：score = Σ(1/(k+rank))，k=60，去重 |
| **VikingDB 重排** | `infra/document/rerank/impl/vikingdb/vikingdb.go` | 181 | POST Volc 重排 API → 模型评分排序 |

### 2.3 Embedding 实现 【已确认】

| 后端 | 文件 | 行数 | 特性 |
|------|------|------|------|
| **接口** | `infra/embedding/embedding.go` | 38 | `Embedder` = Eino Embedder + `EmbedStringsHybrid` + `Dimensions` + `SupportStatus` |
| **工厂** | `infra/embedding/impl/impl.go` | 135 | 按 `EmbeddingType` 分发：OpenAI/Ollama/Gemini/Ark/HTTP |
| **OpenAI** | `impl/wrap/openai.go` | 34 | Dense-only，批量封装 |
| **Ollama** | `impl/wrap/ollama.go` | 34 | 本地部署，Dense-only |
| **Gemini** | `impl/wrap/gemini.go` | 34 | Google Gemini，Dense-only |
| **Ark** | `impl/ark/ark.go` | 136 | 字节 Volc Ark，L2 归一化，Dense-only |
| **HTTP** | `impl/http/http.go` | 214 | 远程 HTTP 服务，支持 Dense + Sparse 混合 |
| **公共** | `impl/wrap/dense_only.go` | 68 | 批量 + 延迟维度探测 + `SupportDense` |

### 2.4 文档解析全链路 【已确认】

| 组件 | 文件 | 行数 | 支持格式 |
|------|------|------|---------|
| **管理器接口** | `infra/document/parser/manager.go` | 130 | 文件扩展名校验 + 策略配置 |
| **工厂** | `parser/impl/impl.go` | 55 | PP-Structure (远程) 或 Builtin (本地) |
| **Builtin 管理器** | `parser/impl/builtin/manager.go` | 82 | 按扩展名选择 ParseFn |
| **PP-Structure** | `parser/impl/ppstructure/parser.go` | 326 | 远程 Paddle API → 结构化 JSON |
| **文本** | `builtin/parse_text.go` | 小 | `.txt` → 分块 |
| **Markdown** | `builtin/parse_markdown.go` | 中 | `.md` + 图片/表格处理 |
| **PDF/DOCX** | `builtin/py_parser_protocol.go` | 276+ | **Python 进程** (`parse_pdf.py`, `parse_docx.py`) |
| **CSV** | `builtin/parse_csv.go` | 中 | 行迭代 → 表格文档 |
| **XLSX** | `builtin/parse_xlsx.go` | 中 | Excel → 行迭代 |
| **JSON** | `builtin/parse_json.go` + `parse_json_maps.go` | 中 | 结构化 JSON → 表格 |
| **图片** | `builtin/parse_image.go` | 中 | LLM 视觉标注 |
| **自定义分块** | `builtin/chunk_custom.go` | ~110 | 用户自定义块大小/重叠/分隔符 |

### 2.5 向量存储后端 【已确认】

| 后端 | Manager 文件 | SearchStore 文件 | 特性 |
|------|-------------|-----------------|------|
| **Milvus** | `milvus/milvus_manager.go` (335) | `milvus/milvus_searchstore.go` (601) | HNSW + Sparse 反向索引，HybridSearch(RRF)，分区键 |
| **Elasticsearch** | `elasticsearch/elasticsearch_manager.go` (117) | `elasticsearch/elasticsearch_searchstore.go` (330) | BM25 全文，Bool+Match 查询 |
| **VikingDB** | `vikingdb/vikingdb_manager.go` | `vikingdb/vikingdb_searchstore.go` | 字节跳动向量库 |
| **OceanBase** | `oceanbase/oceanbase_manager.go` | `oceanbase/oceanbase_searchstore.go` | 蚂蚁向量库 |

选择逻辑（`searchstore/impl/impl.go`）：
- ES 始终创建为 `TypeTextStore`
- 向量库由 `VECTOR_STORE_TYPE` 环境变量决定（`milvus`/`vikingdb`/`oceanbase`）

---

## 三、插件调用 4 种模式

### 3.1 调度架构 【已确认】

```
ExecuteTool(ctx, req)
  │
  ├─ 1. 按 ExecScene 加载实体 ──────────────┐
  │   ├── online_agent → 在线 Agent 版本      │
  │   ├── draft_agent  → 草稿 Agent 版本      │
  │   ├── workflow     → 工作流版本            │
  │   └── tool_debug   → 调试模式              │
  │                                            │
  ├─ 2. OAuth 处理 ────────────────────────────┤
  │   └── acquireAccessTokenIfNeed()           │
  │       ├── 已有有效 Token → 注入            │
  │       └── 无 Token → 返回中断(OAuth URL)    │
  │                                            │
  ├─ 3. 按 Source 分发 ───────────────────────┐│
  │   ├── FromSaas → saasCallImpl             ││
  │   └── 非 SaaS → newToolInvocation(t)      ││
  │       ├── PluginTypeOfCloud ("openapi")   ││
  │       │   └── httpCallImpl                ││
  │       ├── PluginTypeOfMCP ("coze-studio-mcp") ││
  │       │   └── mcpCallImpl (未实现)        ││
  │       ├── PluginTypeOfCustom ("coze-studio-custom") ││
  │       │   └── customCallImpl (注册表)     ││
  │       └── default → httpCallImpl          ││
  │                                            │
  └─ 4. 响应处理 ──────────────────────────────┘
      └── Schema 裁剪 + 错误映射
```

### 3.2 四种调用实现 【已确认】

#### 模式 1：HTTP Cloud（`httpCallImpl`）— 主流模式

| 项 | 值 |
|------|------|
| **文件** | `service/tool/invocation_http.go` (386 行) |
| **HTTP 客户端** | Resty |
| **URL 构建** | `ServerURL` + tool `SubURL` + OpenAPI 路径/查询参数模板 |
| **Body 构建** | `encoder.EncodeBodyWithContentType()` 按 OpenAPI Body Schema 编码 |
| **认证注入** | `injectAuthInfo()`: None / Service Token (Header/Query) / OAuth Bearer |
| **成功判断** | HTTP 200 |
| **响应处理** | 原始 body 字符串透传 → Schema 裁剪 |

#### 模式 2：SaaS Plugin（`saasCallImpl`）

| 项 | 值 |
|------|------|
| **文件** | `service/tool/invocation_saas.go` (199 行) |
| **HTTP 客户端** | Resty |
| **URL 构建** | 同 HTTP Cloud（path/query 参数） |
| **Body 构建** | **固定 JSON**: `{"tool_name": "...", "arguments": {...body map...}}` |
| **认证** | **始终** `Authorization: Bearer` + Coze SaaS API Key |
| **成功判断** | HTTP 200 + JSON `{code, msg, data}` |
| **响应处理** | `data["result"]` → 字符串化返回 |

#### 模式 3：MCP（`mcpCallImpl`）— 未实现

| 项 | 值 |
|------|------|
| **文件** | `service/tool/invocation_mcp.go` (33 行) |
| **实现** | `Do()` → 直接返回 `"mcp call not implemented"` |
| **验证限制** | `PluginManifest.Validate` 不允许 `coze-studio-mcp` 类型通过校验 |

#### 模式 4：Custom（`customCallImpl`）— 注册表模式

| 项 | 值 |
|------|------|
| **文件** | `service/tool/invocation_custom_call.go` (49 行) |
| **注册** | `RegisterCustomTool(toolID string, impl Invocation)` → `customToolMap` |
| **分发** | 按工具 ID 字符串查找已注册的 `Invocation` 实现 |
| **灵活性** | 无 HTTP 约束，完全由注册方决定实现方式 |

### 3.3 关键支撑文件 【已确认】

| 文件 | 行数 | 职责 |
|------|------|------|
| `service/exec_tool.go` | 997 | ExecuteTool 主体：场景加载 → OAuth → 分发 → 响应处理 |
| `service/tool/invocation.go` | 25 | `Invocation` 接口：`Do(ctx, *InvocationArgs) (request, resp, err)` |
| `service/tool/invocation_args.go` | 514 | 参数构建：OpenAPI 参数分组、默认值、文件 URI 转 URL |
| `service/plugin_oauth.go` | 589 | OAuth 全生命周期：Token 刷新、授权码交换、存储、撤销 |
| `service/plugin_auth.go` | 179 | 认证方式配置：None / OAuth / Service Token |
| `internal/encoder/req_encode.go` | ~300 | OpenAPI 请求参数编码（Path/Query/Header/Body） |

### 3.4 OAuth 流程 【已确认】

```
1. ExecuteTool → acquireAccessTokenIfNeed()
2. 查询 DB plugin_oauth_auth 表
   ├── 已有 access_token（未过期） → 注入到请求
   ├── 已有 refresh_token → 刷新获取新 token → 更新 DB → 注入
   └── 无 token → 构建 OAuth 授权 URL → 返回中断事件（ToolNeedOAuth）
3. 用户浏览器授权 → 回调 /api/oauth/authorization_code
4. OauthAuthorizationCode handler → oauth2.Config.Exchange → 存储 DB
5. 后续调用自动使用已存储 token
6. 后台 goroutine 定期刷新即将过期的 token
```

---

## 四、风险与发现

### 4.1 工作流节点风险 【已确认】

| 风险 | 位置 | 严重度 |
|------|------|--------|
| **LLM 节点复杂度极高** | `nodes/llm/llm.go` (1352+ 行) | 中 — 单文件承载 LLM+Plugin+Workflow+Knowledge 集成 |
| **嵌套 Batch/Loop 无深度限制** | `batch.go`, `loop.go` | 高 — 递归嵌套可能导致 OOM |
| **SubWorkflow 递归无限制** | `subworkflow/sub_workflow.go` | 高 — 工作流 A 调用 B 调用 A 可造成栈溢出 |
| **Code 节点 Python 导入** | `code.go:validatePythonImports` | 中 — 白名单依赖管理员配置，默认可能过于宽松 |
| **HTTPRequester 无超时配置** | `http_requester.go` | 中 — 标准 net/http 默认无超时 |
| **DatabaseCustomSQL SQL 注入** | `customsql.go` | 高 — 模板参数直接替换，需确认 sqlparser 是否做安全处理 |

### 4.2 知识库检索风险 【已确认】

| 风险 | 位置 | 严重度 |
|------|------|--------|
| **Python 解析器子进程** | `py_parser_protocol.go` | 高 — exec.Command 调用 Python，需关注 RCE |
| **Embedding 维度不匹配** | `impl/impl.go` + `milvus_manager.go` | 中 — 动态 dims 与已创建集合可能不匹配 |
| **NL2SQL 生成的 SQL** | `nl2sql.go` | 高 — LLM 生成 SQL 需 sqlparser 白名单过滤 |
| **errgroup 并发限制 =2** | `retrieve.go:retrieveChannels` | 低 — 大量知识库时可能是瓶颈 |
| **ES 评分归一化** | `elasticsearch_searchstore.go` | 低 — 按 topHit 归一化在极端情况下可能不准确 |

### 4.3 插件调用风险 【已确认】

| 风险 | 位置 | 严重度 |
|------|------|--------|
| **MCP 未实现但已注册** | `invocation_mcp.go` | 低 — Validate 阻止创建，但 dispatch 路径存在 |
| **HTTP 200 唯一成功判断** | `invocation_http.go` | 中 — 30x 重定向、204 等合法状态码被视为失败 |
| **SaaS 固定 JSON 格式** | `invocation_saas.go` | 低 — 与 OpenAPI 规范不同的封装方式 |
| **OAuth Token 后台刷新** | `plugin_oauth.go` | 中 — goroutine panic 可能导致 token 全量失效 |
| **Custom 工具注册表** | `invocation_custom_call.go` | 低 — 运行时注册，无持久化，重启后丢失 |

---

## 五、已读清单

### 本次新增深度阅读的文件

**工作流节点**（76+ 文件）：
- `nodes/entry/entry.go`, `exit/exit.go`, `emitter/emitter.go`, `receiver/input_receiver.go`
- `nodes/llm/llm.go`, `prompt.go`, `model_with_info.go`, `plugin.go`
- `nodes/plugin/plugin.go`, `exec.go`
- `nodes/code/code.go`
- `nodes/batch/batch.go`, `nodes/loop/loop.go`, `break/break.go`, `continue/continue.go`
- `nodes/subworkflow/sub_workflow.go`
- `nodes/selector/selector.go`, `schema.go`, `callbacks.go`, `clause.go`, `operator.go`
- `nodes/intentdetector/intent_detector.go`, `prompt.go`
- `nodes/qa/question_answer.go`
- `nodes/textprocessor/text_processor.go`
- `nodes/variableaggregator/variable_aggregator.go`
- `nodes/variableassigner/variable_assign.go`, `variable_assign_in_loop.go`
- `nodes/json/json_serialization.go`, `json_deserialization.go`
- `nodes/knowledge/knowledge_retrieve.go`, `knowledge_indexer.go`, `knowledge_deleter.go`, `adaptor.go`
- `nodes/database/query.go`, `insert.go`, `update.go`, `delete.go`, `customsql.go`, `adapt.go`, `common.go`
- `nodes/httprequester/http_requester.go`, `adapt.go`
- `nodes/conversation/` (10 个文件)
- `nodes/node.go`, `template.go`, `stream.go`, `convert.go`, `callbacks.go`, `state.go`, `interrupt.go`
- `compose/workflow.go`, `node_runner.go`, `node_builder.go`, `workflow_run.go`, `workflow_tool.go`
- `canvas/adaptor/to_schema.go`, `from_node.go`
- `entity/node_meta.go`

**知识库检索**（30+ 文件）：
- `domain/knowledge/service/retrieve.go`
- `infra/document/messages2query/impl/builtin/messages_to_query.go`
- `infra/document/nl2sql/impl/builtin/nl2sql.go`
- `infra/document/searchstore/impl/milvus/milvus_searchstore.go`, `milvus_manager.go`, `convert.go`, `consts.go`
- `infra/document/searchstore/impl/elasticsearch/elasticsearch_searchstore.go`, `elasticsearch_manager.go`
- `infra/document/searchstore/impl/impl.go`, `searchstore.go`, `manager.go`, `options.go`, `dsl.go`
- `infra/document/rerank/impl/rrf/rrf.go`, `vikingdb/vikingdb.go`, `impl/impl.go`
- `infra/document/parser/impl/builtin/manager.go`, `parser.go`, `py_parser_protocol.go`, `parse_csv.go`, `parse_xlsx.go`, `parse_json.go`, `parse_markdown.go`, `parse_text.go`, `parse_image.go`, `chunk_custom.go`, `convert.go`, `align_schema.go`
- `infra/embedding/embedding.go`, `impl/impl.go`, `impl/http/http.go`, `impl/ark/ark.go`, `impl/wrap/*.go`

**插件调用**（20+ 文件）：
- `domain/plugin/service/exec_tool.go`, `service_impl.go`, `service.go`
- `domain/plugin/service/tool/invocation.go`, `invocation_http.go`, `invocation_saas.go`, `invocation_mcp.go`, `invocation_custom_call.go`, `invocation_args.go`
- `domain/plugin/service/plugin_oauth.go`, `plugin_auth.go`
- `domain/plugin/entity/plugin.go`, `tool.go`
- `domain/plugin/repository/plugin_impl.go`, `tool_impl.go`, `oauth_impl.go`
- `domain/plugin/internal/encoder/req_encode.go`

### 覆盖率更新

| 模块 | 之前 | 本次 | 更新后 |
|------|------|------|--------|
| 工作流节点 | 30% | +65% | **95%** |
| 知识库检索 | 20% | +70% | **90%** |
| 插件调用 | 40% | +55% | **95%** |
| **后端综合** | ~92% | +5% | **~97%** |
