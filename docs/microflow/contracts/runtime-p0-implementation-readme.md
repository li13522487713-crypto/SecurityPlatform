# 前端 P0 强类型实现（第 25 轮）

## 第 57 轮 Runtime RestCall / LogMessage 补充

- 后端新增 `IMicroflowRuntimeHttpClient`、`MicroflowRuntimeHttpClient`、`MicroflowRestSecurityPolicy`、`MicroflowRestRequestBuilder`、`MicroflowRestResponseHandler` 与专用 `RestCallActionExecutor`。
- RestCall request building 支持 method、URL expression、headers/query expression、body none/json/text/form；mapping body 与 importMapping response 继续要求 connector。
- RestCall response handling 支持 ignore/string/json，支持 `outputVariableName`、`statusCodeVariableName`、`headersVariableName` 写入 `VariableStore`。
- `MicroflowRestExecutionOptions` 控制 `allowRealHttp`、私网策略、allowlist/denylist、timeout、response body size、redirect、mock response 与 non-success classification。
- 后端新增 `LogMessageActionExecutor`，支持 template arguments 表达式、`logNodeName`、`includeContextVariables`、`includeTraceId`，并写入结构化 `MicroflowRuntimeLogDto`。
- DebugPanel 兼容展示 frame input/output/error、logNodeName、traceId 与 structured fields；不改 FlowGram 协议或 P0 action schema。

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

## 第 48 轮 ExecutionPlanLoader

- 后端新增 `IMicroflowExecutionPlanLoader` / `MicroflowExecutionPlanLoader`，支持 current resource、version snapshot、inline schema 三种来源。
- TestRunService 仅预热 `LoadFromSchemaAsync(..., mode=testRun)`，失败不改变现有 Mock Runtime 行为；MockRuntimeRunner 仍沿用第 42～47 轮 trace/result。
- ExecutionPlanLoader 只读 AuthoringSchema，输出 plan/diagnostics，不执行 FlowNavigator、VariableStore、ExpressionEvaluator、CRUD、REST 或事务。
- 后端可通过 `scripts/verify-microflow-execution-plan-loader.ts`、`MicroflowBackend.http` 的 Runtime ExecutionPlanLoader 段落检查 `startNodeId`、flow 分类、loop collection、metadataRefs、variableDeclarations、unsupportedActions 与 failOnUnsupported。
- 下一轮建议：第 49 轮 FlowNavigator，以本轮 plan 的 `normalFlows`、`decisionFlows`、`objectTypeFlows`、`errorHandlerFlows` 和 loop collection 为导航输入。

## 第 49 轮 FlowNavigator

- 新增 `IMicroflowFlowNavigator` / `MicroflowFlowNavigator`，以 `MicroflowExecutionPlan` 为唯一导航输入。
- 新增 `MicroflowNavigationOptions`、`MicroflowNavigationContext`、`MicroflowNavigationResult`、`MicroflowNavigationStep`、`MicroflowNavigationError`、`MicroflowFlowNavigatorDiagnostics` 与 trace skeleton mapper。
- 支持 StartEvent、SequenceFlow、ExclusiveMerge、EndEvent、ErrorEvent、Boolean/Enumeration Decision、ObjectType Decision、ActionActivity placeholder、ErrorHandlerFlow、Loop / Break / Continue、maxSteps 与 CancellationToken。
- P0 supported action 不真实执行，只生成 success placeholder step；RestCall 仅在 `simulateRestError=true` 时生成 `RUNTIME_REST_CALL_FAILED` 并尝试 error handler。
- modeledOnly / unsupported / requiresConnector / nanoflowOnly 只根据 ExecutionPlan 的 `supportLevel` 导航处理，不重新解释 AuthoringSchema action。
- TestRunService 本轮保持第 42～47 轮 MockRuntimeRunner 行为，仅保留第 48 轮 plan preload；不把 persisted RunSession/Trace 切到 FlowNavigator。
- 诊断 API：`POST /api/microflows/runtime/navigate` 与 `GET /api/microflows/{id}/runtime/navigate`。
- 自动化验证：`scripts/verify-microflow-flow-navigator.ts`。

## 第 50 轮 VariableStore

