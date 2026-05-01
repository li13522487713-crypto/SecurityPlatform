# 微流引擎与 Mendix Studio 前后端闭环增强需求文档

## 1. 文档信息

| 项目 | 内容 |
| ---- | ---- |
| 文档名称 | 微流引擎与 Mendix Studio 前后端闭环增强需求文档 |
| 适用系统 | SecurityPlatform / Lowcode Studio / Mendix Studio / Microflow Runtime |
| 页面范围 | `/space/:spaceId/mendix-studio/:appId` |
| 核心目标 | 前端画出来的每条线、每个分支、每个 Loop Body、每个 Gateway，在后端都有一致的编译语义、校验语义、执行语义和 Debug 语义 |
| 优先级 | P0 |
| 目标版本 | 微流 Runtime 生产级增强版本 |
| 编写目的 | 指导前端设计器、后端校验器、ExecutionPlan、Runtime、Debug、测试验收的一体化整改 |

## 2. 背景与现状问题

当前微流模块已经具备基础画布、节点配置、保存、校验、试运行和部分 Runtime 能力，但在复杂微流场景下仍然存在前后端语义不闭环的问题。

典型表现包括：

```text
MF_FLOW_INVALID_TARGET:
Flow 不允许跨 root / loop collection 连接。

MF_DECISION_BOOLEAN_TRUE_MISSING:
Boolean Decision 缺少 true 分支。

MF_DECISION_BOOLEAN_FALSE_MISSING:
Boolean Decision 缺少 false 分支。
```

这些问题说明：前端画布上“看起来可以连接、可以分支、可以运行”的结构，在后端 SchemaReader、Validator、ExecutionPlan 和 Runtime 中并不一定能被稳定识别。

现有代码中，后端已经定义了 Flow、Decision、Loop、Action、Variable、Expression、ErrorHandling 等校验错误码，说明后端校验体系已经存在，但前端建模层与后端语义层仍未完全统一。

同时，微流试运行会先经过后端校验，校验存在 error 时会直接阻止试运行，因此这类问题不是 Runtime 执行失败，而是执行前校验阶段就被阻断。

## 3. 最终目标

本次需求的最终目标不是“让微流勉强跑起来”，而是把微流模块升级为：

```text
可建模
可保存
可校验
可编译
可执行
可调试
可回放
可追踪
可扩展
可生产使用
```

最终必须达到：

1. 前端设计器中的每个节点都有明确后端 Runtime 语义。
2. 前端设计器中的每条线都能被后端稳定识别。
3. Decision 分支必须完整表达 true / false / fallback / enumeration / object type case。
4. Loop 内外边界必须严格一致，不允许生成非法跨 collection flow。
5. Parallel Gateway / Inclusive Gateway 必须具备真实执行语义，而不是仅作为 pass-through 节点。
6. Runtime 必须基于 ExecutionPlan 执行，避免每次重复解析和重复构图。
7. Debug 模式必须展示每个节点的 input、output、变量快照、变量 delta、selected case 和 handoff 信息。
8. 表达式编辑器必须支持变量选择、类型提示、复合条件、调试预览。
9. 画布交互必须接近 Mendix Studio Pro 桌面建模器体验。
10. 执行日志、快照、Debug 数据不能拖垮主流程，必须具备内存预算和摘要化能力。

## 4. 范围说明

### 4.1 本次纳入范围

| 模块 | 是否纳入 | 说明 |
| ---- | ---: | ---- |
| Mendix Studio 微流设计器 | 是 | 画布、拖拽、连线、小地图、沉浸式、属性面板 |
| 微流 Schema 转换层 | 是 | 前端 Authoring Schema、FlowGram JSON、后端 Design Schema |
| 后端 SchemaReader | 是 | 节点、连线、collection、caseValues、loop body 读取 |
| 后端 Validator | 是 | Flow、Decision、Loop、Gateway、Action、Variable、Expression 校验 |
| ExecutionPlan | 是 | 编译、缓存、稳定 plan key、branch/gateway/loop 语义 |
| Runtime Engine | 是 | 顺序执行、分支执行、循环执行、网关执行、子微流调用 |
| Debug / Trace | 是 | 节点输入输出、变量 delta、执行路径、错误定位 |
| 表达式引擎 | 是 | 变量引用、复合判断、类型校验、调试预览 |
| 测试用例 | 是 | 复杂节点族验收微流、单元测试、集成测试、E2E |

### 4.2 本次不纳入范围

| 模块 | 是否纳入 | 说明 |
| ---- | ---: | ---- |
| 完整长流程 Workflow Runtime | 否 | 本次聚焦短事务 Microflow Runtime |
| 人工任务审批流 | 否 | 后续 Workflow 阶段单独设计 |
| 大规模分布式调度 | 否 | 本阶段先完成单体 Runtime 闭环 |
| 全量重写前端画布 | 否 | 在现有 FlowGram 架构基础上修复和增强 |
| 全量重写后端 Runtime | 否 | 在现有 Runtime、Validator、ExecutionPlan 基础上补强 |

## 5. 核心设计原则

### 5.1 Schema 语义唯一原则

同一个微流结构只能有一种合法存储语义。

不允许出现：

```text
前端认为合法
后端 Validator 认为非法
Runtime 又尝试兼容执行
```

目标链路必须统一为：

```text
Frontend Authoring Schema
→ Schema Normalizer
→ Backend SchemaReader
→ Validator
→ ExecutionPlan Compiler
→ Runtime Engine
→ Debug Trace
```

### 5.2 Loop Collection 边界严格原则

Loop 内部节点必须属于 Loop 自己的 objectCollection。

合法结构：

```text
root collection:
  A → LoopedActivity → B

loop collection:
  loopStartLikeNode → node1 → node2 → continue / break / local terminal
```

