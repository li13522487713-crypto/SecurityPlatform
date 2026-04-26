# MicroflowAuthoringSchema 契约

## 权威类型

TypeScript 定义见 `@atlas/microflow`（`schema/types.ts`）中的 `MicroflowAuthoringSchema`。

**P0 动作**（retrieve … logMessage 共 11 种）在 `ActionActivity.action` 上必须为**强类型** `Microflow*Action` 成员，不得落在 `MicroflowGenericAction`；P1/P2 可使用 Generic（kind 已排除 P0）。

## 必选顶层字段（冻结）

| 字段 | 说明 |
|------|------|
| `schemaVersion` | 语义化版本字符串 |
| `mendixProfile` | `mx10` \| `mx11` |
| `id` / `stableId` | 资源与同步主键 |
| `name` / `displayName` | 技术名与展示名 |
| `description` / `documentation` | 可选说明 |
| `moduleId` / `moduleName` | 模块归属 |
| `parameters` | 与 ParameterObject 并存；入参定义 |
| `returnType` / `returnVariableName` | 返回值 |
| `objectCollection` | 含嵌套 Loop 的 `MicroflowObject` 树 |
| `flows` | 仅 `SequenceFlow` / `AnnotationFlow`（决策/对象类型/错误线由 `SequenceFlow` 表达） |
| `security` / `concurrency` / `exposure` | 安全、并发、暴露 |
| `variables` | 可选；通常为 VariableIndex 派生缓存 |
| `validation` | 校验问题列表状态 |
| `debug` | 可选；编辑器内调试/高亮快照，**不**作为业务语义主数据（见下「调试 trace 类型」） |
| `editor` | 编辑器视口/选择，非业务语义 |
| `audit` | 审计/版本元数据 |

## 业务主模型

- **唯一主模型**：`MicroflowAuthoringSchema`。资源/发布快照中的语义版本请使用 `audit.version` 与资源行 `version` 字段；**不再**在 schema 顶层使用已废弃的 `version` 字段。
- **Legacy**：旧 demo 的 `nodes`/`edges` 图仅能通过 `normalizeMicroflowSchema` / `migrateLegacyMicroflowSchema`（`@atlas/microflow/schema/legacy`）迁入 Authoring；不得作为 Runtime、校验或 FlowGram 的持久化输入。
- **禁止**：将 FlowGram `WorkflowJSON` 作为业务主 schema 落库；FlowGram 仅由 `authoringToFlowGram` 派生。

## 调试 trace 类型（与运行时区分）

- **Authoring 内可选持久化**：`debug.traceFrames` / `debug.lastTrace` 的元素类型为 **`MicroflowAuthoringPersistedTraceFrame`**（定义在 `schema/types.ts`）。用于会话内 FlowGram/面板高亮等，形状刻意兼容 FlowGram 叠加层，但**不是**后端 test-run 返回的权威 trace DTO。
- **运行时 / API**：执行轨迹以 **`MicroflowTraceFrame`** 为准（`@atlas/microflow/debug`，`trace-types.ts`）；`authoringToFlowGram` 等可同时接受持久化帧与运行时帧的并集，便于用 `lastTrace` 或本次运行的 `frames` 驱动同一套叠加逻辑。
- 详见 [runtime-trace-contract.md](./runtime-trace-contract.md)。

## 破坏性变更风险

- 重命名/删除 `objectId`/`flowId` 稳定键将导致引用与发布快照失效。
- 将 `flows` 迁回仅挂在 `objectCollection.flows` 而根级 `flows` 为空，会破坏与现有校验/编辑器一致假设（当前实现以**根级** `schema.flows` 为准）。

## 后端存储建议

- 以 **AuthoringSchema JSON** 为真相源；发布时再存 **只读 snapshot JSON**。
- 资源行级元数据（名称、工作区、标签、权限）可拆表，与 `schemaId` 关联。
