# Runtime Test Scenarios

## 第 57 轮 RestCall / LogMessage 场景

自动化入口：`npx tsx scripts/verify-microflow-restcall-logmessage-runtime.ts`。

1. RestCall request building 覆盖 GET URL literal、URL from expression、headers/query expression、json/text/form body、mapping body connector required。
2. HTTP security 覆盖 invalid URL、file scheme、localhost/private network、denied host、allowed host 与 sensitive header redaction。
3. Mock execution 覆盖 `allowRealHttp=false`、string/json response variable、statusCode/header variables 与 invalid JSON response failure。
4. Error path 覆盖 `simulateRestError`、non-success HTTP、timeout/network classification、response too large truncated/error metadata 与 `$latestHttpResponse`。
5. LogMessage 覆盖 simple info、template arguments、includeTraceId、includeContextVariables、expression error failed 与 persisted logs。
6. Integration 覆盖 TraceFrame.output.restCall、error handler scope `$latestHttpResponse`、DebugPanel-compatible logs、No FlowGram JSON、connector-backed action 不 silent success。

本文件定义前端 Mock Runtime 的最小回归场景。自动化入口为 `verifyMicroflowContracts()`。

1. 所有 manifest sample 可执行 `validate → authoringToFlowGram → toRuntimeDto → toExecutionPlan → mockRunExecutionPlan`。
2. `sample-rest-error-handling` 在 `simulateRestError=true` 时应沿 error handler flow，若样例路径触达 RestCall。
3. `sample-loop-processing` 在 `loopIterations=2` 时至少产生 `loopIteration.index=0` trace。
4. object type / decision 样例若包含对应 decision flow，trace 必须写入 `selectedCaseValue`。
5. modeledOnly / unsupported action 执行到达时必须产生 `RUNTIME_UNSUPPORTED_ACTION` 或更具体错误码。
6. 大图样例不要求全量 mock run，但 Runtime 必须受 `RUNTIME_MAX_STEPS_EXCEEDED` 保护。
7. Runtime DTO、ExecutionPlan、RunSession 均不得包含 FlowGram JSON。

## 第 42 轮后端 Mock API 场景

1. `POST /api/microflows/{id}/test-run` 不传 `schema` 时读取后端当前保存 schema，传 `schema` 时执行草稿且不保存草稿。
2. TestRun 前必须调用后端 Validation，`mode=testRun`；validation error 返回 `MICROFLOW_VALIDATION_FAILED`。
3. `simulateRestError=false` 时 RestCall 生成 200 mock response；`simulateRestError=true` 时生成 `RUNTIME_REST_CALL_FAILED`，若配置 custom error handler 则进入错误分支。
4. `decisionBooleanResult`、`enumerationCaseValue`、`objectTypeCase` 必须控制 selected case，并写入 trace `selectedCaseValue`。
5. `loopIterations=2` 必须生成 index 0/1 的 `loopIteration` trace；Break/Continue frame 可见。
6. `GET /api/microflows/runs/{runId}` 返回完整 RunSession；`GET /trace` 返回 trace/logs；`POST /cancel` 可把未完成 run 改为 cancelled。

## 第 46～47 轮综合场景

1. TestRunModal 参数表单覆盖 string / boolean / integer / long / decimal / dateTime / enumeration / object / list / json。
2. DebugPanel 必须显示 trace sequence、object/action/flow、selectedCaseValue、loopIteration、variablesSnapshot、logs 与 errors。
3. FlowGram runtime highlight 使用后端 trace，error handler flow 由 `errorHandlerVisited` 标识。
4. `scripts/verify-microflow-publish-version-references-testrun-debug-integration.ts` 作为真实 HTTP 综合回归入口。

## 第 48 轮 ExecutionPlanLoader 场景

1. `POST /api/microflows/runtime/plan` 可从 inline AuthoringSchema 生成 plan。
2. `GET /api/microflows/{id}/runtime/plan` 可从 current schema snapshot 生成 plan。
3. `GET /api/microflows/{id}/versions/{versionId}/runtime/plan` 可从 version schema snapshot 生成 plan。
4. plan 必须包含 start/end、normal/decision/objectType/errorHandler/ignored flows、loopCollections、variableDeclarations、metadataRefs、unsupportedActions。
5. AnnotationFlow 必须进入 `ignoredFlows`，不得进入控制流分组。
6. modeledOnly / unknown action 必须进入 `unsupportedActions`；`failOnUnsupported=true` 返回验证失败 envelope。
7. invalid flow origin/destination 必须产生 diagnostic。
8. 自动化入口：`scripts/verify-microflow-execution-plan-loader.ts`。

