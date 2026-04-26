# MicroflowAuthoringSchema 契约

## 权威类型

TypeScript 定义见 `@atlas/microflow`（`schema/types.ts`）中的 `MicroflowAuthoringSchema`。

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
| `debug` | 可选；运行/调试，**不**作为业务持久化主数据 |
| `editor` | 编辑器视口/选择，非业务语义 |
| `audit` | 审计/版本元数据 |

## 业务主模型

- **唯一主模型**：`MicroflowAuthoringSchema`（及含 `version` 的 `MicroflowSchema` 草稿包装）。
- **禁止**：将 FlowGram `WorkflowJSON` 作为业务主 schema 落库；FlowGram 仅由 `authoringToFlowGram` 派生。

## 破坏性变更风险

- 重命名/删除 `objectId`/`flowId` 稳定键将导致引用与发布快照失效。
- 将 `flows` 迁回仅挂在 `objectCollection.flows` 而根级 `flows` 为空，会破坏与现有校验/编辑器一致假设（当前实现以**根级** `schema.flows` 为准）。

## 后端存储建议

- 以 **AuthoringSchema JSON** 为真相源；发布时再存 **只读 snapshot JSON**。
- 资源行级元数据（名称、工作区、标签、权限）可拆表，与 `schemaId` 关联。
