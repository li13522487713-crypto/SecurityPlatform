# Runtime Trace / Debug Session 契约

## 第 58 轮 ErrorHandling Trace / RuntimeLog

- 错误处理会生成 `TraceFrame.output.errorHandling`，字段包含 mode、sourceObjectId、sourceActionId、sourceActionKind、enteredErrorHandler、errorHandlerFlowId、errorHandlerTargetObjectId、transactionRolledBack、continued、latestErrorWritten、latestHttpResponseWritten、latestSoapFaultWritten、errorDepth、handled、diagnostics。
- `TraceFrame.error` 使用统一 `MicroflowRuntimeErrorDto`，ErrorEvent 使用 `RUNTIME_ERROR_EVENT_REACHED`，cause 保留原始 `$latestError`。
- RuntimeLog 记录 rollback、进入 handler、handler completed、continue after error、ErrorEvent outside error context 等关键步骤。
- DebugPanel 可直接展示 output JSON 与 errors tab；FlowGram 高亮只依赖 `incomingFlowId/outgoingFlowId/errorHandlerVisited`，不读取 FlowGram JSON。

## 第 57 轮 RestCall / RuntimeLog Trace 补充

- RestCall 成功帧的 `output.restCall` 包含 method、urlPreview、headersPreview、queryPreview、bodyPreview、allowRealHttp、securityDecision、statusCode、responsePreview、responseHandling、producedVariables、durationMs、truncated 与 connectorCapabilities。
- RestCall 失败帧的 `error.details` 与 `output.restCall.error` 包含 statusCode、reasonPhrase、bodyPreview、errorKind、latestHttpResponseWritten 与 securityDecision。
- LogMessage 帧的 `output.logMessage` 包含 logLevel、messagePreview、argumentPreviews、logNodeName、includeTraceId、includeContextVariables。
- `MicroflowRuntimeLog` 可选扩展 `logNodeName`、`traceId`、`variablesPreview`、`structuredFieldsJson`；DebugPanel 必须兼容缺失这些字段的历史日志。

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
- TraceFrame 按执行顺序持久化，字段包含 `objectId`、`actionId`、`collectionId`、incoming/outgoing flow、`selectedCaseValue`、`loopIteration`、input/output/error、`variablesSnapshot`、`errorHandlerVisited` 与 message。
- 第 46～47 轮前端 HTTP RuntimeAdapter 在 test-run 返回后会回读 `GET /api/microflows/runs/{runId}` 与 `GET /api/microflows/runs/{runId}/trace`，DebugPanel 与 FlowGram runtime highlight 均消费后端持久化 trace。
- RuntimeLog 单独持久化并按 timestamp 查询；LogMessage action 会生成对应 log。
- Mock Runtime 不是真实 Runtime，不执行数据库 Retrieve/Commit/Delete，不调用外部 REST；RestCall success/error 只产生契约级输出与错误路径。
- 第 48 轮真实 `ExecutionPlanLoader` 接入后，应继续复用本轮 DTO 与持久化结构。

## 第 49 轮 FlowNavigator Trace Skeleton

FlowNavigator 生成 `MicroflowNavigationStep` 与 `MicroflowNavigationTraceFrame`，并提供 `NavigationResult.ToTraceFrames()` / `NavigationStep.ToTraceFrameDto()` 映射到既有 `MicroflowTraceFrameDto`。本轮 trace skeleton 不包含变量快照、不保存 RunSession、不替代 TestRun Mock trace。

字段映射：

- `sequence` → trace frame 顺序。
- `objectId` / `actionId` / `collectionId` → DebugPanel 定位字段。
- `incomingFlowId` / `outgoingFlowId` → flow 高亮字段。
- `selectedCaseValue` → Decision / ObjectType 被选 case。
- `loopIteration` → Loop 骨架迭代上下文。
- `status` / `startedAt` / `endedAt` / `durationMs` / `message` / `error` → 运行帧状态与错误。

`MicroflowNavigationResult` 不包含 FlowGram JSON / WorkflowJSON，可作为真实 Runtime 后续 VariableStore、ExpressionEvaluator 与 ActionExecutor 接入前的导航级 trace 骨架。

## 第 50 轮 VariableSnapshot

- `MicroflowTraceFrameDto.variablesSnapshot` 现在可由 VariableStore 生成；FlowNavigator trace skeleton 与 MockRuntimeRunner trace 均可携带变量快照。
- 单变量 DTO 兼容原字段 `name/type/valuePreview/rawValue/source`，并补充 `rawValueJson`、`readonly`、`scopeKind` 供 DebugPanel 展示。
- 快照以 frame 的 `objectId/actionId/collectionId/stepIndex` 为上下文，不包含 FlowGram JSON / WorkflowJSON。
- DebugPanel 的变量 tab 仍读取当前 active frame 的 `variablesSnapshot`，可显示 source、scopeKind 与 readonly tag；trace/log/error tab 不改变。

