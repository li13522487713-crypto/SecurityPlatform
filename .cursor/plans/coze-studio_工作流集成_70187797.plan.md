---
name: Coze-Studio 工作流集成
overview: 将 coze-studio 的工作流引擎（V2）功能完整集成到 Atlas Security Platform 中。Atlas 已定义了 V2 领域实体、枚举、值对象、应用层接口、仓储接口/实现、DAG 执行引擎和 5 个节点执行器，但缺少 V2 应用服务实现、DI 注册、数据库表初始化、API 控制器和前端集成。
todos:
  - id: db-init
    content: 数据库初始化：在 DatabaseInitializerHostedService 中添加 WorkflowMeta/Draft/Version/Execution/NodeExecution 5 个 V2 实体表
    status: pending
  - id: di-register
    content: DI 注册：在 WorkflowServiceRegistration 中注册 V2 仓储、DagExecutor、NodeExecutorRegistry、节点执行器、V2 应用服务
    status: pending
  - id: v2-command-svc
    content: 实现 WorkflowV2CommandService（Create/Save/UpdateMeta/Publish/Delete/Copy）
    status: pending
  - id: v2-query-svc
    content: 实现 WorkflowV2QueryService（List/Get/ListVersions/GetProcess/GetNodeDetail/GetNodeTypes）
    status: pending
  - id: v2-exec-svc
    content: 实现 WorkflowV2ExecutionService（SyncRun/AsyncRun/Cancel/Resume/DebugNode/StreamRun）
    status: pending
  - id: v2-controller
    content: 新建 WorkflowV2Controller（api/v2/workflows），暴露 17 个端点
    status: pending
  - id: validators
    content: 新建 V2 FluentValidation 验证器（Create/Save/Publish/Run/NodeDebug）
    status: pending
  - id: node-executors-p0
    content: 补充 P0 节点执行器：Loop、CodeRunner、HttpRequester、TextProcessor
    status: pending
  - id: node-executors-p1
    content: 补充 P1 节点执行器：DatabaseQuery、AssignVariable、VariableAggregator、JsonSerialization/Deserialization
    status: pending
  - id: http-tests
    content: 新建 Workflows-V2.http 测试文件，覆盖所有 V2 端点
    status: pending
  - id: contracts-doc
    content: 更新 docs/contracts.md 添加 V2 Workflow API 契约
    status: pending
isProject: false
---

# Coze-Studio 工作流引擎集成到 Atlas Security Platform

## 现状分析

### Atlas 已有（V2 Coze 风格工作流）

**领域层**（`Atlas.Domain.Workflow`）：

- 实体：`WorkflowMeta`, `WorkflowDraft`, `WorkflowVersion`, `WorkflowExecution`, `NodeExecution`
- 枚举：`ExecutionStatus`(6 值), `InterruptType`(3 值), `NodeType`(28 种), `WorkflowLifecycleStatus`(3 值), `WorkflowMode`(2 值)
- 值对象：`CanvasSchema`, `ConnectionSchema`, `NodeLayout`, `NodeSchema`

**应用层**（`Atlas.Application.Workflow`）：

- 接口：`IWorkflowV2CommandService`(6 方法), `IWorkflowV2QueryService`(6 方法), `IWorkflowV2ExecutionService`(6 方法)
- 仓储接口：5 个（Meta/Draft/Version/Execution/NodeExecution）
- DTO：14 个请求/响应模型 + SSE 事件模型

**基础设施层**（`Atlas.Infrastructure`）：

- 仓储实现：5 个 SqlSugar 仓储（`[Atlas.Infrastructure/Repositories/Workflow/](src/backend/Atlas.Infrastructure/Repositories/Workflow/)`）
- DAG 执行引擎：`[DagExecutor](src/backend/Atlas.Infrastructure/Services/WorkflowEngine/DagExecutor.cs)` - 支持同步/流式执行、并行分支、中断
- 节点执行器注册表：`[NodeExecutorRegistry](src/backend/Atlas.Infrastructure/Services/WorkflowEngine/NodeExecutorRegistry.cs)`
- 已实现节点：`EntryNodeExecutor`, `ExitNodeExecutor`, `IfNodeExecutor`, `LlmNodeExecutor`, `SubWorkflowNodeExecutor`

### Atlas 缺失项