非法结构：

```text
root node → loop inner node
loop inner node → root node
```

前端不得保存这种跨 collection 的普通 SequenceFlow。

后端 Validator 当前已经明确禁止 origin 和 destination 不同 collection 的 flow。

### 5.3 Decision 分支完整原则

Boolean Decision 必须同时存在：

```text
true 分支
false 分支
```

Enumeration Decision 必须支持：

```text
枚举值分支
empty / noCase
fallback / default
```

Object Type Decision 必须支持：

```text
inheritance case
empty
fallback
```

后端当前依赖 outgoing flow 的 `caseValues` 判断 true / false 是否存在，因此前端必须稳定写入 `caseValues`，不能只依赖端口 label。

### 5.4 Runtime 执行语义优先原则

微流不是画布配置，而是可执行程序。

所有节点必须回答：

```text
输入是什么？
执行什么？
输出是什么？
变量如何变化？
失败如何处理？
是否进入事务？
是否写日志？
下一个节点是谁？
```

### 5.5 Debug 可观测原则

Debug 模式下，用户必须能看到每个节点：

```text
input
output
变量变化
表达式求值
selected case
incoming flow
outgoing flow
耗时
错误
事务效果
```

## 6. 当前关键问题清单

### 6.1 P0：跨 root / loop collection 连线被后端阻止

#### 问题描述

前端可能生成跨 collection 的 flow，例如：

```text
root 节点 → loop 内部节点
loop 内部节点 → root 节点
```

后端 Validator 会识别为：

```text
MF_FLOW_INVALID_TARGET
```

#### 影响

1. 微流保存后试运行失败。
2. 用户在画布上看不出问题来源。
3. 复杂 loop 微流无法稳定测试。
4. 前端和后端对于 Loop 边界的理解不一致。

#### 修复要求

1. 前端 `addFlow` 必须禁止跨 collection 普通 flow。
2. FlowGram 新增连线时必须先计算 source / target 所属 collection。
3. 如果 source 和 target collection 不一致，直接阻止连线并提示用户。
4. Loop 入口必须连接到 LoopedActivity 节点本身。
5. Loop 内部流程只能在 Loop 容器内部编辑。
6. Break / Continue 通过 Runtime LoopExecutor 语义处理，不通过跨 collection flow 表达。
7. 后端 Validator 保持严格规则，不允许为了兼容前端错误结构而放松。

### 6.2 P0：Boolean Decision 缺少 true / false 分支

#### 问题描述

Boolean Decision 的出边未稳定写入 `caseValues`，导致后端无法识别 true / false 分支。

#### 影响

1. 试运行被后端校验阻止。
2. 用户明明画了两条线，但后端仍然认为缺分支。
3. Debug 无法准确高亮 selected case。
4. ExecutionPlan 无法编译稳定分支表。

#### 修复要求

1. FlowGram edge 转 MicroflowFlow 时，空数组 `caseValues: []` 不允许覆盖默认 case。
2. 如果 source 是 Boolean Decision，新增第一条分支默认 true，第二条默认 false。
3. 保存和试运行前增加 `repairBooleanDecisionBranches`。
4. 缺少 true / false 时，画布节点上直接显示错误 badge。
5. Problems 面板点击错误时必须定位到具体 Decision 节点。
6. 后端错误返回必须包含 `objectId`、`relatedFlowIds` 和准确 `fieldPath`。

### 6.3 P0：Parallel / Inclusive Gateway 只有画布语义，没有完整 Runtime 语义

#### 问题描述

当前 Runtime 对 `parallelGateway` / `inclusiveGateway` 更接近 pass-through 逻辑，不能证明多分支真实执行。

Runtime 中已经存在 gateway 节点分发入口，但执行行为需要升级为真正的 fork / join。

#### 影响

1. 全节点族验收微流无法证明网关真实执行。
2. 并行分支变量作用域不清晰。
3. 合并策略不明确。
4. Debug 无法展示每条分支执行过程。

#### 修复要求

1. Validator 增加 `parallelGateway`、`inclusiveGateway` 合法节点类型。
2. ExecutionPlan 编译 Gateway 节点为明确的 split / merge / splitMerge 结构。
3. Parallel Gateway 必须执行所有分支。
4. Inclusive Gateway 必须按条件选择多条分支。
5. Merge 必须等待必要分支完成。
6. 并行分支必须使用隔离变量作用域。
7. 分支合并时必须有变量冲突处理策略。
8. Debug Trace 必须展示每个分支是否执行、是否跳过、是否失败。

### 6.4 P0：节点 Debug 输入输出不够清晰

#### 问题描述

当前 Trace 已经有 input/output/variables snapshot 存储基础，但需要统一成明确的节点 I/O 协议。

Trace frame 持久化时已经包含 InputJson、OutputJson、VariablesSnapshotJson、SelectedCaseValueJson、LoopIterationJson 等字段，说明后端具备扩展基础。

#### 修复要求

Debug 模式每个节点必须输出：

```text
Node Input
Action Input
Evaluated Expressions
Node Output
Variable Delta
Handoff Payload
Selected Case
Loop Iteration
Transaction Effect
Error Detail
```

## 7. 目标架构

### 7.1 总体链路

```text
Mendix Studio Canvas
  ↓
Authoring Schema
  ↓
Schema Normalizer
  ↓
Backend SchemaReader
  ↓
Runtime Validator
  ↓
ExecutionPlan Compiler
  ↓
ExecutionPlan Cache
  ↓
Runtime Engine
  ↓
Node Executor / Loop Executor / Gateway Executor
  ↓
Trace / Debug / Audit
  ↓
Frontend Debug Panel / Canvas Highlight
```

### 7.2 前端设计器目标架构

