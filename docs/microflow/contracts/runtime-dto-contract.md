# Runtime DTO 第一版

## 权威来源

- **核心结构**：`MicroflowRuntimeDto`（`@atlas/microflow/schema`）。
- **工厂函数**：`toRuntimeDto(schema: MicroflowAuthoringSchema)`（`@atlas/microflow/adapters` → `microflow-adapters`）。

## 内容

- 含 `microflowId`、`schemaVersion`、`name`、`returnType`、`parameters`、**objectCollection**、**flows**、**variables**（VariableIndex）。
- **不含** FlowGram / WorkflowJSON。

## 补充契约类型

`@atlas/mendix-studio-core` 的 `contracts/runtime-dto-contract.ts` 提供 `MicroflowRuntimeParameterDto`、`MicroflowRuntimeActionDto` 等 **文档化/映射辅助** 类型，便于后端与 P0 动作对齐；全量动作载荷仍以 Authoring 侧 `MicroflowAction` 并集为准。

## P0 动作

retrieve、createObject、changeMembers、commit、delete、rollback、createVariable、changeVariable、callMicroflow、restCall、logMessage — 由执行器按 `action.kind` 消费；未覆盖动作为 `modeledOnly` / executor `unsupported` 策略（与节点注册表一致）。
