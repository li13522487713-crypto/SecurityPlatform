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