```text
FlowGram Canvas
  ├─ Node Registry
  ├─ Edge Registry
  ├─ Port Rules
  ├─ Case Editor
  ├─ Expression Editor
  ├─ Property Panel
  ├─ Problems Panel
  ├─ Debug Panel
  ├─ Minimap
  └─ Schema Adapter
```

前端的职责是：

1. 只生成后端可识别的合法 Schema。
2. 在用户建模时提前阻止非法连线。
3. 把 `collectionId`、`parentObjectId`、`caseValues`、`edgeKind` 稳定写入 Schema。
4. 在保存、校验、运行前执行 Schema repair。
5. 根据后端 validation issues 精确定位画布节点和连线。
6. 根据 Runtime trace 高亮执行路径。

FlowGram 当前已经通过 `authoringToFlowGram` 把节点、边、caseValues、runtimeState、validationState 等写入 FlowGram JSON，这是后续增强的基础。

### 7.3 后端 Runtime 目标架构

```text
MicroflowRuntimeEngine
  ├─ ExecutionPlanLoader
  ├─ ExecutionPlanCache
  ├─ RuntimeGraph
  ├─ ExecutionContext
  ├─ VariableScopeManager
  ├─ ExpressionEvaluator
  ├─ TransactionManager
  ├─ ActionExecutorRegistry
  ├─ LoopExecutor
  ├─ GatewayExecutor
  ├─ ErrorHandlingService
  ├─ DebugCoordinator
  └─ TraceWriter
```

执行引擎需要从“简单顺序解释执行器”升级为“ExecutionPlan-first 的可观测 Runtime”。执行引擎应重点覆盖 ExecutionContext、Scope、Transaction、MemoryBudget、ExecutionPlan、NodeExecutor、ExpressionEngine、UnitOfWork、ExecutionLog、DebugSession 等能力，不能只关注节点是否能被执行。

## 8. 功能需求

### 8.1 Schema Normalizer

#### 8.1.1 需求描述

新增统一 Schema Normalizer，在保存、校验、运行前执行。

#### 8.1.2 功能要求

1. 补齐缺失的 `schemaVersion`。
2. 统一 root collection id。
3. 统一 loop collection id。
4. 统一节点 `collectionId`。
5. 统一 flow 所属 collection。
6. 修复 Boolean Decision 的空 `caseValues`。
7. 修复 edgeKind 与 caseValues 不一致问题。
8. 检查重复 node id / flow id。
9. 检查非法跨 collection flow。
10. 输出 repair report。

#### 8.1.3 输出示例

```json
{
  "repaired": true,
  "changes": [
    {
      "type": "decisionCaseRepair",
      "objectId": "decision-1",
      "flowId": "flow-true",
      "before": [],
      "after": [
        {
          "kind": "boolean",
          "value": true,
          "persistedValue": "true"
        }
      ]
    }
  ],
  "blockingIssues": []
}
```

### 8.2 Flow 连线规则增强

#### 8.2.1 普通 Sequence Flow

允许：

```text
同一 collection 内节点 → 同一 collection 内节点
```

禁止：

```text
root collection 节点 → loop collection 节点
loop collection 节点 → root collection 节点
不同 loop collection 节点互连
```

#### 8.2.2 Decision Flow

必须满足：

```text
source.kind = exclusiveSplit
edgeKind = decisionCondition
caseValues 非空
```

Boolean Decision 必须最终存在：

```text
true case
false case
```

#### 8.2.3 Object Type Flow

必须满足：

```text
source.kind = inheritanceSplit
edgeKind = objectTypeCondition
caseValues 包含 inheritance / empty / fallback
```

#### 8.2.4 Error Handler Flow

必须满足：

```text
source 支持 error handling
target 通常为 error handler path 起点
同一个 source 最多一条 error handler flow
isErrorHandler = true
edgeKind = errorHandler
```

#### 8.2.5 Annotation Flow

Annotation Flow 不参与 Runtime 控制流。

### 8.3 Decision 分支配置

#### 8.3.1 属性面板能力

Decision 属性面板必须包含：

```text
Condition Type:
  - Boolean
  - Enumeration
  - Rule
  - Object Type

Expression:
  - Raw expression
  - Visual builder

Branches:
  - true
  - false
  - fallback
  - enumeration values
```

#### 8.3.2 Boolean Decision UI

必须展示：

```text
true branch: 已连接 / 未连接
false branch: 已连接 / 未连接
```

缺少分支时显示：

```text
Boolean Decision 缺少 false 分支
[一键创建 false 分支]
```

#### 8.3.3 验收标准

1. 用户从 Decision 拉第一条线，默认 true。
2. 用户从 Decision 拉第二条线，默认 false。
3. true / false 已存在时，不允许再创建重复 true / false。
4. 修改边属性时可以切换 true / false，但不能产生重复 case。
5. 保存后后端不再报 `MF_DECISION_BOOLEAN_TRUE_MISSING`。
6. 保存后后端不再报 `MF_DECISION_BOOLEAN_FALSE_MISSING`。

### 8.4 Loop 建模能力

#### 8.4.1 Loop 容器规则

Loop 节点是 root collection 的一个普通节点，但 Loop Body 是独立 objectCollection。

```text
LoopedActivity
  └─ objectCollection
      ├─ body node 1
      ├─ body node 2
      └─ body flow
```

#### 8.4.2 前端交互

1. 拖入 Loop 容器 body 区域的节点，自动归属该 Loop collection。
2. 拖到 Loop header 区域时，不进入 body，只选中 Loop 节点。
3. Loop 内节点连线只能连接 Loop 内节点。
4. Loop 外节点连线只能连接 Loop 外节点。
5. Loop 外进入 Loop，必须连接 LoopedActivity 节点本身。
6. Loop 内退出 Loop，必须使用 Break / Continue 语义，不允许直接连到 root 节点。

