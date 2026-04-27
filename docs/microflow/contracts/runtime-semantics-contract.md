# Runtime Semantics Contract v2

## 执行图

- Runtime 语义以 `MicroflowAuthoringSchema.objectCollection`、嵌套 Loop collection 与 `MicroflowFlow` 为准。
- 第 30 轮起，前端 Mock Runner 的唯一执行输入是 `MicroflowExecutionPlan`；旧 schema test-run 入口仅负责 `toRuntimeDto → toExecutionPlan → mockRunExecutionPlan` 转换。
- `AnnotationFlow` 不参与执行图。
- `ErrorHandlerFlow` 不参与 normal graph，只参与 error handler scope 与运行时异常跳转。
- Loop collection 独立建图，外部变量可进入 Loop，Loop 内变量默认不回流外部主路径。
- Runtime DTO / ExecutionPlan 必须保留 `caseValues` 与 `isErrorHandler`，但不得依赖 FlowGram JSON 或视觉顺序执行。
- ExecutionPlan 将 control flow 分为 `normalFlows`、`decisionFlows`、`errorHandlerFlows`；AnnotationFlow 可用于编辑器导出元数据，但不进入这些分组。
- AutoLayout 只能改对象坐标/容器尺寸/可视 routing，必须保持 flow semantic hash 不变：origin、destination、connectionIndex、edgeKind、caseValues、isErrorHandler、kind 不可改变。
- Runtime Trace / RunSession 只使用 objectId、flowId、actionId、collectionId 定位，不得携带 FlowGram JSON。

## 变量语义

- Runtime DTO 必须携带 `variables: MicroflowVariableIndex`。
- ExecutionPlan 必须携带 `variableDeclarations`、`actionOutputs`、`loopVariables`、`systemVariables`、`errorContextVariables`、`variableScopes` 与 `variableDiagnostics`。
- 变量 declaration 只包含 Runtime 需要的信息：name、dataType、source、scope、readonly、objectId/actionId/flowId/loopObjectId；不包含 UI-only 信息或 FlowGram JSON。

## Branch / Merge

- Decision 分支变量在兄弟分支不可见。
- Merge 后若变量不是所有 incoming path 都 definite，则为 `maybe`。
- 同名变量类型不一致必须进入 diagnostic，后端实现可按 save/publish 策略升级为阻断。

## Error Handler

- `$latestError` 在所有 custom error handler scope 内可见。
- RestCall error handler 额外暴露 `$latestHttpResponse`。
- WebService error handler 额外暴露 `$latestSoapFault`。
- Error handler 中声明的变量默认不回流 normal path。
- P0 每个 source object 最多一个 error handler flow；rollback/continue/custom 模式与 error flow 的一致性由 Validator 阻断。

## 第 49 轮 FlowNavigator 语义

FlowNavigator 是真实 Runtime 引擎的控制流导航层，仅消费 `MicroflowExecutionPlan`。本轮只做可预测导航和 trace skeleton，不执行真实 action、表达式、变量存取、事务、数据库 CRUD、REST 或 CallMicroflow。

- StartEvent：从 `plan.startNodeId` 或 `preferredStartNodeId` 出发，缺失返回 `RUNTIME_START_NOT_FOUND`。
- EndEvent：作为 success terminal，`terminalNodeId` 指向 EndEvent。
- ErrorEvent：作为 failed terminal，返回 `RUNTIME_ERROR_EVENT_REACHED`。
- SequenceFlow：只选择 normal outgoing flow，AnnotationFlow 不参与，ErrorHandlerFlow 只在错误上下文进入。
- ExclusiveMerge：到达即继续，不等待所有 incoming 分支。
- Decision：Boolean 使用 `decisionBooleanResult`，缺省按 true 并产生 warning diagnostic；Enumeration 使用 `enumerationCaseValue` 或 deterministic fallback；无匹配返回 `RUNTIME_INVALID_CASE`。
- ObjectType Decision：使用 `objectTypeCase` 匹配 `entityQualifiedName`；未提供时优先 fallback / empty / first inheritance；不解析实体继承树。
- ActionActivity：P0 supported action 只生成 placeholder success step；modeledOnly 可在 dry-run 中 skip；unsupported、requiresConnector、nanoflowOnly 产生导航错误。
- ErrorHandlerFlow：仅在 simulated action failure / RestCall error 等错误上下文进入；本轮不实现 rollback / continue / custom 事务语义。
- Loop / Break / Continue：Loop 按 `loopIterations` 模拟骨架迭代；Break 退出 loop，Continue 进入下一次迭代；二者在 loop 外返回失败。
- maxSteps：默认 500，超过返回 `RUNTIME_MAX_STEPS_EXCEEDED` 与 `maxStepsExceeded` status。

## 第 50 轮 VariableStore 语义

VariableStore 是真实 Runtime 的变量读写地基，位于 `ExecutionPlan -> FlowNavigator -> RuntimeExecutionContext -> VariableStore -> VariableSnapshot -> TraceFrame.variablesSnapshot` 链路中。

- 参数变量从 `ExecutionPlan.parameters` 初始化，默认 readonly；缺 required input 产生 diagnostic，optional 缺失为 `null`，额外 input 产生 warning。
- P0 Action 仍不真实执行；FlowNavigator 只写安全 placeholder 输出，MockRuntimeRunner 写 mock output。Retrieve/CreateObject/CreateVariable/CallMicroflow/RestCall success 可写基础变量，ChangeVariable 只改 preview/raw mock 值。
- Loop scope 每次 iteration 单独 push/pop，内部可见 iterator 与 `$currentIndex`；外部 frame 不可见。
- ErrorHandler scope 只在错误处理路径中可见，包含 `$latestError` 和 REST error 的 `$latestHttpResponse`；本轮不实现完整 rollback/continue/custom 事务语义。
- 第 51 轮 ExpressionEvaluator 将从 `RuntimeExecutionContext.VariableStore` 读取变量；第 54 轮 Object CRUD Actions 将通过同一 store 写真实业务对象变量。
