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