#### 8.4.3 Runtime 执行语义

LoopExecutor 必须支持：

```text
iterableList
whileCondition
$currentIndex
iteratorVariableName
continueEvent
breakEvent
maxIterations
loop variable scope
```

#### 8.4.4 Debug 展示

Loop Debug 必须展示：

```text
iteration index
iterator value
loop input
loop output
continue / break reason
loopScore delta
```

### 8.5 Gateway 执行能力

#### 8.5.1 Parallel Gateway

Parallel Gateway 必须支持：

```text
fork all branches
wait all branches
merge variable outputs
record branch trace
handle branch failure
```

#### 8.5.2 Inclusive Gateway

Inclusive Gateway 必须支持：

```text
evaluate branch conditions
select one or more branches
execute selected branches
skip unselected branches
merge selected branches
```

#### 8.5.3 变量合并策略

默认策略：

| 场景 | 默认行为 |
| ---- | ---- |
| 分支只读变量 | 共享 |
| 分支局部变量 | 分支私有 |
| 分支输出变量 | 显式 merge |
| 多分支写同名变量 | 报冲突 |
| List append | 需要配置 append merge |
| Object 修改 | 需要 UnitOfWork 管理 |
| 数据库写入 | 不共享同一个 DbContext |

#### 8.5.4 Debug 展示

```text
Parallel Gateway
  branch A: executed, output = 7
  branch B: executed, output = 11
  merge result = 18

Inclusive Gateway
  branch A: selected, output = 5
  branch B: selected, output = 7
  merge result = 12
```

### 8.6 ExecutionPlan 编译与缓存

#### 8.6.1 需求描述

Runtime 必须优先使用 ExecutionPlan，而不是每次运行都重复解析 JSON 和重复构建图。

当前后端已经有 `MicroflowExecutionPlanBuilder`，并能把 flows 拆分为 NormalFlows、DecisionFlows、ObjectTypeFlows、ErrorHandlerFlows、IgnoredFlows、LoopCollections 等，这是目标架构的基础。

#### 8.6.2 ExecutionPlan 内容

```csharp
public sealed class CompiledMicroflowExecutionPlan
{
    public string PlanId { get; init; }
    public string ResourceId { get; init; }
    public string SchemaSnapshotId { get; init; }
    public string Version { get; init; }
    public string SchemaHash { get; init; }

    public string StartNodeId { get; init; }

    public IReadOnlyDictionary<string, CompiledNode> Nodes { get; init; }
    public IReadOnlyDictionary<string, CompiledFlow[]> NormalOutgoing { get; init; }
    public IReadOnlyDictionary<string, CompiledFlow[]> DecisionOutgoing { get; init; }
    public IReadOnlyDictionary<string, CompiledLoopBody> LoopBodies { get; init; }
    public IReadOnlyDictionary<string, CompiledGateway> Gateways { get; init; }

    public IReadOnlyList<CompiledVariableDeclaration> Variables { get; init; }
    public IReadOnlyList<ValidationDiagnostic> Diagnostics { get; init; }

    public DateTime CreatedAt { get; init; }
}
```

#### 8.6.3 缓存 Key

```text
resourceId
schemaSnapshotId
version
schemaHash
mode
metadataVersion
connectorCapabilitiesHash
```

#### 8.6.4 缓存失效

以下场景必须失效：

1. 微流 Schema 变化。
2. 微流版本变化。
3. Domain Model 变化。
4. Metadata Catalog 变化。
5. Connector Capability 变化。
6. Runtime mode 变化。
7. 后端 ActionExecutor 能力变化。

### 8.7 ExecutionContext 与变量作用域

#### 8.7.1 ExecutionContext

```csharp
public sealed class MicroflowExecutionContext
{
    public string RunId { get; init; }
    public string ResourceId { get; init; }
    public string SchemaId { get; init; }
    public string Version { get; init; }

    public string TenantId { get; init; }
    public string WorkspaceId { get; init; }
    public string UserId { get; init; }
    public string TraceId { get; init; }
    public string CorrelationId { get; init; }

    public CancellationToken CancellationToken { get; init; }

    public VariableScope RootScope { get; init; }
    public Stack<VariableScope> ScopeStack { get; init; }

    public ExecutionMemoryBudget MemoryBudget { get; init; }
    public ExecutionRuntimeOptions Options { get; init; }

    public IMicroflowTransactionManager TransactionManager { get; init; }
    public IMicroflowTraceWriter TraceWriter { get; init; }
}
```

#### 8.7.2 Scope 类型

```csharp
public enum VariableScopeType
{
    Root,
    Parameter,
    Node,
    Branch,
    Loop,
    SubMicroflow,
    ErrorHandler,
    Gateway,
    Debug
}
```

#### 8.7.3 变量对象

```csharp
public sealed class RuntimeVariable
{
    public string Name { get; init; }
    public RuntimeType Type { get; init; }
    public object? Value { get; init; }

    public bool IsReadOnly { get; init; }
    public bool IsLargeObject { get; init; }
    public long EstimatedSizeBytes { get; init; }

    public string? SourceObjectId { get; init; }
    public string? SourceActionId { get; init; }
    public DateTime CreatedAt { get; init; }
}
```

### 8.8 Debug 节点 I/O

#### 8.8.1 NodeIoTraceDto

