# Runtime Trace / Debug Session 契约

## 权威类型

- **运行时 trace 帧**：`MicroflowTraceFrame`（`@atlas/microflow/debug` → `trace-types.ts`）；与 `MicroflowRunSession`、`MicroflowRuntimeError` 等一并用于 test-run、本地 `runtime-adapter/types`。
- **会话内变量切片**：`MicroflowRunSession.variables` 的元素为 **`MicroflowRunSessionVariableSnapshot`**（按 `frameId` / `objectId` 挂变量行），与编辑器用的 **`MicroflowEditorVariableSnapshot`**（`variables/variable-snapshot`）不同名、不同用途。
- **Authoring 上可选的调试快照**：`MicroflowAuthoringSchema.debug` 中的 `traceFrames` / `lastTrace` 为 **`MicroflowAuthoringPersistedTraceFrame[]`**（`schema/types.ts`），仅会话/高亮辅助，**不**等同于一次完整 `MicroflowRunSession`。
- 模拟测试运行：`mockRunExecutionPlan(plan, input)` 是主入口；`debug/mock-test-runner` 中的 schema 入口仅做 DTO/Plan 转换包装。

## 定位

- Trace 帧必须能关联到 **`objectId` / `flowId` / `actionId` / `collectionId`**（与图示高亮一致）。
- 决策、循环、错误分支的附加字段在 **`MicroflowTraceFrame`** 上扩展（如 `selectedCaseValue`、`loopIteration` 等，以类型定义为准）。持久化在 Authoring 的帧可含重叠字段（如 `selectedCaseValue`），以便 FlowGram 叠加；完整语义仍以运行时类型为准。

## 不变量

- **`MicroflowRunSession`（含完整 `trace` / `logs` / `variables` 等）不作为 Authoring 业务真相源**；可选的 `debug.lastTrace` 仅为编辑器态。清除调试状态**不得**将业务内容的 dirty 清为 false（由编辑器状态机实现）。
- RunSession / TraceFrame 不得包含 FlowGram / WorkflowJSON；runtime highlight 只能消费这些字段。
- Decision/ObjectType trace 使用 `selectedCaseValue` 标识被选分支；Loop trace 使用 `loopIteration` 标识迭代上下文；ErrorHandler trace 使用 `errorHandlerVisited` 标识错误路径。

## 后端 test-run 响应建议

与 `TestRunMicroflowResponse` 对齐：`runId`、`status`、`frames[]`、`session`、`error?`。

## 第 42 轮后端 Mock Trace

- `POST /api/microflows/{id}/test-run` 已返回 `MicroflowApiResponse<{ session }>`；`session.trace/logs/variables/error` 与 DebugPanel 契约同构。
- TraceFrame 按执行顺序持久化，字段包含 `objectId`、`actionId`、`collectionId`、incoming/outgoing flow、`selectedCaseValue`、`loopIteration`、input/output/error、`variablesSnapshot` 与 message。
- RuntimeLog 单独持久化并按 timestamp 查询；LogMessage action 会生成对应 log。
- Mock Runtime 不是真实 Runtime，不执行数据库 Retrieve/Commit/Delete，不调用外部 REST；RestCall success/error 只产生契约级输出与错误路径。
- 第 48 轮真实 `ExecutionPlanLoader` 接入后，应继续复用本轮 DTO 与持久化结构。