## 第 49 轮 FlowNavigator 场景

自动化入口：`scripts/verify-microflow-flow-navigator.ts`。

覆盖场景：

1. Start → End success。
2. 缺 Start 返回 `RUNTIME_START_NOT_FOUND`。
3. P0 ActionActivity placeholder success。
4. unsupported action 返回 `RUNTIME_UNSUPPORTED_ACTION`。
5. modeledOnly 在 `stopOnUnsupported=true` 时 failed，在 `false` 时 skipped 并继续。
6. Boolean Decision true / false 可由 options 控制。
7. Enumeration Decision 可由 `enumerationCaseValue` 控制。
8. ObjectType Decision 可由 `objectTypeCase` 控制，未传时走 fallback。
9. invalid case 返回 `RUNTIME_INVALID_CASE`。
10. ExclusiveMerge 到达即继续，AnnotationFlow 不影响导航。
11. RestCall `simulateRestError=true` 可进入 ErrorHandlerFlow；无 handler 时 failed；handler 到 ErrorEvent failed。
12. Loop `loopIterations=2` 产生两轮内部 steps；Break 停止 loop；Continue 进入下一轮。
13. `maxSteps` 返回 `RUNTIME_MAX_STEPS_EXCEEDED` / `maxStepsExceeded`。
14. NavigationResult 生成 trace skeleton 且不包含 FlowGram JSON。
15. 现有 TestRun Mock API 不切换到 FlowNavigator，本轮只做独立诊断验证。

## 第 50 轮 VariableStore 场景

自动化入口：`scripts/verify-microflow-variable-store.ts`。

覆盖场景：

1. 参数变量可进入 snapshot，缺 required input 产生 diagnostic。
2. `$currentUser` 可见且 readonly/system。
3. P0 placeholder / Mock action 可写 Retrieve、CreateObject、CreateVariable、ChangeVariable、RestCall response/status/headers 等基础变量。
4. Loop 内可见 iterator 与 `$currentIndex`，Loop scope pop 后不泄漏。
5. RestCall `simulateRestError=true` 的 error handler frame 可见 `$latestError` 与 `$latestHttpResponse`。
6. Snapshot 包含 `valuePreview`，可按 option 省略 raw value，且不包含 FlowGram JSON。
7. DebugPanel variables tab 可展示 source、scopeKind、readonly。
8. 本轮不验证真实 ExpressionEvaluator、真实 CRUD、真实 REST、事务或 CallMicroflow 执行。

## 第 51 轮 ExpressionEvaluator P0 场景

自动化入口：`scripts/verify-microflow-expression-evaluator.ts` 与 `MicroflowExpressionEvaluatorTests`。

覆盖场景：

1. Parser 支持 `$Amount > 100`、`$Order/Status = Sales.OrderStatus.New`、`not empty($Order)`、`if $Flag then 'yes' else 'no'`、`$Amount + 10`。
2. Parser 对 `$`、`$Order/`、未闭合字符串返回 parse diagnostic，不抛未捕获异常。
3. Type inference 覆盖 literal、variable、member access、comparison、empty、if、enum 和 unknown member diagnostic。
4. Evaluation 覆盖变量读取、对象 member、comparison、and/or/not、empty string/list/null、if、arithmetic、enum equality。
5. Unknown variable、type mismatch、divide by zero、unsupported function 均返回结构化错误。
6. Runtime integration 覆盖 Decision expression、CreateVariable initialValue、ChangeVariable newValue、End returnValue、LogMessage arguments、RestCall request preview。
7. TraceFrame output/error 必须包含 `expressionResult` 或诊断详情，且表达式结果不得携带 FlowGram JSON。

## 第 52 轮 MetadataResolver + EntityAccess 场景

自动化入口：`scripts/verify-microflow-metadata-resolver-entity-access.ts`。

覆盖场景：

1. Resolve entity / attribute / association / enumeration / enumeration value / microflow ref。
2. Resolve object、list<object>、enumeration 与 unknown dataType。
3. Resolve `$Order/Status`、`$Order/Customer/Name`，并对一对多 list traversal 输出 diagnostic。
4. Plan `metadataRefs` 预解析输出 missing 与 unsupported refs。
5. EntityAccess `AllowAll`、`DenyUnknownEntity`、`RoleBasedStub`、system context 与 `applyEntityAccess=false`。
6. Diagnostic API 与 response 不保存、不回传 FlowGram JSON。

## 第 53 轮 TransactionManager / UnitOfWork 场景

自动化入口：`scripts/verify-microflow-transaction-manager.ts`。

覆盖场景：