- 新增 Runtime VariableStore、变量值模型、ScopeStack、RuntimeExecutionContext 与 Snapshot mapper。
- FlowNavigator 在 Start 前初始化参数和 `$currentUser`，每个 step 结束时写 `variablesSnapshot`；Loop/ErrorHandler 路径使用作用域 push/pop。
- MockRuntimeRunner 改用 VariableStore 生成 TestRun trace 快照，P0 mock action 写入 Retrieve/CreateObject/CreateVariable/ChangeVariable/CallMicroflow/RestCall 的基础输出变量。
- 本轮仍不执行表达式、不访问业务数据库、不发送真实 REST、不实现事务或 CallMicroflow 执行。
- 自动化验证入口：`scripts/verify-microflow-variable-store.ts`，推荐命令 `npx tsx scripts/verify-microflow-variable-store.ts`（需 AppHost 已运行，默认 `http://localhost:5002`）。

下一轮建议：第 51 轮 ExpressionEvaluator P0，从 `RuntimeExecutionContext.VariableStore` 读取参数、系统变量、loop/error 变量与 action output。

## 第 51 轮 Runtime ExpressionEvaluator P0

- 后端新增 Runtime expression 目录，包含 AST、Tokenizer、Parser、Type 模型、TypeInference、EvaluationContext、EvaluationResult、RuntimeError 与解释器执行。
- P0 子集覆盖变量、系统变量、member access、literal、comparison、boolean、`empty()`、`if then else`、枚举值、基础 arithmetic 和 expectedType 检查。
- FlowNavigator 仅低风险接入 Decision 表达式，保留 options override 与 `disableExpressionEvaluation` 回退；ObjectType Decision 不在本轮做表达式求值。
- MockRuntimeRunner 低风险接入 CreateVariable、ChangeVariable、EndEvent、LogMessage、RestCall preview；不做真实 CRUD、真实 REST、真实事务或 CallMicroflow 执行。
- 自动化验证：`dotnet test tests/Atlas.AppHost.Tests/Atlas.AppHost.Tests.csproj --filter "FullyQualifiedName~MicroflowExpressionEvaluatorTests" -p:BaseOutputPath=artifacts/test-bin/`，或 `npx tsx scripts/verify-microflow-expression-evaluator.ts`。
- 下一轮建议：第 52 轮 MetadataResolver + EntityAccess Stub，在现有 expression/metadata/type 边界上补实体继承、访问权限和真实对象解析 stub。

## 第 52 轮 MetadataResolver + EntityAccess Stub

- 新增 Runtime metadata resolver、resolution context、resolved entity/attribute/association/enumeration/enumeration value/microflow ref/dataType/memberPath model 与 plan metadataRefs 预解析 report。
- 新增 `MicroflowRuntimeSecurityContext` 与 `IMicroflowEntityAccessService` stub，支持 `AllowAll`、`RoleBasedStub`、`DenyUnknownEntity`，`Strict` 仅作为预留模式。
- 新增 `IMicroflowRuntimeObjectMetadataService`，只构建 Retrieve/Create/Change/Commit/Delete 的 metadata + access plan，不执行真实数据库 CRUD、不创建真实事务。
- 自动化验证入口：`scripts/verify-microflow-metadata-resolver-entity-access.ts`；诊断 API：`POST /api/microflows/runtime/metadata/resolve`。
- 下一轮建议仍是 TransactionManager / UnitOfWork；本轮不做真实事务。

## 第 53 轮 Runtime TransactionManager / UnitOfWork

- 后端新增 Runtime transaction 模型、`IMicroflowTransactionManager`、`MicroflowTransactionManager`、`IMicroflowUnitOfWork` 与 `MicroflowUnitOfWork`。
- `RuntimeExecutionContext` 挂载强类型 `Transaction`、`UnitOfWork`、`TransactionManager`、`TransactionOptions` 和 `TransactionDiagnostics`。
- MockRuntimeRunner 的 P0 object actions 已接入事务日志：
  - `CreateObject` -> `TrackCreate`，可记录 implicit commit。
  - `ChangeMembers` -> `TrackUpdate`，记录 changed members 与 `validateObject`。
  - `CommitAction` -> `TrackCommitAction`，记录 `operation=commit` 的结构化提交动作但不写 DB。
  - `DeleteAction` -> `TrackDelete`。
  - `RollbackAction` -> `TrackRollbackObject`，不等同 transaction rollback。
