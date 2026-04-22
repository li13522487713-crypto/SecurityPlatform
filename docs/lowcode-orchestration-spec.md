# 低代码编排哲学与有状态运行规格（lowcode-orchestration-spec）

> 状态：M19 + M20 落地。
> 范围：M19 工作流父级工程能力 + M20 节点 49 全集 + 双哲学 + 节点级状态。

## 1. 两种编排哲学

### 1.1 显式模式（explicit）
- 现有 `DagExecutor` 完整支持。
- 节点目录全量展示；用户在 Studio 中手动连线。
- 适用于流程清晰的标准工作流。

### 1.2 模型自决模式（agentic）
- 引擎入口：当前统一桥接到 Coze workflow 执行服务；配置中带有 `orchestration=agentic` 时改写为 LLM tool calling 协议。
- Studio 隐藏中间链路，仅暴露 LLM 节点 + Tool 池配置。
- 执行轨迹仍落 `RuntimeTrace`（M13），便于调试。
- 切换器：`POST /api/v2/workflows/orchestration/plan` → `OrchestrationPlan`。

### 1.3 统一切换 API
- `IDualOrchestrationEngine.Plan(canvasJson, mode, tools)` → `OrchestrationPlan { mode, canvasJson, tools, metadataJson }`。
- 前端 `lowcode-workflow-adapter/orchestration` 与本服务对齐。

## 2. 有状态工作流节点状态作用域

`INodeStateStore` 4 类作用域：

| 作用域 | scopeKey 含义 | 用例 |
| --- | --- | --- |
| `session` | sessionId | chatflow 会话内短期变量、表单填写进度 |
| `conversation` | conversationId | conversation 内消息上下文摘要 |
| `trigger` | triggerId | CRON 触发器上一次结果 |
| `app` | appId | 应用级缓存 / 配置项 |

操作：`ReadAsync` / `WriteAsync` / `DeleteAsync`；按 `(tenantId, scope, scopeKey, nodeKey)` 唯一索引。

存储实体：`Atlas.Domain.LowCode.Entities.NodeStateEntry`（已加入 schema catalog）。

## 3. M20 节点 49 全集映射

| Coze 上游 ID | Atlas 枚举 | 节点 Key | 类别 |
| --- | --- | --- | --- |
| 11 | `Variable` | `Variable` | data |
| 14 | `ImageGenerate` | `ImageGenerate` | image |
| 15 | `Imageflow` | `Imageflow` | image |
| 16 | `ImageReference` | `ImageReference` | image |
| 17 / 23 | `ImageCanvas` | `ImageCanvasUpstream` | image |
| 24 | `SceneVariable` | `SceneVariable` | data |
| 25 | `SceneChat` | `SceneChat` | ai |
| 26 | `LtmUpstream` | `LtmUpstream` | knowledge（与 Atlas 私有 Ltm(62) 联动）|
| Atlas 64 | `MemoryRead` | `MemoryRead` | knowledge |
| Atlas 65 | `MemoryWrite` | `MemoryWrite` | knowledge |
| Atlas 66 | `MemoryDelete` | `MemoryDelete` | knowledge |
| Atlas 44 | `ImageGeneration` | `ImageGeneration` | image |
| Atlas 45 | `Canvas` | `Canvas` | image |
| Atlas 46 | `ImagePlugin` | `ImagePlugin` | image |
| Atlas 47 | `VideoGeneration` | `VideoGeneration` | video |
| Atlas 48 | `VideoToAudio` | `VideoToAudio` | video |
| Atlas 49 | `VideoFrameExtraction` | `VideoFrameExtraction` | video |

加上 M12 触发器 34/35/36 + 既有 35+ 节点，节点目录 ≥ 49 完整覆盖。

## 4. 反例

- 把 `session` 状态当 `app` 共享 —— 违反作用域隔离；M20 `INodeStateStore.WriteAsync` 不允许 `scope` 字段以外取值。
- agentic 模式调用未注册的 tool —— 由当前 workflow 执行服务在执行期拒绝。