```csharp
public sealed record MicroflowNodeIoTraceDto
{
    public string RunId { get; init; }
    public string ObjectId { get; init; }
    public string? ActionId { get; init; }

    public string NodeKind { get; init; }
    public string? ActionKind { get; init; }

    public string? IncomingFlowId { get; init; }
    public string? OutgoingFlowId { get; init; }

    public JsonElement InputVariables { get; init; }
    public JsonElement ActionInput { get; init; }
    public JsonElement EvaluatedExpressions { get; init; }

    public JsonElement OutputVariables { get; init; }
    public JsonElement VariableDelta { get; init; }
    public JsonElement HandoffPayload { get; init; }

    public JsonElement? SelectedCaseValue { get; init; }
    public JsonElement? LoopIteration { get; init; }

    public string Status { get; init; }
    public long DurationMs { get; init; }

    public MicroflowRuntimeErrorDto? Error { get; init; }
}
```

#### 8.8.2 Debug 面板展示

点击节点后显示：

```text
Node
  id
  caption
  kind
  actionKind
  status
  duration

Input
  variables
  action input
  expression input

Output
  variables
  action output
  variable delta

Flow
  incomingFlowId
  outgoingFlowId
  selectedCase

Transaction
  staged changes
  committed
  rolled back

Error
  code
  message
  details
```

### 8.9 表达式与变量引擎

#### 8.9.1 表达式编辑器

表达式编辑器必须支持：

1. 变量列表。
2. 变量类型。
3. 对象成员访问。
4. List 函数。
5. 字符串函数。
6. 数值比较。
7. 逻辑 AND / OR / NOT。
8. 括号分组。
9. 实时语法校验。
10. 类型推导。
11. Debug 值预览。
12. 表达式测试运行。

#### 8.9.2 表达式结构

```json
{
  "language": "mendix",
  "raw": "$contains5 == true && $listScore == 18",
  "expectedType": {
    "kind": "boolean"
  },
  "references": {
    "variables": ["contains5", "listScore"],
    "entities": [],
    "attributes": [],
    "functions": ["&&", "=="]
  },
  "diagnostics": []
}
```

#### 8.9.3 安全要求

1. 表达式不得执行任意 JS。
2. 表达式不得执行任意 C#。
3. 表达式不得访问文件系统。
4. 表达式不得访问环境变量。
5. 表达式不得绕过权限访问数据库。
6. 表达式必须由后端 DSL parser 执行。

### 8.10 Mendix Studio 交互增强

#### 8.10.1 画布布局

支持三种模式：

```text
默认模式
测试模式
沉浸模式
```

默认模式：

```text
左侧 App Explorer
中间画布
右侧属性面板
底部 Problems / Debug / Console
```

测试模式：

```text
左侧收起
右侧显示运行输入
底部显示 Debug Trace
画布高亮执行路径
```

沉浸模式：

```text
全屏画布
浮动 toolbar
浮动属性面板
可折叠 Debug 抽屉
可拖动小地图
```

#### 8.10.2 小地图

小地图必须支持：

1. 显示全部节点。
2. 显示 viewport 矩形。
3. 拖动 viewport。
4. 点击节点定位。
5. 错误节点高亮。
6. 当前执行节点高亮。
7. 当前执行路径高亮。
8. Loop 容器层级展示。
9. 大图 LOD 降级。
10. 可折叠 / 可隐藏。

当前前端已经有 `FlowGramMicroflowMiniMap` 基础实现，可在此基础上增强。

#### 8.10.3 测试输入

试运行输入必须支持结构化 JSON：

```json
{
  "numbers": [1, 2, 3, 4, 5, 6]
}
```

支持：

1. 保存测试样例。
2. 运行当前样例。
3. 运行全部样例。
4. 显示期望值。
5. 显示实际值。
6. 对比上次运行。
7. 一键复制 Debug trace。

## 9. 验收微流需求

### 9.1 验收微流名称

```text
MF_AllNodeComplexComputation_Test
```

### 9.2 输入参数

```text
numbers: List<Integer>
```

推荐输入：

```json
{
  "numbers": [1, 2, 3, 4, 5, 6]
}
```

### 9.3 期望输出

```text
120
```

### 9.4 计算公式

```text
列表链路 18
+ 循环链路 4
+ 对象链路 68
+ 网关链路 30
= 120
```

### 9.5 列表链路验收

#### 执行过程

```text
workList = []
append [6,1,3,2,5,4]
sort -> [1,2,3,4,5,6]
filter > 2 -> [3,4,5,6]
sum -> 18
contains 5 -> true
```

#### 覆盖节点

```text
创建列表
修改列表
排序列表
过滤列表
列表聚合
列表操作
Decision true 分支
```

#### Debug 必须展示

```text
input:
  workList = [1,2,3,4,5,6]
  filterExpression = "$item > 2"

output:
  filteredList = [3,4,5,6]
  listScore = 18
```

### 9.6 循环链路验收

#### 输入

```json
[1,2,3,4,5,6]
```

#### 规则

```text
遇到 2 continue
遇到 4 break
break 前其他数字累加
```

#### 期望

```text
1 + 3 = 4
```

#### Debug 必须展示

```text
iteration 0:
  item = 1
  loopScore = 1

iteration 1:
  item = 2
  decision = continue
  loopScore = 1

iteration 2:
  item = 3
  loopScore = 4

iteration 3:
  item = 4
  decision = break
  loopScore = 4
```

### 9.7 对象链路验收

#### 执行过程

```text
创建 Sales.Student
修改 student
转换 student -> member
对象类型决策命中 Sales.Student
提交对象
检索对象
创建临时对象 tempStudent
回滚临时对象
删除 student
```

#### 分数

```text
ObjectType 命中 Student: +30
Retrieve 成功: +20
Rollback 成功: +10
Delete 成功: +8
合计: 68
```

#### Debug 必须展示

```text
object state:
  new
  changed
  casted
  committed
  retrieved
  rolledBack
  deleted

transaction:
  dryRun = true
  rollbackAtEnd = true
```

