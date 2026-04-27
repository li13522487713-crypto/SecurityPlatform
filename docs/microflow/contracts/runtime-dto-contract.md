# Runtime DTO 第一版

## 权威来源

- **核心结构**：`MicroflowRuntimeDto`（`@atlas/microflow/schema`）。
- **工厂函数**：`toRuntimeDto(schema: MicroflowAuthoringSchema)`（`@atlas/microflow/adapters` → `microflow-adapters`）。

## 内容

- 含 `microflowId`、`schemaVersion`、`name`、`returnType`、`parameters`、**objectCollection**、**flows**、**variables**（VariableIndex）。
- 含 **`p0RuntimeActionBlocks`**：与 P0 动作一一对应的强类型块（`MicroflowDiscriminatedRuntimeP0ActionDto`），无 `editor` / 设计器元数据；解析失败时含 `supportLevel: "error"` 与 `MF_P0_MALFORMED` 语义。
- **不含** FlowGram / WorkflowJSON。

## 补充契约类型

`@atlas/mendix-studio-core` 的 `contracts/runtime-dto-contract.ts` 提供 `MicroflowRuntimeParameterDto`、`MicroflowRuntimeActionDto` 等 **文档化/映射辅助** 类型，便于后端与 P0 动作对齐；全量动作载荷仍以 Authoring 侧 `MicroflowAction` 并集为准。

## P0 动作

retrieve、createObject、changeMembers、commit、delete、rollback、createVariable、changeVariable、callMicroflow、restCall、logMessage — 由执行器按 `action.kind` 消费；未覆盖动作为 `modeledOnly` / executor `unsupported` 策略（与节点注册表一致）。

## 第 26 轮字段对齐

- `retrieve` DTO 包含 `retrieveSource`、`outputVariableName`、`errorHandlingType`；source 内保留 database/association、sort、range 的完整结构。
- `createObject` / `changeMembers` DTO 保留 entity/target、memberChanges、commit、validateObject，字段与属性面板逐项同名。
- `callMicroflow` DTO 保留 target、metadata 驱动的 `parameterMappings`、`returnValue`、`callMode`。
- `restCall` DTO 保留 request method/url/headers/query/body、response handling/status/headers、timeoutSeconds。
- `logMessage` DTO 保留 level、logNodeName、template.text、template.arguments、includeContextVariables、includeTraceId。
