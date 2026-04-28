# Microflow Runtime Engine 设计

> 本文档对齐用户 §8 / §13 / §15 清单，描述 Atlas Microflow 运行时引擎的执行入口、节点派发逻辑、
> 表达式语言能力、错误码与 trace 结构。引擎实现见
> [`Atlas.Application.Microflows/Runtime/MicroflowRuntimeEngine.cs`](../src/backend/Atlas.Application.Microflows/Runtime/MicroflowRuntimeEngine.cs)，
> 与 [`microflow-runtime-engine-architecture.md`](microflow-runtime-engine-architecture.md) 中的整体架构互补：
> 后者描述包结构 / 调用链 / 持久化模型，本文重点是 Stage 25 引入的
> Engine ↔ ActionExecutorRegistry 闭环。

## 1. API 入口

```
POST /api/v1/microflows/{id}/test-run
```

控制器：[`MicroflowResourceController.cs`](../src/backend/Atlas.AppHost/Microflows/Controllers/MicroflowResourceController.cs)
（`TestRun` action）。请求体：`TestRunMicroflowApiRequest`（`schema?` / `inputs?` / `mode` / `version` /
`schemaId` / `correlationId` / `timeout?` / `options?` / `debug`）。响应：`TestRunMicroflowApiResponse`
（`runId`、`microflowId`、`status`、`result`、`errorCode`、`errorMessage`、`durationMs`、
`startedAt`、`completedAt`、`traceId`、`logs`、`nodeResults` (=Trace frames)、`callStack`）。

`MicroflowTestRunService` 在执行前先调用 `MicroflowValidationService.ValidateAsync`；任何 error 严重度
issue 直接以 `MICROFLOW_VALIDATION_FAILED`（HTTP 422）阻断，不进入引擎。

## 2. 引擎执行流程

```
RunAsync(MicroflowExecutionRequest)
  │
  ├─ Build RuntimeContext / MicroflowSchemaModel
  ├─ BindParameters(input vs schema.parameters)
  ├─ MicroflowRuntimeGraph.Build(model)
  ├─ FindStart() -> currentNodeId
  └─ while currentNodeId is set:
       ├─ TryStep(maxSteps)
       └─ ExecuteNodeAsync(node)
             ├─ startEvent / parameterObject / exclusiveMerge -> ExecuteSingleOutgoing
             ├─ endEvent -> ExecuteEnd（returnValue 表达式）
             ├─ exclusiveSplit -> ExecuteDecision（布尔分支）
             ├─ actionActivity -> ExecuteActionAsync
             └─ 其它 kind -> RUNTIME_UNSUPPORTED_ACTION
```

每条 frame 通过 `RuntimeContext.AddFrame` 写入 `Trace`，session 持久化由
`MicroflowTestRunService.PersistSessionGraphAsync` 完成。

## 3. ExecuteActionAsync 派发

Stage 25 之后引擎对 `actionActivity` 节点的派发分两段：

```
ExecuteActionAsync(action):
  1) Fast-path: createVariable / changeVariable / callMicroflow
     -> 引擎内联执行（变量作用域、调用栈、参数映射保留原语义）
  2) Registry path: 其它 actionKind
     -> IMicroflowActionExecutorRegistry.TryGet(actionKind, out executor)
        -> executor.ExecuteAsync(MicroflowActionExecutionContext)
        -> 桥接 ProducedVariables / Logs / Error 回 RuntimeContext
```

桥接细节：

- `ProducedVariables[]` 写入引擎 `RuntimeContext.Variables` 与 `_expressionVariableStore`；
- `Logs[]` 推入 `RuntimeContext.Logs`，最终通过 trace API 返回；
- `Status = Failed/Unsupported/ConnectorRequired` 时合并 `MicroflowRuntimeErrorDto` 走 `Failed`
  分支，**不允许 silent success**；
- `Status = PendingClientCommand` 视为 `success`，输出 `runtimeCommands`，主流程继续。

## 4. ActionExecutorRegistry 覆盖

实现：
[`MicroflowActionExecutorRegistry.cs`](../src/backend/Atlas.Application.Microflows/Runtime/Actions/MicroflowActionExecutorRegistry.cs)。