### 9.8 网关链路验收

#### Parallel

```text
parallelA = 7
parallelB = 11
parallel result = 18
```

#### Inclusive

```text
inclusiveA = 5
inclusiveB = 7
inclusive result = 12
```

#### 合计

```text
18 + 12 = 30
```

#### Debug 必须展示

```text
parallel branch A executed
parallel branch B executed
parallel merge completed

inclusive branch A selected
inclusive branch B selected
inclusive merge completed
```

### 9.9 最终返回

```text
total = listScore + loopScore + objectScore + gatewayScore
total = 18 + 4 + 68 + 30
total = 120
```

End Event 返回：

```text
$total
```

## 10. 非功能需求

### 10.1 性能要求

| 场景 | 要求 |
| ---- | ---- |
| 100 节点微流 | 可稳定执行 |
| 1000 节点微流 | 不栈溢出 |
| 100 并发实例 | 变量不串扰 |
| ExecutionPlan | 可缓存复用 |
| Debug trace | 不阻塞主执行 |
| 大 List | 不全量重复复制 |
| 大对象 | 只保存引用和摘要 |
| 循环 | 有最大迭代次数 |
| 节点 | 支持节点级超时 |
| 运行 | 支持取消 |

### 10.2 内存控制

新增内存预算：

```csharp
public sealed class ExecutionMemoryBudget
{
    public long MaxContextBytes { get; init; }
    public long MaxVariableBytes { get; init; }
    public long MaxNodeOutputBytes { get; init; }
    public int MaxCollectionPreviewItems { get; init; }
    public int MaxLoopIterations { get; init; }
    public int MaxExecutionDepth { get; init; }
}
```

要求：

1. Production 默认只保存摘要。
2. Debug 可保存更多变量，但必须限流。
3. 大对象不得完整写入 trace。
4. 大文件不得进入 ExecutionContext。
5. HTTP 大响应必须摘要化或外部化。
6. 循环变量必须按作用域释放。
7. 并行分支不得无脑复制完整上下文。

### 10.3 事务要求

事务模式：

```csharp
public enum TransactionMode
{
    None,
    Required,
    RequiresNew,
    Suppress,
    ReadOnly
}
```

微流级配置：

```json
{
  "transactionStrategy": "SingleTransaction",
  "isolationLevel": "ReadCommitted",
  "timeoutSeconds": 30,
  "enableSavepoint": true,
  "rollbackOnUnhandledError": true
}
```

要求：

1. TestRun 默认 dry-run rollback。
2. 节点失败默认回滚。
3. Continue on Error 可配置是否回滚。
4. 子微流可继承父事务。
5. 子微流可独立事务。
6. Commit 节点提交范围明确。
7. Rollback 节点回滚范围明确。
8. 并行数据库操作不共享同一个 DbContext。
9. 支持乐观锁冲突识别。
10. 支持事务日志进入 Debug trace。

### 10.4 日志与可观测性

日志结构：

```csharp
public sealed class NodeExecutionLog
{
    public string RunId { get; init; }
    public string ObjectId { get; init; }
    public string NodeType { get; init; }

    public DateTime StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
    public long DurationMs { get; init; }

    public string Status { get; init; }
    public string? ErrorCode { get; init; }

    public long InputSizeBytes { get; init; }
    public long OutputSizeBytes { get; init; }

    public string? InputSummaryJson { get; init; }
    public string? OutputSummaryJson { get; init; }
}
```

要求：

1. 每个节点记录耗时。
2. 每个节点记录 incoming / outgoing flow。
3. 每个 Decision 记录 selected case。
4. 每个 Loop 记录 iteration。
5. 每个 Gateway 记录 branch 状态。
6. 失败时能定位到具体节点。
7. 前端画布能根据 trace 高亮执行路径。
8. 日志写入不得同步拖慢主流程。
9. 敏感字段必须脱敏。
10. 大对象必须摘要化。

## 11. API 需求

### 11.1 校验接口

```http
POST /api/microflows/{id}/validate
```

请求：

```json
{
  "schema": {},
  "mode": "testRun",
  "includeWarnings": true,
  "includeInfo": true
}
```

响应：

```json
{
  "issues": [
    {
      "code": "MF_DECISION_BOOLEAN_FALSE_MISSING",
      "severity": "error",
      "message": "Boolean Decision 缺少 false 分支。",
      "objectId": "decision-1",
      "relatedFlowIds": ["flow-1"],
      "fieldPath": "workflow.nodes.5.splitCondition"
    }
  ],
  "summary": {
    "errorCount": 1,
    "warningCount": 0,
    "infoCount": 0
  }
}
```

### 11.2 试运行接口

```http
POST /api/microflows/{id}/test-run
```

请求：

```json
{
  "inputs": {
    "numbers": [1, 2, 3, 4, 5, 6]
  },
  "options": {
    "debug": true,
    "captureNodeInput": true,
    "captureNodeOutput": true,
    "captureVariableDelta": true
  }
}
```

响应：

```json
{
  "runId": "run-xxx",
  "status": "succeeded",
  "result": 120,
  "nodeResults": [],
  "logs": [],
  "session": {}
}
```

### 11.3 Debug Trace 接口

```http
GET /api/microflow-runs/{runId}/trace
```

响应：

```json
{
  "runId": "run-xxx",
  "trace": [
    {
      "objectId": "filter-list-1",
      "actionKind": "filterList",
      "incomingFlowId": "flow-10",
      "outgoingFlowId": "flow-11",
      "input": {
        "sourceList": [1, 2, 3, 4, 5, 6],
        "expression": "$item > 2"
      },
      "output": {
        "filteredList": [3, 4, 5, 6]
      },
      "variablesSnapshot": {},
      "durationMs": 3,
      "status": "success"
    }
  ]
}
```