1. Begin 创建 active `singleRunTransaction`，并生成 begin log。
2. 成功 TestRun 自动 commit，`RunSession.transactionSummary.status=committed`。
3. ErrorHandling `rollback` 或 failed run 自动 rollback，`status=rolledBack`。
4. `customWithRollback` rollback 后进入 handler path，成功会话仍保留 `rolledBack` transaction summary。
5. `customWithoutRollback` 与 `continue` 不 rollback，成功结束后可 commit。
6. `CreateObject` trace 有 `output.transaction.operation=createObject` 与 `stageCreate` log。
7. `ChangeMembers` trace 有 update preview、changed members 和 `validateObject` 标记。
8. `CommitAction` 生成 commit transaction log 与 `operation=commit` 的结构化变更，并可标记匹配 staged changes 为 committed。
9. `DeleteAction` 生成 delete change 与 `stageDelete` log。
10. `RollbackAction` 生成对象级 rollback operation，不等同 transaction rollback。
11. savepoint 可创建，TestRun 默认创建 `run-start` savepoint；`RollbackToSavepoint` 为后续 ErrorHandling 深化保留基础能力，已回滚 staged change 不会被最终 commit 重新提交。
12. TraceFrame / RuntimeLog / RunSession summary 均不得包含 FlowGram JSON 或大 raw object。
13. 本轮不验证真实数据库 CRUD、真实对象持久化、真实 REST 或 EntityAccess enforcement。

## 第 54 阶段 ActionExecutor 全量覆盖场景

自动化入口：`scripts/verify-microflow-action-executors-full-coverage.ts`。

覆盖场景：

1. 静态扫描前端 51 个 `actionKind`，每个都能从 `MicroflowActionExecutorRegistry` 获取 executor 或 fallback strategy。
2. Registry 暴露 runtimeCategory、supportLevel、connector capability、错误码和 verify 覆盖标记。
3. Object actions 继续写 VariableStore / TransactionManager / Trace。
4. Cast、CreateList、ChangeList、ListOperation、AggregateList、Metrics 从 modeledOnly 转成 serverExecutable mock 语义。
5. ShowMessage / ShowPage / ClosePage / ValidationFeedback / DownloadFile 生成 RuntimeCommand。
6. WebService / XML / Workflow / Document / ML / ExternalObject / Java action 缺 connector capability 时返回 `RUNTIME_CONNECTOR_REQUIRED`。
7. Nanoflow-only / unknown action 返回 `RUNTIME_UNSUPPORTED_ACTION`。
8. Trace output 包含 `executorCategory`、`supportLevel`、`runtimeCommands`、`connectorRequests` 和 transaction preview。
9. ValidationService 与 ActionSupportMatrix / Registry 对齐。

## 第 55 轮 Loop Runtime 场景

自动化入口：`scripts/verify-microflow-loop-runtime.ts`。

覆盖重点：

1. iterable list 3 items 执行 3 次，empty list 执行 0 次。
2. iterator 与 `$currentIndex` 只在 loop iteration scope 可见，loop 后不泄漏。
3. while false 直接跳过；while true 受 `maxIterations` 保护。
4. Break 只终止最近 loop；Continue 跳过当前 iteration 剩余节点并进入下一轮。
5. nested loop 通过独立 scope 区分 depth、inner iterator 与最近 `$currentIndex`。
6. loop body action/error handler/transaction 均复用既有 Runtime pipeline。
7. trace frame 带 `loopIteration`，不包含 FlowGram JSON。

## 第 56 轮 CallMicroflow / CallStack 场景

自动化入口：`scripts/verify-microflow-callstack-runtime.ts`。

覆盖重点：

1. parent 调 child，可按 id / qualifiedName 解析 target。
2. child current schema 可执行 Start -> End，参数从父表达式映射。
3. child string return 可通过 `returnValue.storeResult` 写回父 VariableStore。
4. required parameter 缺失、未知参数、表达式失败、void storeResult、返回类型不匹配均失败。
5. `TraceFrame.output.callMicroflow` 包含 parameterBindings、returnBinding、transactionBoundary、callFrameId、callDepth、childRunId 与 childTraceSummary。
6. child RunSession / Trace / Log 可持久化并通过 `GET /api/microflows/runs/{childRunId}/trace` 查询。
7. direct recursion 与 indirect recursion 被 `RUNTIME_CALL_RECURSION_DETECTED` 阻止，maxCallDepth 超限返回 `RUNTIME_CALL_STACK_OVERFLOW`。
8. child failure 传播为父 CallMicroflow action failure，父 error handler 可继续沿用既有 `custom/continue` 骨架。
9. trace / output 不包含 FlowGram JSON。