| ActionKind | 类别 | Executor 类 |
|-----------|------|-------------|
| createVariable / changeVariable | ServerExecutable | `VariableActionExecutors.cs` |
| retrieve / createObject / changeMembers / commit / delete / rollback / cast | ServerExecutable | `ObjectActionExecutors.cs` |
| createList / changeList / aggregateList / listOperation | ServerExecutable | `ListActionExecutors.cs` |
| filterList / sortList | ServerExecutable | `FilterListActionExecutor.cs` / `SortListActionExecutor.cs` |
| restCall | ServerExecutable | `RestCallActionExecutor.cs` (allowRealHttp + security policy) |
| logMessage | ServerExecutable | `LogMessageActionExecutor.cs` |
| callMicroflow | ServerExecutable | 引擎内联 + `CallMicroflowActionExecutor.cs`（Navigator） |
| break / continue | ServerExecutable | `LoopControlActionExecutors.cs`（loop scope 内有效） |
| throwException | ServerExecutable | `ThrowExceptionActionExecutor.cs` |
| callJavaAction / webServiceCall / importXml / exportXml / generateDocument / sendExternalObject … | ConnectorBacked | `ConfiguredMicroflowActionExecutor` -> connector required |
| showPage / showHomePage / showMessage / closePage / validationFeedback / downloadFile | RuntimeCommand | `ConfiguredMicroflowActionExecutor` 返回 `pendingClientCommand` |
| callJavaScriptAction / callNanoflow / synchronize | ExplicitUnsupported | `RUNTIME_UNSUPPORTED_ACTION` |

完整 actionKind 列表见 `MicroflowActionExecutorRegistry.BuiltInActionKinds`，可通过
`/api/v1/microflows/runtime/metadata/resolve` 拉到客户端用于 Toolbox 同步。

## 5. 表达式引擎

实现：
[`MicroflowExpressionEvaluator.cs`](../src/backend/Atlas.Application.Microflows/Runtime/Expressions/MicroflowExpressionEvaluator.cs)。

支持范围：

- 字面量：字符串、`true`/`false`、整数 / 小数、`null`、`empty`。
- 比较：`=`、`!=`、`>`、`<`、`>=`、`<=`（注意：相等使用单 `=`，与 SQL 同步）。
- 布尔：`and` / `or` / `not`（短路求值）。
- 算术：`+`（含字符串拼接）、`-`、`*`、`/`（除零抛 `RUNTIME_EXPR_DIVIDE_BY_ZERO`）。
- 变量引用：`$varName` 或 `$root/sub/leaf`（斜杠成员路径，大小写不敏感）。
- 控制流：`if … then … else …`。
- 函数：`empty(expr)`；其它函数返回 `RUNTIME_EXPR_UNSUPPORTED_FUNCTION`。
- 枚举：`Module.Enum.ValueName`。

错误码常量：`RUNTIME_EXPR_PARSE_ERROR / RUNTIME_EXPR_UNKNOWN_TOKEN / RUNTIME_EXPR_TRAILING_TOKEN /
RUNTIME_EXPR_MEMBER_NOT_FOUND / RUNTIME_EXPR_UNSUPPORTED_FUNCTION / RUNTIME_EXPR_DIVIDE_BY_ZERO /
RUNTIME_EXPR_EXPECTED_TYPE_MISMATCH / RUNTIME_EXPR_MAX_DEPTH_EXCEEDED`。`RUNTIME_VARIABLE_NOT_FOUND` /
`RUNTIME_VARIABLE_TYPE_MISMATCH` 通过 `MicroflowVariableStore` 抛出。

## 6. Trace 与 Run Session

`RuntimeContext.Frames`（[`MicroflowTraceFrameDto`](../src/backend/Atlas.Application.Microflows/Models/MicroflowRuntimeDtos.cs)）包含：

- `id / runId / parentRunId / rootRunId / callDepth / callerObjectId / callerActionId`
- `objectId / actionId / incomingFlowId / outgoingFlowId / selectedCaseValue`
- `status / startedAt / endedAt / durationMs / message`
- `input / output / error / variablesSnapshot`

Session 通过 `MicroflowTestRunService.PersistSessionGraphAsync` 写入：

- `MicroflowRunSessionEntity`（`MicroflowRunRepository`）
- `MicroflowRunTraceFrameEntity[]`
- `MicroflowRunLogEntity[]`
- 子 session 通过 `RuntimeContext.AddChildRun` 递归持久化。

API 暴露：