- `TraceFrame.output.transaction` 输出对象变更 preview；`RuntimeLog` 写入 `transaction.*` 短文本；`RunSession.transactionSummary` 输出最终状态与计数。
- ErrorHandling 仅提供 rollback/customWithRollback/customWithoutRollback/continue 的事务基础接口；`customWithoutRollback` 与 `continue` 明确写非 rollback 的 keep-active/continue 日志，不执行完整 error handler 语义。
- 自动化验证：`npx tsx scripts/verify-microflow-transaction-manager.ts`（需 AppHost 已运行）。
- 下一轮建议：第 54 轮 Object CRUD Actions，通过本轮 TransactionManager 记录真实对象 CRUD 的运行时 change set。

## 第 54 阶段加强版 Runtime ActionExecutor

- 后端新增 `Runtime/Actions`：`IMicroflowActionExecutor`、`MicroflowActionExecutorRegistry`、`MicroflowActionExecutionContext`、`MicroflowActionExecutionResult`、`MicroflowRuntimeCommand` 与 `IMicroflowRuntimeConnectorRegistry`。
- 前端 51 个 `MicroflowActionKind` 已全部映射到 `serverExecutable`、`runtimeCommand`、`connectorBacked` 或 `explicitUnsupported`。
- Object / Variable / Rest / LogMessage 继续执行既有可信 testRun 语义；List、Cast、Metrics 已从 modeledOnly 转为 serverExecutable mock 语义并写 VariableStore/RuntimeLog。
- Client/UI actions 生成 `RuntimeCommand`，服务端不假装页面或消息已经执行。
- WebService / XML / Workflow / Document / ML / ExternalObject / Java 等 connector-backed action 缺 capability 时返回 `RUNTIME_CONNECTOR_REQUIRED`。
- Nanoflow-only 与 unknown action 走 `ExplicitUnsupportedActionExecutor`，返回 `RUNTIME_UNSUPPORTED_ACTION`，不再 silent skip。
- `MicroflowValidationService` 通过 `MicroflowActionSupportMatrix` 与 Registry 对齐；自动化验证入口：`scripts/verify-microflow-action-executors-full-coverage.ts`。

## 第 55 轮 Loop Runtime 闭环

- 新增 `Runtime/Loops`：`IMicroflowLoopExecutor`、`MicroflowLoopExecutor`、`MicroflowLoopExecutionContext`、`MicroflowLoopIterationContext`、`MicroflowLoopExecutionResult` 与 `MicroflowLoopControlSignal`。
- FlowNavigator 与 TestRun 均接入真实 iterable / while 语义；loop body 仍通过统一 runtime path 执行 action、decision、nested loop 与 error handler。
- ActionExecutorRegistry 不引入 Batch/Parallel 概念；现有 batch/parallel 类 action 仍只属于 ActionExecutor coverage，不影响 Mendix Microflow Loop 语义。
- 自动化入口：`scripts/verify-microflow-loop-runtime.ts`；`.http` 增加 Round 55 iterable loop 与 while false 示例。

## 第 56 轮 CallMicroflow / CallStack

- 新增 `Runtime/Calls` 模型与 `IMicroflowCallStackService` / `MicroflowCallStackService`，`RuntimeExecutionContext` 增加 callStack frame、rootRunId、parentRunId、callCorrelationId 与 maxCallDepth。
- 新增真实 `CallMicroflowActionExecutor`，由 `ActionExecutorRegistry` 延迟解析，避免子调用绕过 Registry、ExecutionPlanLoader、VariableStore 与 MockRuntimeRunner 执行管线。
- target 支持 `targetMicroflowId` 与 `targetMicroflowQualifiedName`；current / latest published / targetVersion schema 选择复用资源、版本、发布快照仓储，并补齐 ExecutionPlanLoader 的 latest published 入口。
- 参数映射执行 ExpressionEvaluator，返回绑定写回父 VariableStore；父 trace 输出 `output.callMicroflow`，包含 parameterBindings、returnBinding、transactionBoundary、callFrameId、callDepth、childRunId 与 childTraceSummary。
- TestRun 持久化现在会递归保存 child RunSession/Trace/Log；`GET /api/microflows/runs/{childRunId}/trace` 可查询子调用 trace。
- 默认事务边界为 inherit/sharedTransaction，child 复用父 transaction；`childTransaction/noTransaction` 仅保留策略，不做完整 ErrorHandling 事务语义。
- 自动化入口：`scripts/verify-microflow-callstack-runtime.ts`；`.http` 增加 Round 56 verify 与 child trace 查询说明。
