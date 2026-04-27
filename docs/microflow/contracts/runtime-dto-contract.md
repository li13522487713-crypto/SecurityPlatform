# Runtime DTO 第一版

## 权威来源

- **核心结构**：`MicroflowRuntimeDto`（`@atlas/microflow/schema`）。
- **工厂函数**：`toRuntimeDto(schema: MicroflowAuthoringSchema)`（`@atlas/microflow/adapters` → `microflow-adapters`）。

## 内容

- 含 `microflowId`、`schemaVersion`、`name`、`returnType`、`parameters`、**objectCollection**、**flows**、**variables**（VariableIndex）。
- `flows` 是 Runtime control-flow 列表，只包含 `SequenceFlow` 语义：`sequence`、`decisionCondition`、`objectTypeCondition`、`errorHandler`；`AnnotationFlow` 不进入该列表。
- decision/object type flow 必须保留 `caseValues`；error handler flow 必须保留 `isErrorHandler=true`、origin/destination 与 connectionIndex。
- 含 **`p0RuntimeActionBlocks`**：与 P0 动作一一对应的强类型块（`MicroflowDiscriminatedRuntimeP0ActionDto`），无 `editor` / 设计器元数据；解析失败时含 `supportLevel: "error"` 与 `MF_P0_MALFORMED` 语义。
- **不含** FlowGram / WorkflowJSON。
- 第 27 轮起，`variables.all` 是后端 Runtime 静态变量声明的权威来源；若 AuthoringSchema 上只有旧 buckets 而无 `all`，`toRuntimeDto` 会重新构建 v2 VariableIndex。

## 补充契约类型

`@atlas/mendix-studio-core` 的 `contracts/runtime-dto-contract.ts` 提供 `MicroflowRuntimeParameterDto`、`MicroflowRuntimeActionDto` 等 **文档化/映射辅助** 类型，便于后端与 P0 动作对齐；全量动作载荷仍以 Authoring 侧 `MicroflowAction` 并集为准。

## P0 动作

retrieve、createObject、changeMembers、commit、delete、rollback、createVariable、changeVariable、callMicroflow、restCall、logMessage — 由执行器按 `action.kind` 消费；未覆盖动作为 `modeledOnly` / executor `unsupported` 策略（与节点注册表一致）。

## 第 27 轮变量声明

- `variables` 包含 `all`、`byName`、`byObjectId`、`byActionId`、`byCollectionId`、`byScopeKey`、`diagnostics`、`graphAnalysis`、`schemaId`、`metadataVersion`。
- 每个变量包含 `name`、`displayName`、`kind`、`dataType`、`source`、`scope`、`visibility`、`readonly`、`availableFromObjectId`、`availableInCollectionId`、branch/loop/error handler 标识与 diagnostics。
- `visibility` 取值为 `definite` / `maybe` / `unavailable`；Runtime 执行前可用该字段生成后端变量槽位和发布期 warning。
- ExecutionPlan 会从 DTO.variables 派生 `variableDeclarations`、`actionOutputs`、`loopVariables`、`systemVariables`、`errorContextVariables`、`variableScopes`、`variableDiagnostics`。
- 第 29 轮起，ExecutionPlan 同时提供 `normalFlows`、`decisionFlows`、`errorHandlerFlows` 分组；`flows` 仍为 control-flow 超集，不含 AnnotationFlow。
- 第 30 轮起，`toExecutionPlan` 的权威实现下沉到 `@atlas/microflow/runtime`，studio-core 契约层只做再导出，避免 Mock Runner 与契约验证使用两套 Plan 结构。

## 第 26 轮字段对齐

- `retrieve` DTO 包含 `retrieveSource`、`outputVariableName`、`errorHandlingType`；source 内保留 database/association、sort、range 的完整结构。
- `createObject` / `changeMembers` DTO 保留 entity/target、memberChanges、commit、validateObject，字段与属性面板逐项同名。
- `callMicroflow` DTO 保留 target、metadata 驱动的 `parameterMappings`、`returnValue`、`callMode`。
- `restCall` DTO 保留 request method/url/headers/query/body、response handling/status/headers、timeoutSeconds。
- `logMessage` DTO 保留 level、logNodeName、template.text、template.arguments、includeContextVariables、includeTraceId。
