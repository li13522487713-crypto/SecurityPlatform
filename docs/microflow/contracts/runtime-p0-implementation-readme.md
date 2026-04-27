# 前端 P0 强类型实现（第 25 轮）

- **源类型**：`@atlas/microflow/schema` 中各 `Microflow*Action`（P0）与 `MicroflowGenericAction`（排除 P0 kind）。
- **映射**：`mapAuthoringP0ToRuntimeBlocks` / `tryMapP0ActionToDiscriminatedDto`（`@atlas/microflow/runtime`）。
- **校验**：`p0-action-guards` + `validate-actions` 错误码 `MF_ACTION_P0_MUST_BE_STRONGLY_TYPED` 等。
- **样例**：`verifyMicroflowContracts()` 会检查 `p0RuntimeActionBlocks` 存在性与 supportLevel 一致性。

## 第 26 轮产品化补充

- P0 表单入口仍集中在 `ActionActivityForm`，通用输入能力沉淀到 `property-panel/common`，包括 `FieldRow`、`OutputVariableEditor`、`VariableNameInput`、`ErrorHandlingEditor`。
- Metadata/Variable/Expression 分别通过 Selector、VariableIndex/VariableSelector、ExpressionEditor 接入，不从 app-web 或 mock metadata 承载核心逻辑。
- FlowGram subtitle 从 Authoring action 派生，覆盖 REST method/url、Log level、CallMicroflow target 与 P0 输出变量。
- Contract verify 增加 P0 runtime block、fieldPath 契约和输出变量进入 VariableIndex 的检查。

## 第 28 轮表达式与校验补充

- P0 表达式字段统一走 microflow 包内 `ExpressionEditor`、`parseExpression`、`inferExpressionType`、`validateExpression`。
- 支持变量、对象属性、literal、comparison、and/or/not、`empty()`、`if then else` 与 enumeration value 第一版。
- `validateActions` 覆盖 P0 必填字段、Rest header/query/body、CallMicroflow 参数与 void return storeResult。
- `validateExpressions` 覆盖 Retrieve custom range、REST form body、LogMessage arguments 等第 27 轮遗漏字段。
- modeledOnly / requiresConnector / nanoflowOnly 进入 Validator，并按 edit/save/publish/testRun 模式调整 severity。

## 第 29 轮 Flow 协议补充

- P0 Runtime 只消费 `SequenceFlow` control flow；`AnnotationFlow` 仅用于编辑器说明与导出展示，不进入 ExecutionPlan control flows。
- Decision/ObjectType 分支必须从 `caseValues` 读取，不得从 FlowGram port label 推断；`noCase` 表示 pending，publish/testRun 必须阻断。
- ErrorHandler 必须由 `isErrorHandler=true` 与 `editor.edgeKind="errorHandler"` 同时表达；P0 每个 source object 最多一个。
- AutoLayout、validation sync、runtime highlight 只能更新视图/状态，不得修改 flow semantic hash。

## 第 30 轮 Mock Runtime 补充

- `mockRunExecutionPlan(plan, input)` 是 Mock Runner 主入口；旧 `mockTestRunMicroflow(schema)` 只做 validate 与 DTO/Plan 转换。
- P0 mock 执行以 `MicroflowExecutionNode.p0ActionRuntime` 为准，不再从 AuthoringSchema 扫 action 执行。
- Decision/ObjectType 分支只从 `plan.decisionFlows[].caseValues` 选择。
- RestCall `simulateRestError=true` 使用 `plan.errorHandlerFlows` 进入错误路径，并在 trace 中标记 `errorHandlerVisited`。
- Unsupported/modeledOnly 到达时产生 `RUNTIME_UNSUPPORTED_ACTION` 或 `RUNTIME_CONNECTOR_REQUIRED`。

## 第 40 轮后端 Validation 补充

- 后端 `MicroflowValidationService` 已实现 P0 action 必填字段、metadata reference、基础变量和基础表达式校验，可作为保存/发布/运行前权威校验入口。
- 当前后端仍不执行 Runtime，也不实现完整表达式执行器；`testRun` mode 会把 unsupported/modeledOnly 等运行前阻断问题返回为 error，供第 42 轮 TestRun Mock API 复用。

## 第 42 轮后端 Mock Runtime 补充

- 后端 TestRun 已从 skeleton 升级为契约级 Mock Runner：输入是 `MicroflowAuthoringSchema` 或当前保存 schema，输出是 `MicroflowRunSession`、`MicroflowTraceFrame`、`RuntimeLog` 与变量快照。
- P0 mock 覆盖 Start/End、Decision、ObjectTypeDecision、Merge、Loop、Break、Continue、ErrorHandler flow，以及 retrieve/create/change/commit/delete/rollback/createVariable/changeVariable/callMicroflow/restCall/logMessage 等 action 的模拟输出。
- `simulateRestError` 只模拟 REST 失败和 `$latestError` / `$latestHttpResponse`，不发送真实 HTTP；Retrieve/Commit/Delete 只生成 mock output，不访问业务数据库。
- max steps 默认 500，超过后返回 `RUNTIME_MAX_STEPS_EXCEEDED` failed session。
- 第 48 轮真实 Runtime 应从 `ExecutionPlanLoader` 接管导航与执行，但沿用本轮 DTO、错误码和持久化表。

## 第 46～47 轮前后端 Debug 联调

- HTTP RuntimeAdapter 不回退前端 mock runner；`testRunMicroflow` 之后回读 `GET /runs/{runId}` 与 `/trace`。
- DebugPanel 展示后端 RunSession / TraceFrame / RuntimeLog / VariableSnapshot / RuntimeError，Cancel Run 后再次回读持久化状态。
- FlowGram runtime highlight 使用后端 `objectId/outgoingFlowId/selectedCaseValue/loopIteration/errorHandlerVisited`，不修改 AuthoringSchema 或 dirty。
