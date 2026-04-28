# Sample Runtime Matrix

本矩阵用于第 30 轮前端 Runtime 契约回归。所有样例必须完成：

- `validateMicroflowSchema`
- `authoringToFlowGram`
- `toRuntimeDto`
- `toExecutionPlan`
- `mockRunExecutionPlan`

| sample | validate expected | runtime expected | trace expected | unsupported expected |
|--------|-------------------|------------------|----------------|----------------------|
| `sample-order-processing` | manifest 中的 errors/warnings | mock run 可生成 RunSession | 至少包含 start/action/end 中可达节点；无 FlowGram 字段 | P0 动作不进入 unsupported |
| `sample-approval-flow` | manifest 中的 errors/warnings | 若执行到 P1/P2 modeledOnly，返回 `RUNTIME_UNSUPPORTED_ACTION` | failed trace 带 objectId/actionId | modeledOnly 进入 ExecutionPlan.unsupportedActions |
| `sample-rest-error-handling` | save/testRun 可校验 error handler 变量 | success path 成功；`simulateRestError=true` 进入 error handler flow | error path trace 标记 `errorHandlerVisited`，可见 `$latestError` / `$latestHttpResponse` | P0 RestCall supported |
| `sample-loop-processing` | Loop 变量作用域不泄漏 | `loopIterations=2` 可运行 | trace 包含 `loopIteration.index=0/1` 与 `$currentIndex` snapshot | 无 P0 unsupported |
| `sample-object-type-decision` | object type case 由 validator 校验 | 若样例含 objectType decision，则通过 `objectTypeCase` 选择分支 | selectedCaseValue 必须来自 `caseValues` | cast 等 P1/P2 按 modeledOnly |
| `sample-list-processing` | manifest 中的 errors/warnings | P0 子集可运行；P1/P2 到达时 failed | list/retrieve 变量进入 snapshot | modeledOnly 明确记录 |
| `sample-large-100-nodes` | 可 validate/DTO/Plan | 不要求全量 mock run；如执行需受 `RUNTIME_MAX_STEPS_EXCEEDED` 保护 | 不依赖 FlowGram JSON | 大图节点不新增 Runtime unsupported |

验收入口：`pnpm --filter @atlas/mendix-studio-core run verify-contracts`。
