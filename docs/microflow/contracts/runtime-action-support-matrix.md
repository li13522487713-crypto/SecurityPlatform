# Runtime 动作支持矩阵（第 58 阶段）

第 58 轮补充 ErrorHandling continue 白名单：`callMicroflow`、`restCall` 与 `loopedActivity` 支持 `continue`；其它 action 配置 continue 时 Runtime 返回 `RUNTIME_CONTINUE_NOT_ALLOWED`，Validation 返回 `MF_ERROR_HANDLER_CONTINUE_NOT_ALLOWED`。所有 action 失败均只通过 `MicroflowActionExecutionResult` 返回错误，由 `IMicroflowErrorHandlingService` 决定 rollback/custom/continue。

第 57 轮将 `restCall` 从 preview/mock-only 推进为安全受控的 Runtime HTTP 动作：默认 mock，配置 `allowRealHttp=true` 后通过 `IMicroflowRuntimeHttpClient` 与 `IHttpClientFactory` 执行真实 HTTP，并强制 URL/SSRF/header policy、timeout、response size limit、response handling 与 `$latestHttpResponse` 错误上下文。`logMessage` 同步升级为结构化 RuntimeLog，支持模板参数表达式、`logNodeName`、`includeContextVariables` 与 `includeTraceId`。

第 54 阶段后端以 `MicroflowActionExecutorRegistry` 作为 Action Runtime 行为的权威来源。所有已建模 `actionKind` 必须落入四类之一：

- `serverExecutable`：服务端 testRun 可执行可信运行语义，写变量、事务、日志或 trace。
- `runtimeCommand`：服务端不假装执行 UI 行为，只返回 `MicroflowRuntimeCommand`。
- `connectorBacked`：依赖外部系统，缺 capability 时返回 `RUNTIME_CONNECTOR_REQUIRED`。
- `explicitUnsupported`：Nanoflow-only / unknown / unsafe 等明确返回 `RUNTIME_UNSUPPORTED_ACTION`。

`modeledOnly` 不再作为运行时模糊状态；进入后端注册表的 P1/P2 动作会转成 `modeledOnlyConverted` 并拥有明确 executor 策略。

## 全量覆盖矩阵

| 分组 | actionKind | runtimeCategory | executor | supportLevel | capability / error |
|---|---|---|---|---|---|
| Object | retrieve | serverExecutable | RetrieveActionExecutor | supported | testRun mock；真实 DB 后续需 objectStore.crud |
| Object | createObject, changeMembers, commit, delete, rollback | serverExecutable | 对应 Object Action Executor | supported | 写 VariableStore/TransactionManager |
| Object | cast | serverExecutable | CastObjectActionExecutor | modeledOnlyConverted | 输出目标类型变量 |
| List | createList, changeList, listOperation, aggregateList | serverExecutable | List Action Executors | modeledOnlyConverted | 写 VariableStore |
| Variable | createVariable, changeVariable | serverExecutable | Variable Action Executors | supported | 表达式求值 + VariableStore |
| Call | callMicroflow | serverExecutable | CallMicroflowActionExecutor | supported | testRun/previewRun/publishedRun 本地同步子微流调用；参数/返回绑定、callStack、childRunId、recursion guard |
| Call | callJavaAction | connectorBacked | JavaActionExecutor | requiresConnector | java.action |
| Call | callJavaScriptAction, callNanoflow | explicitUnsupported | ExplicitUnsupportedActionExecutor | nanoflowOnly | RUNTIME_UNSUPPORTED_ACTION |
| Integration | restCall | serverExecutable | RestCallActionExecutor | supported | 默认 mock；allowRealHttp=true 时经 Runtime HTTP client 和安全策略真实执行 |
| Integration | webServiceCall | connectorBacked | WebServiceCallActionExecutor | requiresConnector | soap.webService |
| Integration | importXml, exportXml | connectorBacked | XML Mapping Executors | requiresConnector | xml.importMapping / xml.exportMapping |
| Integration | callExternalAction, restOperationCall | connectorBacked | External/REST Operation Executors | requiresConnector | external.action / rest.realHttp |
| Client/UI | showPage, showHomePage, showMessage, closePage, validationFeedback, downloadFile | runtimeCommand | Client RuntimeCommand Executors | modeledOnlyConverted | MicroflowRuntimeCommand |
| Client/UI | synchronize | explicitUnsupported | ExplicitUnsupportedActionExecutor | nanoflowOnly | RUNTIME_UNSUPPORTED_ACTION |
| Logging | logMessage | serverExecutable | LogMessageActionExecutor | supported | RuntimeLog |
| Document | generateDocument | connectorBacked | DocumentGenerationExecutor | deprecated | document.generation |
| Metrics | counter, incrementCounter, gauge | serverExecutable | MetricsActionExecutor | modeledOnlyConverted | RuntimeLog fallback |
| ML | mlModelCall | connectorBacked | MLModelCallExecutor | requiresConnector | ml.model |
| Workflow | applyJumpToOption, callWorkflow, changeWorkflowState, completeUserTask, generateJumpToOptions, retrieveWorkflowActivityRecords, retrieveWorkflowContext, retrieveWorkflows, showUserTaskPage, showWorkflowAdminPage, lockWorkflow, unlockWorkflow, notifyWorkflow | connectorBacked | WorkflowActionExecutor | requiresConnector | workflow.action |
| ExternalObject | deleteExternalObject, sendExternalObject | connectorBacked | ExternalObjectActionExecutor | requiresConnector | externalObject.crud |
| Unknown / Generic | unknown actionKind | explicitUnsupported | FallbackUnsupportedActionExecutor | unsupported | RUNTIME_UNSUPPORTED_ACTION |

