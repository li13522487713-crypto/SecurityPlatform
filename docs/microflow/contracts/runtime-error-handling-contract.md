# Runtime Error Handling Contract

P0 Mock Runtime 与后端 Runtime 都以 `ExecutionPlan.errorHandlerFlows` 解释错误路径。

## 模式

- `rollback`：失败后 run failed，并记录 `RUNTIME_TRANSACTION_ROLLED_BACK`；不沿 error handler flow 继续。
- `customWithRollback`：设置 `$latestError`，RestCall 设置 `$latestHttpResponse`，事务标记 rollback 后沿 error handler flow 执行。
- `customWithoutRollback`：设置 latest error context，不 rollback，沿 error handler flow 执行。
- `continue`：记录 warning/error log，不走 error handler flow，沿 normal flow 继续。

## Trace

错误帧必须包含 `MicroflowRuntimeError.code`、`objectId`、`actionId` 与可选 `flowId`。错误路径帧应设置 `errorHandlerVisited=true`。

## RestCall

Mock 通过 `options.simulateRestError=true` 触发 `RUNTIME_REST_CALL_FAILED`；若有 error handler flow，错误路径中必须可见 `$latestError` 与 `$latestHttpResponse`。

## 第 58 轮 Runtime ErrorHandling 完整化

后端新增 `IMicroflowErrorHandlingService` / `MicroflowErrorHandlingService`，Runtime orchestrator 不再在 ActionExecutor 内私自跳转错误路径。ActionExecutor 只返回 `MicroflowActionExecutionResult`；Runner 将失败结果、source node、normal flow、latest response 与 transaction context 交给统一服务处理。

- `rollback`：调用 `TransactionManager.RollbackForError`，不进入 ErrorHandlerFlow，RunSession 失败，trace `output.errorHandling.transactionRolledBack=true`。
- `customWithRollback`：先调用 `PrepareCustomWithRollback`，再 push error handler scope，写 `$latestError`，RestCall 写 `$latestHttpResponse`，WebService 预留 `$latestSoapFault`，然后执行 ErrorHandlerFlow。
- `customWithoutRollback`：调用 `PrepareCustomWithoutRollback`，不 rollback，写同样的 error scope，handler 到 EndEvent 视为 handled。
- `continue`：只允许 policy 标记支持的 action（当前 `callMicroflow`、`restCall` 与 looped activity），不 rollback、不进入 handler，沿 normal flow 继续；不支持时报 `RUNTIME_CONTINUE_NOT_ALLOWED`。
- ErrorEvent：到达即 `RUNTIME_ERROR_EVENT_REACHED`，若处于 error handler scope，`cause` 保留 `$latestError` raw json；非 error context 到达由 Runtime 兜底 failed 并写 warning log。
- Depth guard：默认 `maxErrorHandlingDepth=5`，重复进入同一 handler flow 返回 `RUNTIME_ERROR_HANDLER_RECURSION`。

RunSession 成功策略：custom handler 到 EndEvent 返回 `success` 并在 `errorHandlingSummary.handledErrorCount` 记录 handled error；rollback、ErrorEvent、handler failed、max depth exceeded 均为 failed；continue 到 EndEvent 为 success 并记录 `continuedErrorCount`。

本轮不做分布式补偿事务、异步 job error handling、完整 SOAP Fault、权限系统或 Connector 平台。
