# Runtime Trace / Debug Session 契约

## 权威类型

- `MicroflowTraceFrame`、`MicroflowRunSession`、`MicroflowRuntimeError` 等：`@atlas/microflow/debug` 与 `runtime-adapter/types`。
- 模拟测试运行：`debug/mock-test-runner`（随 `@atlas/microflow` 构建）。

## 定位

- Trace 帧必须能关联到 **`objectId` / `flowId` / `actionId`**（与图示高亮一致）。
- 决策、循环、错误分支的附加字段在 `MicroflowTraceFrame` 上扩展（如 `selectedCaseValue`、`loopIteration` 等，以类型定义为准）。

## 不变量

- **Run session / trace 不写入** `MicroflowAuthoringSchema` 持久化字段；清除调试状态**不得**将 dirty 清 false（由编辑器状态机实现）。

## 后端 test-run 响应建议

与 `TestRunMicroflowResponse` 对齐：`runId`、`status`、`frames[]`、`session`、`error?`。