后端还保留 legacy aliases：`externalObject`、`connectorCall`、`externalConnectorCall`、`javascriptAction`、`nanoflowCall`、`nanoflowCallAction`、`nanoflowOnlySynchronize`、`workflow`、`workflowAction`、`metrics`。

校验：`MicroflowValidationService` 通过同一 `MicroflowActionSupportMatrix` 判定 supportLevel；connector missing 在 `testRun/publish` 为 error，edit/save 为 warning；deprecated 为 warning。

Flow 协议：Runtime action execution 只跟随 `sequence` / `decisionCondition` / `objectTypeCondition` / `errorHandler` control flows；`AnnotationFlow`、FlowGram JSON、port label 与视觉 branch order 不作为 Runtime 执行依据。

自动化验证：`scripts/verify-microflow-action-executors-full-coverage.ts` 静态覆盖所有前端 actionKind，并 smoke 验证 object/list/UI/connector/unsupported trace；第 56 轮 `scripts/verify-microflow-callstack-runtime.ts` 覆盖 CallMicroflow child execution / call stack / child trace。

## 第 26 轮属性面板支持

| actionKind | 属性面板核心控件 | 输出变量 |
|------------|------------------|----------|
| retrieve | Entity/Association selector、ExpressionEditor、Sort、Range、ErrorHandlingEditor | `action.outputVariableName` |
| createObject | EntitySelector、MemberChange 表、commit 开关 | `action.outputVariableName` |
| changeMembers | VariableSelector、MemberChange 表、validateObject | 无 |
| commit / delete / rollback | object/list VariableSelector、事件/刷新/删除行为 | 无 |
| createVariable / changeVariable | VariableName/VariableSelector、DataTypeSelector、ExpressionEditor | createVariable 输出 `action.variableName` |
| callMicroflow | MicroflowSelector、参数 ExpressionEditor、return OutputVariableEditor | `action.returnValue.outputVariableName` |
| restCall | method/url/header/query/body/response/timeout | response/status/headers 变量 |
| logMessage | level、log node、template、arguments | 无 |

## 第 60 轮回归要求

- `scripts/verify-microflow-round60-full-e2e.ts` 必须调用 `scripts/verify-microflow-action-executors-full-coverage.ts`，并把结果写入 `coverage-matrix.json`。
- ActionExecutor 回归不得新增 actionKind 或 connector 平台；失败时按 blocker/critical/major/minor 分类，connector-backed 缺 capability 仍应返回 `RUNTIME_CONNECTOR_REQUIRED`。
- `RestCall`、`LogMessage`、`CallMicroflow`、Loop 与 ErrorHandling 的专项脚本作为全量矩阵的行为证据。

## 第 61 轮生产健康要求

- 本轮不新增 ActionExecutor。
- `GET /api/microflows/runtime/health` 必须能报告 `actionExecutorRegistry` 检查，descriptor count 大于 0。
- readiness gate 只验证 coverage 脚本与健康检查可用，不改变 connector-backed / unsupported / RuntimeCommand 既有语义。