| 层次   | 缺失内容                                                                                   | 优先级 |
| ---- | -------------------------------------------------------------------------------------- | --- |
| 数据库  | V2 实体表未在 `DatabaseInitializerHostedService` 中初始化                                       | P0  |
| DI   | V2 仓储、DagExecutor、NodeExecutorRegistry、各节点执行器未注册                                       | P0  |
| 应用服务 | `WorkflowV2CommandService`, `WorkflowV2QueryService`, `WorkflowV2ExecutionService` 无实现 | P0  |
| 控制器  | 无 `api/v2/workflows` 控制器                                                               | P0  |
| 验证   | V2 请求缺少 FluentValidation 验证器                                                           | P1  |
| 节点   | 缺少 Loop/Break/Continue/Batch/CodeRunner/HttpRequester/TextProcessor 等节点执行器             | P1  |
| 测试   | 无 `.http` 测试文件                                                                         | P1  |
| 前端   | 无工作流画布编辑器页面                                                                            | P2  |


### Coze-Studio 功能对照

coze-studio 拥有 15 个业务域（见 `[docs/coze-studio-feature-atlas.md](docs/coze-studio-feature-atlas.md)`）。以下按与 Atlas 安全平台的契合度分级：

**第一阶段（本次集成重点）- 工作流引擎 V2**：

- 工作流 CRUD + 草稿/版本管理
- 工作流发布
- 测试运行（同步 + SSE 流式）
- 单节点调试
- 执行进度查询
- 补充缺失的节点执行器

**第二阶段（后续考虑）**：

- 知识库 / RAG（安全知识管理）
- 插件系统（安全工具扩展）
- Agent / Bot 对话（AI 安全助手）
- 管理后台（模型管理）

---

## 第一阶段实施计划（后端）

### Step 1: 数据库表初始化

在 `[DatabaseInitializerHostedService.cs](src/backend/Atlas.Infrastructure/Services/DatabaseInitializerHostedService.cs)` 第 162 行 `PersistedSubscription` 之后添加 V2 实体表：

```csharp
typeof(WorkflowMeta),
typeof(WorkflowDraft),
typeof(WorkflowVersion),
typeof(WorkflowExecution),
typeof(NodeExecution),
```

### Step 2: DI 注册

在 `[WorkflowServiceRegistration.cs](src/backend/Atlas.Infrastructure/DependencyInjection/WorkflowServiceRegistration.cs)` 中添加：

- 5 个 V2 仓储（Scoped）
- `DagExecutor`（Scoped）
- `NodeExecutorRegistry`（Singleton）
- 各 `INodeExecutor` 实现（Singleton）
- 3 个 V2 应用服务（Scoped）

### Step 3: V2 应用服务实现

在 `Atlas.Infrastructure/Services/` 下新建 3 个服务：

- **WorkflowV2CommandService** - 实现 `IWorkflowV2CommandService`
  - `CreateWorkflowAsync`：创建 WorkflowMeta + WorkflowDraft（默认空画布）
  - `SaveDraftAsync`：更新 WorkflowDraft.CanvasJson + CommitId
  - `UpdateMetaAsync`：更新 WorkflowMeta 名称/描述
  - `PublishAsync`：从 Draft 创建 WorkflowVersion，更新 Meta.LatestVersion
  - `DeleteWorkflowAsync`：软删或物理删除 Meta/Draft
  - `CopyWorkflowAsync`：复制 Meta + Draft
- **WorkflowV2QueryService** - 实现 `IWorkflowV2QueryService`
  - `ListWorkflowsAsync`：分页查询 WorkflowMeta
  - `GetWorkflowAsync`：获取详情（Meta + Draft）
  - `ListVersionsAsync`：按 WorkflowId 查版本列表
  - `GetExecutionProcessAsync`：查执行状态 + 各节点执行记录
  - `GetNodeExecutionDetailAsync`：单个节点执行详情
  - `GetNodeTypesAsync`：返回所有已注册节点类型元数据
- **WorkflowV2ExecutionService** - 实现 `IWorkflowV2ExecutionService`
  - `SyncRunAsync`：解析画布 JSON → CanvasSchema，调用 DagExecutor.RunAsync
  - `AsyncRunAsync`：后台执行，返回 executionId
  - `CancelAsync`：取消执行（CancellationTokenSource）
  - `ResumeAsync`：恢复中断的执行
  - `DebugNodeAsync`：提取目标节点的子图，单独执行
  - `StreamRunAsync`：创建 Channel，调用 DagExecutor.StreamRunAsync，返回 IAsyncEnumerable

### Step 4: V2 API 控制器

新建 `[WorkflowV2Controller](src/backend/Atlas.WebApi/Controllers/)` - 路由 `api/v2/workflows`：

