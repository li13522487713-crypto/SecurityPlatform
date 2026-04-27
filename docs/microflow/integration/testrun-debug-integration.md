# TestRun / Debug 联调说明

## 范围

本轮覆盖 TestRunModal、HTTP RuntimeAdapter、MicroflowDebugPanel、Trace / Input / Output / Variables / Logs / Errors、Cancel Run、Get Run、Get Trace，以及 FlowGram runtime highlight。

依赖后端已完成第 42 轮 TestRun Mock API 与 RunSession / TraceFrame / RuntimeLog 持久化。前端配置 `adapterMode=http` 后，不允许回退前端 mock runner。

## 联调步骤

1. 点击 TestRun 前调用 ValidationAdapter `mode=testRun`。
2. `errorCount > 0` 时不调用 `/test-run`，打开 ProblemPanel；warning only 允许运行。
3. TestRunModal 根据 `schema.parameters` 生成输入表单，支持 string / boolean / integer / long / decimal / dateTime / enumeration / object / list / json。
4. options 支持 `simulateRestError`、`decisionBooleanResult`、`enumerationCaseValue`、`objectTypeCase`、`loopIterations`、`maxSteps`。
5. 点击运行调用 `POST /api/microflows/{id}/test-run`；返回 session 后立即回读 `GET /api/microflows/runs/{runId}` 与 `GET /api/microflows/runs/{runId}/trace`。
6. DebugPanel 展示后端 RunSession、TraceFrame、RuntimeLog、VariableSnapshot 与 RuntimeError。
7. 点击 trace frame 定位 object；点击 in/out flow 定位连线；点击 error/log 定位相关节点或连线。
8. Cancel Run 调用 `POST /api/microflows/runs/{runId}/cancel`，随后回读 session/trace。

## Trace 字段

TraceFrame 必须保留 `objectId`、`actionId`、`collectionId`、`incomingFlowId`、`outgoingFlowId`、`selectedCaseValue`、`loopIteration`、`variablesSnapshot`、`errorHandlerVisited`。

RunSession / TraceFrame / RunLog 从数据库读取；TestRun 不修改 AuthoringSchema、`CurrentSchemaSnapshotId` 或 `publishStatus`。

## Runtime Highlight

- `success` frame 显示 node success。
- `failed` frame 显示 node failed。
- `outgoingFlowId` 显示 flow visited。
- `errorHandlerVisited=true` 显示错误处理路径。
- 清除 run 后高亮清空，且不修改 dirty 和 viewport。

## 不覆盖

本轮仍使用后端 Mock Runtime，不覆盖真实 Runtime、真实数据库 Retrieve / Commit / Delete、真实外部 REST、完整表达式执行器和完整事务引擎。下一轮建议接入 Runtime ExecutionPlanLoader。

## 验证

- 手工：`src/backend/Atlas.AppHost/Bosch.http/MicroflowBackend.http` 的 TestRun / Debug 段。
- 自动：`scripts/verify-microflow-publish-version-references-testrun-debug-integration.ts`。