- `GET /api/v1/microflows/runs/{runId}` -> session
- `GET /api/v1/microflows/runs/{runId}/trace` -> 帧列表
- `POST /api/v1/microflows/runs/{runId}/cancel` -> 取消运行
- `GET /api/v1/microflows/{id}/runs?...` -> run 历史

## 7. 错误码总览（Stage 25 后）

完整列表见
[`microflow-runtime-error-codes.md`](microflow-runtime-error-codes.md)，本节列出与 ExecuteActionAsync
打通强相关的：

| Code | 触发条件 |
|------|---------|
| `RUNTIME_UNSUPPORTED_ACTION` | actionKind 在 fast-path 未命中且 registry 未注册 / Connector 缺失 / 显式 unsupported |
| `RUNTIME_CONNECTOR_REQUIRED` | ConnectorBacked actionKind，对应 capability 未启用 |
| `RUNTIME_VARIABLE_NOT_FOUND` | createVariable / changeVariable 缺 variableName 或目标变量不存在 |
| `RUNTIME_VARIABLE_TYPE_MISMATCH` | 参数 / 变量赋值类型不兼容 |
| `RUNTIME_EXPRESSION_ERROR` | 表达式解析或求值失败 |
| `RUNTIME_FLOW_NOT_FOUND` | 节点出 / 入边数量不符合规范，或 flow 端点缺失 |
| `RUNTIME_TARGET_MICROFLOW_MISSING / RUNTIME_TARGET_MICROFLOW_NOT_FOUND` | callMicroflow 目标缺失或无法加载 |
| `RUNTIME_CALL_STACK_OVERFLOW / RUNTIME_CALL_RECURSION_DETECTED` | callMicroflow 深度或递归检查 |
| `RUNTIME_PARAMETER_MAPPING_MISSING / FAILED` | callMicroflow 参数映射 |
| `RUNTIME_RETURN_BINDING_FAILED` | callMicroflow 返回值绑定 |
| `RUNTIME_REST_BLOCKED_BY_SECURITY` | RestCall 默认安全策略阻断 |
| `RUNTIME_REST_TIMEOUT / RUNTIME_REST_CALL_FAILED` | RestCall 网络层 |
| `MF_THROWN_EXCEPTION`（默认） | ThrowException 节点抛出（可被自定义错误码覆盖） |

## 8. 安全约束

- `RestCallActionExecutor`：未传 `allowRealHttp=true` 时 `MicroflowRestSecurityPolicy` 阻断真实 HTTP；
  阻断时返回 `RUNTIME_REST_BLOCKED_BY_SECURITY`。
- 任意 connector-backed 动作都通过 `IMicroflowRuntimeConnectorRegistry.HasCapability` 检查；缺失即拒绝。
- 引擎严格遵守 `maxSteps` 与 `maxCallDepth`，防止无限循环 / 递归。

## 9. 测试

| 测试文件 | 覆盖范围 |
|----------|---------|
| `tests/Atlas.AppHost.Tests/Microflows/MicroflowRuntimeEngineTests.cs` | 23 项基础 fast-path：start/end/decision/createVariable/changeVariable/callMicroflow happy & failure |
| `tests/Atlas.AppHost.Tests/Microflows/MicroflowRuntimeEngineRegistryDispatchTests.cs` | 9 项 registry 派发：logMessage / restCall security block / aggregateList / createObject / nanoflow / retrieve / throwException / filterList / sortList |
| `MicroflowExpressionEvaluatorTests.cs` | 表达式解析 / 求值 / 错误码 |
| `MicroflowActionExecutorRegistryTests.cs` | Registry coverage / fallback |
| `MicroflowTransactionManagerTests.cs` | 事务 |
| `MicroflowCreateHotfixTests.cs` | hotfix 流程 |

合计 54 个测试覆盖，全部通过。

## 10. 关联文档

- [`microflow-runtime-engine-architecture.md`](microflow-runtime-engine-architecture.md) — 包架构 / 调用链 / 持久化
- [`microflow-runtime-error-codes.md`](microflow-runtime-error-codes.md) — 错误码表
- [`microflow-runtime-api-contract.md`](microflow-runtime-api-contract.md) — Runtime API 契约
- [`microflow-canvas-ui-design.md`](microflow-canvas-ui-design.md) — 前端画布
- [`microflow-node-registry.md`](microflow-node-registry.md) — 节点目录
- [`microflow-e2e-checklist.md`](microflow-e2e-checklist.md) — 端到端验收