## 12. 前端验收标准

### 12.1 画布验收

1. 节点可以自由拖动。
2. 节点连接后仍可拖动。
3. 连线随节点稳定移动。
4. 节点不会抖动。
5. 节点可吸附网格。
6. 左侧节点可拖入画布。
7. 左侧节点可拖入 Loop Body。
8. Loop Header 和 Loop Body drop 行为区分明确。
9. 小地图可点击定位。
10. 小地图可拖动 viewport。
11. 沉浸模式可全屏编辑。
12. Debug 路径可在画布高亮。

### 12.2 分支验收

1. Boolean Decision 第一条线默认为 true。
2. Boolean Decision 第二条线默认为 false。
3. 不允许重复 true 分支。
4. 不允许重复 false 分支。
5. 缺少 true / false 时节点显示错误。
6. Problems 点击错误可定位节点。
7. 后端不再报 true / false missing。
8. Debug 可看到 selected case。

### 12.3 Loop 验收

1. Loop 外节点不能直接连接 Loop 内节点。
2. Loop 内节点不能直接连接 Loop 外节点。
3. Loop 内节点保存后 collectionId 正确。
4. Loop 内 flow 保存到 loop objectCollection.flows。
5. Break / Continue 只能在 Loop 内使用。
6. Loop Debug 可看到每次 iteration。
7. 空数组输入时 loopScore 为 0。
8. `[1,2,3,4,5,6]` 输入时 loopScore 为 4。

### 12.4 Gateway 验收

1. Parallel Gateway 两条分支都执行。
2. Parallel Merge 等待所有分支。
3. Inclusive Gateway 能选择多条分支。
4. Inclusive Merge 等待已选择分支。
5. 未选择分支 trace 标记 skipped。
6. 分支输出变量合并规则明确。
7. Debug 展示 branch input/output。
8. 网关链路最终输出 30。

## 13. 后端验收标准

### 13.1 Validator 验收

1. 非法跨 collection flow 必须报错。
2. 合法 Loop 内 flow 不报错。
3. Boolean Decision 缺 true 报错。
4. Boolean Decision 缺 false 报错。
5. Boolean Decision true / false 完整时不报错。
6. Gateway 节点类型被识别。
7. Break / Continue 不在 Loop 内时报错。
8. ParameterObject 不允许参与 SequenceFlow。
9. AnnotationFlow 不参与 Runtime 控制流。
10. 所有错误必须带 objectId / flowId / fieldPath。

### 13.2 Runtime 验收

1. Start → End 可稳定执行。
2. ActionActivity 可执行。
3. Decision 可按 caseValues 选择路径。
4. Loop 可执行 break / continue。
5. Parallel Gateway 可执行多分支。
6. Inclusive Gateway 可执行多分支。
7. Object Type Decision 可按对象类型选择路径。
8. 对象操作 dry-run 不污染数据库。
9. End Event 返回 `$total`。
10. 输入 `[1,2,3,4,5,6]` 返回 120。
11. 输入 `[]` 返回 116。

### 13.3 Debug 验收

1. 每个节点有 trace frame。
2. 每个节点有 input。
3. 每个节点有 output。
4. 每个节点有 variable delta。
5. 每个 Decision 有 selected case。
6. 每个 Loop 有 iteration。
7. 每个 Gateway 有 branch trace。
8. 错误节点可定位。
9. 前端可根据 trace 高亮执行路径。
10. Trace 大小受控，不保存超限大对象。

## 14. 测试计划

### 14.1 单元测试

| 测试项 | 文件建议 |
| ---- | ---- |
| Boolean Decision case repair | `microflow-decision-case-repair.test.ts` |
| FlowGram empty caseValues 修复 | `flowgram-edge-mapping.test.ts` |
| 跨 collection flow 阻止 | `edge-registry-loop-boundary.test.ts` |
| addFlow collection 归属 | `authoring-operations-flow-collection.test.ts` |
| splitFlowWithObject collection 保持 | `authoring-operations-split-flow.test.ts` |
| SchemaReader caseValues 读取 | `MicroflowSchemaReaderTests.cs` |
| Validator decision branches | `MicroflowValidationDecisionTests.cs` |
| Validator loop boundary | `MicroflowValidationLoopBoundaryTests.cs` |
| Runtime gateway execution | `MicroflowRuntimeGatewayTests.cs` |
| Node IO trace | `MicroflowRuntimeDebugTraceTests.cs` |

### 14.2 集成测试

1. 创建包含 Boolean Decision 的微流，保存、校验、运行。
2. 创建包含 Loop 的微流，验证非法跨 collection flow 被阻止。
3. 创建包含 Parallel Gateway 的微流，验证两条分支都执行。
4. 创建包含 Inclusive Gateway 的微流，验证多分支选择。
5. 创建对象生命周期微流，验证 dry-run rollback。
6. 创建 Debug 运行，验证 trace input/output/delta 完整。

### 14.3 E2E 测试

路径：

```text
/space/:spaceId/mendix-studio/:appId
```

用例：

1. 打开 Mendix Studio。
2. 新建微流。
3. 拖入 Start / Action / Decision / Loop / Gateway / End。
4. 创建 Boolean Decision true / false 分支。
5. 创建 Loop Body。
6. 尝试非法跨 Loop 连线，确认被前端阻止。
7. 输入测试参数。
8. 点击校验。
9. 点击试运行。
10. 验证结果为 120。
11. 打开 Debug 面板。
12. 点击每个节点查看 input/output。
13. 小地图定位执行路径。
14. 切换沉浸模式。
15. 保存、刷新、重新打开，结构不丢失。

## 15. 实施计划

### 阶段一：P0 校验阻断修复