## 第 51 轮 Expression Trace Output

- `MicroflowTraceFrameDto.output` / `MicroflowNavigationTraceFrame.output` 可包含 `expressionResult`，用于展示 rawValueJson、valuePreview、valueType、diagnostics、referencedVariables、referencedMembers 与 durationMs。
- Decision 表达式成功时，trace 同时写入 `selectedCaseValue` 与 `output.expressionResult`；失败时写入 `error.code=RUNTIME_EXPRESSION_ERROR` 和表达式 diagnostics。
- CreateVariable / ChangeVariable / EndEvent / LogMessage / RestCall preview 的 Mock trace output 保留表达式结果预览；RestCall error 的 `details` 可包含 `requestPreview`。
- DebugPanel 无需新增协议即可显示 output JSON 与 error code/message；表达式结果不得包含 FlowGram JSON。

## 第 53 轮 Transaction Trace / Log Output

- P0 object action 的 `TraceFrame.output` 可包含 `transaction` object：
  - `transactionId`
  - `status`
  - `operation`
  - `changedObjectCount`
  - `committedObjectCount`
  - `rolledBackObjectCount`
  - `changedObjectsPreview`
  - `diagnostics`
- `RunSession.transactionSummary` 与成功 `RunSession.output.transactionSummary` 输出事务最终摘要。
- `RuntimeLog.message` 以 `transaction.{operation}: {message}` 形式展示事务日志；当前不扩展独立结构化 log payload。
- `CommitAction` 与 `commit.enabled=true` 的隐式提交会在 preview 中形成 `operation=commit` 的短变更记录；`customWithoutRollback` / `continue` 使用 `transaction.errorHandlingKeepActive` / `transaction.errorHandlingContinue`，不会伪装成 rollback 日志。
- DebugPanel 继续按 JSON output 展示 transaction，无需复杂事务可视化。
- Trace/log 只保存短 preview 与计数，不保存 FlowGram JSON、WorkflowJSON 或大 raw object。

## 第 54 阶段 ActionExecutor Trace Output

所有 ActionExecutor 的 `TraceFrame.output` 统一增加以下字段：

- `actionKind`
- `executorCategory`
- `supportLevel`
- `outputPreview`
- `producedVariables`
- `runtimeCommands`
- `connectorRequests`
- `transaction`
- `diagnostics`
- `durationMs`

Connector missing 的 `TraceFrame.error.code` 为 `RUNTIME_CONNECTOR_REQUIRED`；Nanoflow-only / unknown action 的错误码为 `RUNTIME_UNSUPPORTED_ACTION`。DebugPanel 无需协议改造，继续展示 output JSON。

## 第 55 轮 Loop Trace / DebugPanel

- Loop body frame 的 `loopIteration` 至少包含 `loopObjectId`、`collectionId`、`index`、`iteratorVariableName`、`iteratorValuePreview`、`parentLoopObjectId`、`depth`、`controlSignal`、`conditionResult`、`itemCount`。
- `BreakEvent` / `ContinueEvent` trace message 分别为 `Break loop.` / `Continue loop.`，并写 `loopIteration.controlSignal`。
- iterable loop 的 action frame snapshot 可展示 iterator 与 `$currentIndex`；loop scope pop 后后续 frame 不再展示这些变量。
- while loop 在 summary output 展示 mode、conditionResult 与 iteration 计数；condition error 写稳定 RuntimeErrorCode。
- Runtime highlight 仍使用 `objectId` + `collectionId` 定位 loop internal node，不引入 FlowGram JSON。

## 第 56 轮 CallMicroflow Trace / Child Run

- 父 `CallMicroflow` action frame 的 `output.callMicroflow` 必须包含 `targetResourceId`、`targetQualifiedName`、`targetVersion`、`targetSchemaId`、`schemaSelection`、`callFrameId`、`callDepth`、`transactionBoundary`、`parameterBindings`、`returnBinding`、`childRunId`、`childStatus`、`childTraceSummary`、`durationMs` 与 diagnostics。
- 子 `MicroflowTraceFrameDto` 增加 `parentRunId`、`rootRunId`、`callFrameId`、`callDepth`、`callerObjectId`、`callerActionId`；持久化通过 trace frame extra JSON 保存，不新增表。
- `MicroflowRunSessionDto` 增加 `parentRunId`、`rootRunId`、`callFrameId`、`callDepth`、`childRuns` 与 `childRunIds`；TestRun 返回体可直接展开 child run，持久化后可通过 `GET /api/microflows/runs/{childRunId}/trace` 查询。
- RuntimeLog 写 call enter / exit / failed / recursion diagnostics；DebugPanel 不需要树形 UI 改造，继续展示 output JSON 即可看到 call stack 摘要。
- Trace / RunSession 仍禁止携带 FlowGram JSON；child trace 只以 runId/frameId/objectId/actionId 等运行时字段关联。
