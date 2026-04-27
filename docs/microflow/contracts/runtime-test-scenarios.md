# Runtime Test Scenarios

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
