# Runtime Test Scenarios

本文件定义前端 Mock Runtime 的最小回归场景。自动化入口为 `verifyMicroflowContracts()`。

1. 所有 manifest sample 可执行 `validate → authoringToFlowGram → toRuntimeDto → toExecutionPlan → mockRunExecutionPlan`。
2. `sample-rest-error-handling` 在 `simulateRestError=true` 时应沿 error handler flow，若样例路径触达 RestCall。
3. `sample-loop-processing` 在 `loopIterations=2` 时至少产生 `loopIteration.index=0` trace。
4. object type / decision 样例若包含对应 decision flow，trace 必须写入 `selectedCaseValue`。
5. modeledOnly / unsupported action 执行到达时必须产生 `RUNTIME_UNSUPPORTED_ACTION` 或更具体错误码。
6. 大图样例不要求全量 mock run，但 Runtime 必须受 `RUNTIME_MAX_STEPS_EXCEEDED` 保护。
7. Runtime DTO、ExecutionPlan、RunSession 均不得包含 FlowGram JSON。