目标：解决当前试运行被后端校验阻止的问题。

任务：

1. 修复 `caseValues: []` 覆盖默认 case。
2. 修复 Boolean Decision true / false 自动补齐。
3. 修复 `addFlow` 跨 collection 默认落 root 的问题。
4. 修复 `splitFlowWithObject` 错误操作 root flows 的问题。
5. 后端 Validator issue 增强 objectId / flowId / fieldPath。
6. 增加前后端单元测试。

验收：

```text
MF_FLOW_INVALID_TARGET 不再由前端错误结构触发
MF_DECISION_BOOLEAN_TRUE_MISSING 可准确定位并可一键修复
MF_DECISION_BOOLEAN_FALSE_MISSING 可准确定位并可一键修复
```

### 阶段二：ExecutionPlan-first Runtime

目标：把 Runtime 执行入口改为 ExecutionPlan 优先。

任务：

1. 增加稳定 PlanId。
2. 增加 ExecutionPlanCache。
3. 编译 normal / decision / loop / gateway / error flow。
4. Runtime 优先使用 ExecutionPlan。
5. ExecutionPlan 编译失败时返回结构化 diagnostics。
6. 增加缓存失效测试。

验收：

```text
同一 schema 多次试运行复用 ExecutionPlan
修改 schema 后缓存失效
ExecutionPlan diagnostics 可展示到 Problems 面板
```

### 阶段三：Gateway 真执行

目标：Parallel / Inclusive Gateway 真正执行。

任务：

1. Validator 支持 gateway kind。
2. ExecutionPlan 编译 gateway。
3. Runtime 实现 parallel fork / join。
4. Runtime 实现 inclusive branch selection。
5. 增加分支变量作用域。
6. 增加分支 merge 策略。
7. 增加 gateway trace。

验收：

```text
parallelA = 7
parallelB = 11
inclusiveA = 5
inclusiveB = 7
gatewayScore = 30
```

### 阶段四：Debug Node I/O

目标：每个节点可观测。

任务：

1. 增加 NodeIoTraceDto。
2. Runtime 执行前采集 input。
3. Runtime 执行后采集 output。
4. 增加 variable delta。
5. 增加 expression evaluated values。
6. 增加 handoff payload。
7. 前端 Debug 面板展示。
8. 画布执行路径高亮。

验收：

```text
点击任意节点可以看到 input/output/delta
Decision 可以看到 selected case
Loop 可以看到 iteration
Gateway 可以看到 branch trace
```

### 阶段五：Mendix Studio 体验增强

目标：接近 Mendix Studio Pro 桌面建模体验。

任务：

1. 优化响应式布局。
2. 增加测试模式。
3. 增强沉浸模式。
4. 重做小地图交互。
5. 优化属性面板。
6. 增强表达式编辑器。
7. 增加测试样例管理。
8. 增加 Problems 定位能力。

验收：

```text
长链路微流可顺畅查看、定位、调试
小地图可拖动 viewport
测试输入和 Debug trace 不遮挡画布
```

## 16. 风险与约束

| 风险 | 说明 | 应对 |
| ---- | ---- | ---- |
| 前端 FlowGram 与 Authoring Schema 双模型不同步 | 容易丢 caseValues / collectionId | 增加 Schema Normalizer 和转换测试 |
| 后端 Validator 与 Runtime 规则不一致 | 校验过了但运行失败 | Validator 与 ExecutionPlan 共用语义模型 |
| Gateway 并发导致变量冲突 | 多分支写同名变量 | 分支 Scope 隔离 + merge 策略 |
| Debug Trace 过大 | 大对象进入日志拖慢运行 | 摘要化 + 大小限制 + Debug 模式开关 |
| 事务语义复杂 | 对象链路 dry-run / commit / rollback 易混乱 | UnitOfWork + TransactionSummary |
| 表达式执行安全风险 | Raw 表达式不可直接执行任意代码 | 后端 DSL parser + 类型校验 |
| 小地图大图性能问题 | 节点过多卡顿 | LOD + 虚拟化 + 节流 |

## 17. 最终交付物

| 交付物 | 说明 |
| ---- | ---- |
| 前端连线规则修复 | 禁止非法跨 collection flow |
| Decision 分支修复 | true / false 稳定 caseValues |
| Schema Normalizer | 保存 / 校验 / 运行前统一修复 |
| 后端 Validator 增强 | 精确 objectId / flowId / fieldPath |
| ExecutionPlan Cache | 稳定缓存与失效 |
| Gateway Runtime | parallel / inclusive 真执行 |
| Debug Node I/O | input / output / delta / handoff |
| 表达式编辑器增强 | 变量选择、复合判断、调试预览 |
| 小地图增强 | viewport 拖动、执行路径、错误高亮 |
| 测试样例管理 | 输入、期望、实际、回归 |
| 单元测试 | 前端 + 后端 |
| 集成测试 | Validation + Runtime |
| E2E 测试 | Mendix Studio 页面完整链路 |

## 18. 总体验收标准

最终必须达到：

```text
用户在 Mendix Studio 画布上创建的微流，
保存后不会因为前端 Schema 结构错误被后端拒绝；
校验错误可以准确定位到节点或连线；
试运行可以真实执行每个节点；
Debug 可以看到每个节点的输入、输出和变量变化；
复杂节点族验收微流 MF_AllNodeComplexComputation_Test 输入 [1,2,3,4,5,6] 稳定返回 120。
```

核心验收命令：

```json
{
  "numbers": [1, 2, 3, 4, 5, 6]
}
```

期望结果：

```json
120
```

空数组验收：

```json
{
  "numbers": []
}
```

期望结果：

```json
116
```

这两个结果必须同时成立，且 Debug Trace 能解释每一分的来源。