- `POST /` - 创建工作流
- `GET /` - 工作流列表（分页）
- `GET /{id}` - 工作流详情
- `PUT /{id}/meta` - 更新元信息
- `PUT /{id}/draft` - 保存草稿
- `DELETE /{id}` - 删除工作流
- `POST /{id}/copy` - 复制工作流
- `POST /{id}/publish` - 发布
- `GET /{id}/versions` - 版本列表
- `POST /{id}/run` - 同步运行
- `POST /{id}/stream` - SSE 流式运行（`text/event-stream`）
- `POST /executions/{execId}/cancel` - 取消执行
- `POST /executions/{execId}/resume` - 恢复执行
- `GET /executions/{execId}/process` - 执行进度
- `GET /executions/{execId}/nodes/{nodeKey}` - 节点执行详情
- `POST /{id}/debug-node` - 单节点调试
- `GET /node-types` - 节点类型列表

### Step 5: FluentValidation 验证器

在 `Atlas.Application.Workflow/Validators/V2/` 下新建：

- `WorkflowCreateRequestValidator` - Name 必填（2-100 字符）
- `WorkflowSaveRequestValidator` - CanvasJson 必填
- `WorkflowPublishRequestValidator` - ChangeLog 非空
- `WorkflowRunRequestValidator` - 基本校验
- `NodeDebugRequestValidator` - NodeKey 必填

### Step 6: 补充节点执行器

参考 coze-studio 47 种节点，Atlas 当前有 28 种 NodeType 枚举，已实现 5 个执行器。按优先级补充：

**P0（核心流程节点）**：

- `LoopNodeExecutor` - 循环，支持计数器 + Break/Continue
- `CodeRunnerNodeExecutor` - C# 脚本执行（安全沙箱）
- `HttpRequesterNodeExecutor` - HTTP 请求
- `TextProcessorNodeExecutor` - 文本模板渲染

**P1（数据节点）**：

- `DatabaseQueryNodeExecutor` - 查询数据库
- `AssignVariableNodeExecutor` - 变量赋值
- `VariableAggregatorNodeExecutor` - 变量聚合
- `JsonSerializationNodeExecutor` / `JsonDeserializationNodeExecutor`

**P2（高级节点）**：

- `KnowledgeRetrieverNodeExecutor` - RAG 检索
- `PluginApiNodeExecutor` - 插件调用
- `IntentDetectorNodeExecutor` - 意图识别
- `QuestionAnswerNodeExecutor` - 人机交互中断

### Step 7: .http 测试文件

新建 `[Workflows-V2.http](src/backend/Atlas.WebApi/Bosch.http/Workflows-V2.http)`，覆盖所有 V2 端点。

### Step 8: 契约文档更新

更新 `[docs/contracts.md](docs/contracts.md)` 添加 V2 Workflow API 契约。

---

## 第一阶段实施计划（前端 - 后续）

前端工作流编辑器需要：

- 画布组件（可考虑引入 FlowGram 或类似 Vue 生态画布库如 vue-flow）
- 节点面板（拖拽添加节点）
- 节点配置表单（每种节点类型的参数配置）
- 连线交互
- 测试运行面板（SSE 实时状态）
- 版本管理 UI

前端作为独立阶段，后端 API 完成后再启动。

---

## 技术对照：Coze-Studio Go vs Atlas .NET


| Coze 概念                                        | Atlas 实现                                        |
| ---------------------------------------------- | ----------------------------------------------- |
| `domain/workflow/entity/node_meta.go` NodeType | `Atlas.Domain.Workflow.Enums.NodeType` 枚举       |
| Canvas JSON (nodes + edges)                    | `CanvasSchema` 值对象 (Nodes + Connections)        |
| `compose.NewWorkflow` DAG 构造                   | `DagExecutor.ExecuteGraphAsync` 拓扑遍历            |
| `schema.Pipe[*Message]` 流式管道                   | `Channel<SseEvent>` + `IAsyncEnumerable`        |
| `NodeAdaptor` 注册                               | `INodeExecutor` + `NodeExecutorRegistry`        |
| `ExecuteModeDebug` 单节点调试                       | `DebugNodeAsync` 提取子图执行                         |
| Eino LLM 调用                                    | `ILlmProviderFactory` + `ChatCompletionRequest` |


---

## 非工作流功能评估（第二阶段路线图）


| Coze 功能域 | Atlas 适配方向      | 已有基础                              | 优先级 |
| -------- | --------------- | --------------------------------- | --- |
| 知识库 RAG  | 安全知识库、漏洞库检索     | KnowledgeBase/Document/Chunk 实体已有 | P1  |
| 插件系统     | 安全扫描工具集成        | AiPlugin/AiPluginApi 实体已有         | P2  |
| Agent 对话 | 安全 AI 助手        | Agent/Conversation/ChatMessage 已有 | P2  |
| 数据库/变量   | 工作流运行时变量        | AiDatabase/AiVariable 已有          | P2  |
| 模型管理     | LLM Provider 管理 | ModelConfig 已有                    | P1  |
| 市场/探索    | 安全模板/规则市场       | AiMarketplace 实体已有                | P3  |


