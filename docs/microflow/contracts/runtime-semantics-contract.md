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

## 第 51 轮 ExpressionEvaluator 运行语义

- Decision options 仍最高优先级；未传 `decisionBooleanResult/enumerationCaseValue` 且未设置 `disableExpressionEvaluation` 时，Boolean Decision 可使用 ExpressionEvaluator 选择分支。
- MockRuntimeRunner 已接入 CreateVariable `initialValue`、ChangeVariable `newValueExpression`、EndEvent `returnValue`、LogMessage template arguments 与 RestCall URL/Header/Query/Body preview。
- RestCall 仍不发送真实 HTTP，只把表达式结果写入 `requestPreview`；Retrieve/Create/Commit/Delete 仍不做真实 CRUD，事务语义仍留后续轮次。
- 表达式失败会进入 failed trace/error，`TraceFrame.output.expressionResult` 包含 valuePreview、rawValueJson、valueType、diagnostics 和 referenced variables/members。

## 第 53 轮 TransactionManager / UnitOfWork 运行语义

TransactionManager 是真实 Runtime 引擎的第六块地基，链路为 `RuntimeExecutionContext -> TransactionManager -> UnitOfWork -> changed/committed/rolledBack objects -> Trace/Log/RunSession diagnostics`。

- TestRun Mock Runtime 默认以 `singleRunTransaction` 自动 begin，并创建 `run-start` savepoint。
- 成功结束时，active transaction 自动 commit；失败且仍 active 时自动 rollback。
- `CreateObject`、`ChangeMembers`、`CommitAction`、`DeleteAction`、`RollbackAction` 只写运行时 change set 与 transaction log，不写业务表。
- `CommitAction` 除标记匹配 staged changes 为 committed 外，还会写一条 `operation=commit` 的结构化 changed object，便于 Trace/RunSession 预览提交动作。
- `RollbackAction` 是对象级 rollback operation；ErrorHandling `rollback` / `customWithRollback` 才是 transaction rollback 基础接口。
- `customWithoutRollback` 与 `continue` 不 rollback，事务日志使用 `errorHandlingKeepActive` / `errorHandlingContinue`，后续导航成功时仍可提交当前运行事务。
- savepoint 本轮不复制大对象 JSON，不回滚 VariableStore；rollbackToSavepoint 后的 rolledBack staged changes 会保留为回滚记录，但不会被最终 commit 重新写入 committedObjects。
- Trace / RunSession 只保存 transaction summary 与对象短 preview，不保存 FlowGram JSON。

## 第 54 阶段 ActionExecutor 运行语义

Runtime 从 P0 placeholder 扩展为全量 action 行为分派：

- `serverExecutable`：在 testRun 中执行可信语义，写 VariableStore、TransactionManager、RuntimeLog 和 Trace。
- `runtimeCommand`：生成 `MicroflowRuntimeCommand`，由客户端后续消费；服务端不伪造 UI 已执行。
- `connectorBacked`：生成 connector request，缺 capability 时失败并返回 `RUNTIME_CONNECTOR_REQUIRED`。
- `explicitUnsupported`：Nanoflow-only、unknown 或 unsafe action 返回 `RUNTIME_UNSUPPORTED_ACTION`。

`TraceFrame.output` 统一包含 `actionKind`、`executorCategory`、`supportLevel`、`outputPreview`、`producedVariables`、`runtimeCommands`、`connectorRequests`、`transaction`、`diagnostics` 与 `durationMs`。`modeledOnly` 不再是运行时模糊状态，已建模 action 必须转成上述四类之一。

## 第 52 轮 Metadata / Access 运行语义

- Runtime 执行前可通过 `MetadataResolver.CreateContextAsync(plan, securityContext)` 固化本次 run 的 metadata catalog、索引、plan refs 与安全上下文。
- ExpressionEvaluator、Object CRUD metadata plan 与后续 CallMicroflow 均复用同一个 resolution context，unknown metadata 返回 diagnostic，不抛 NullReference。
- EntityAccess 当前只做 stub decision：system context 与 `applyEntityAccess=false` 可 bypass 但必须记录 decision source；普通 user 由 configured mode 判断。
- 本轮不做完整权限系统、真实 CRUD、真实事务、真实 REST、完整 Domain Model 管理器。

## 第 55 轮 Runtime Loop 语义

- 新增 `IMicroflowLoopExecutor` / `MicroflowLoopExecutor`，Loop 执行只消费 `MicroflowExecutionPlan.loopCollections` 与 `RuntimeExecutionContext`，不读取 FlowGram JSON，也不改 AuthoringSchema。
- `iterableList` 从 VariableStore 读取 `loopSource.listVariableName`，变量缺失返回 `RUNTIME_VARIABLE_NOT_FOUND`，非 list 返回 `RUNTIME_LOOP_SOURCE_NOT_LIST`；空 list 迭代 0 次并走 LoopedActivity normal outgoing。
- 每次 iteration push loop scope，写入 iterator 与 readonly/system `$currentIndex`；iteration 结束 pop scope，loop-local action output 默认不外泄。
- `whileCondition` 每轮执行 ExpressionEvaluator，期望 boolean；false 退出，求值失败返回 `RUNTIME_LOOP_CONDITION_ERROR`，非 boolean 返回 `RUNTIME_LOOP_CONDITION_NOT_BOOLEAN`。
- `BreakEvent` / `ContinueEvent` 在 loop 外返回 `RUNTIME_LOOP_CONTROL_OUT_OF_SCOPE`；在 loop 内只作用于最近 loop，break 后执行 LoopedActivity normal outgoing，continue 跳到下一 iteration。
- Nested loop 通过 Runtime loop scope stack 隔离；`$currentIndex` 解析最近 loop，外层 iterator 在内层仍可见，内层同名变量按 scope shadow 处理。
- `maxIterations` 与既有 `maxSteps` 同时生效，超限返回 `RUNTIME_LOOP_MAX_ITERATIONS_EXCEEDED` 或 `RUNTIME_MAX_STEPS_EXCEEDED`，不允许 silent infinite loop。
- 本轮不做 CallMicroflow 深度递归、真实 REST 深化、完整 ErrorHandling rollback 事务语义或真实 ObjectStore/权限专项；第 56-58 轮应复用本轮 loop scope、trace 与控制信号。
