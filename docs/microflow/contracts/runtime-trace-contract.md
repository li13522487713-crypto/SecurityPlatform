# Runtime Trace / Debug Session 契约

## 权威类型

- **运行时 trace 帧**：`MicroflowTraceFrame`（`@atlas/microflow/debug` → `trace-types.ts`）；与 `MicroflowRunSession`、`MicroflowRuntimeError` 等一并用于 test-run、本地 `runtime-adapter/types`。
- **会话内变量切片**：`MicroflowRunSession.variables` 的元素为 **`MicroflowRunSessionVariableSnapshot`**（按 `frameId` / `objectId` 挂变量行），与编辑器用的 **`MicroflowEditorVariableSnapshot`**（`variables/variable-snapshot`）不同名、不同用途。
- **Authoring 上可选的调试快照**：`MicroflowAuthoringSchema.debug` 中的 `traceFrames` / `lastTrace` 为 **`MicroflowAuthoringPersistedTraceFrame[]`**（`schema/types.ts`），仅会话/高亮辅助，**不**等同于一次完整 `MicroflowRunSession`。
- 模拟测试运行：`debug/mock-test-runner`（随 `@atlas/microflow` 构建）。

## 定位

- Trace 帧必须能关联到 **`objectId` / `flowId` / `actionId`**（与图示高亮一致）。
- 决策、循环、错误分支的附加字段在 **`MicroflowTraceFrame`** 上扩展（如 `selectedCaseValue`、`loopIteration` 等，以类型定义为准）。持久化在 Authoring 的帧可含重叠字段（如 `selectedCaseValue`），以便 FlowGram 叠加；完整语义仍以运行时类型为准。

## 不变量

- **`MicroflowRunSession`（含完整 `trace` / `logs` / `variables` 等）不作为 Authoring 业务真相源**；可选的 `debug.lastTrace` 仅为编辑器态。清除调试状态**不得**将业务内容的 dirty 清为 false（由编辑器状态机实现）。

## 后端 test-run 响应建议

与 `TestRunMicroflowResponse` 对齐：`runId`、`status`、`frames[]`、`session`、`error?`。
