# Microflow ActionKind 命名规范

> R1 生产化门禁文档。本文定义进入 AuthoringSchema、前端 registry、后端 descriptor、verify 矩阵的唯一 canonical actionKind。

## 1. 总则

- actionKind 使用 lower camelCase。
- actionKind 必须表达 Mendix 语义动作，不表达 UI 分组。
- AuthoringSchema、前端 `defaultMicroflowActionRegistry`、后端 `MicroflowActionExecutorRegistry.BuiltInDescriptors()`、生产矩阵必须使用同一个 canonical actionKind。
- 旧别名只能出现在本文档或 R3 的 `MicroflowActionDescriptorNormalizer` migration map；禁止写入新的 schema、样例、fixture、前端默认配置或后端 descriptor。

## 2. Canonical actionKind

| 分组 | canonical actionKind |
|---|---|
| object | `retrieve`, `createObject`, `changeMembers`, `commit`, `delete`, `rollback`, `cast` |
| list | `createList`, `changeList`, `listOperation`, `aggregateList`, `filterList`, `sortList` |
| variable / loop | `createVariable`, `changeVariable`, `break`, `continue` |
| call | `callMicroflow`, `callJavaAction`, `callJavaScriptAction`, `callNanoflow` |
| integration | `restCall`, `webServiceCall`, `restOperationCall`, `importXml`, `exportXml`, `callExternalAction` |
| client command | `showPage`, `showHomePage`, `showMessage`, `closePage`, `validationFeedback`, `downloadFile`, `synchronize` |
| logging / errors | `logMessage`, `throwException` |
| document / metrics / ML | `generateDocument`, `counter`, `incrementCounter`, `gauge`, `metrics`, `mlModelCall` |
| workflow | `applyJumpToOption`, `callWorkflow`, `changeWorkflowState`, `completeUserTask`, `generateJumpToOptions`, `retrieveWorkflowActivityRecords`, `retrieveWorkflowContext`, `retrieveWorkflows`, `showUserTaskPage`, `showWorkflowAdminPage`, `lockWorkflow`, `unlockWorkflow`, `notifyWorkflow`, `workflow`, `workflowAction` |
| external object / legacy connector | `deleteExternalObject`, `sendExternalObject`, `createExternalObject`, `changeExternalObject`, `externalObject`, `connectorCall`, `externalConnectorCall` |
| communication / messaging | `sendEmail`, `sendNotification`, `publishMessage`, `consumeMessage` |
| odata | `callODataAction`, `retrieveODataObject`, `commitODataObject`, `deleteODataObject` |
| file document | `retrieveFileDocument`, `storeFileDocument`, `exportFileDocument`, `importFileDocument` |
| explicit unsupported legacy | `javascriptAction`, `nanoflowCall`, `nanoflowCallAction`, `nanoflowOnlySynchronize` |

## 3. 禁止进入 schema 的旧别名

| 旧别名 | canonical actionKind | R3 migration 行为 |
|---|---|---|
| `webserviceCall` | `webServiceCall` | load normalize，save canonical |
| `webService` | `webServiceCall` | load normalize，save canonical |
| `callExternal` | `callExternalAction` | load normalize，save canonical |
| `externalCall` | `callExternalAction` | load normalize，save canonical |
| `deleteExternal` | `deleteExternalObject` | load normalize，save canonical |
| `sendExternal` | `sendExternalObject` | load normalize，save canonical |
| `rollbackObject` | `rollback` | load normalize，save canonical |
| `castObject` | `cast` | load normalize，save canonical |
| `listUnion` | `listOperation` | load normalize，并设置 operation=union |
| `listIntersect` | `listOperation` | load normalize，并设置 operation=intersect |
| `listSubtract` | `listOperation` | load normalize，并设置 operation=subtract |
| `aggregate` | `aggregateList` | load normalize，save canonical |
| `filter` | `filterList` | load normalize，save canonical |
| `sort` | `sortList` | load normalize，save canonical |

## 4. Verify 规则

- `verify-microflow-action-descriptor-naming.ts` 必须扫描后端 descriptor、前端 registry、schema 样例与矩阵。
- 禁止旧别名进入 schema/registry/descriptor。
- 旧别名仅允许在本文档和 R3 normalizer migration map 中出现。
- 发现旧别名时 production gate 必须输出 no-go。

## 5. R1 错误码对照

| 错误码 | 触发条件 | R1 门禁要求 | 目标轮次 |
|---|---|---|---|
| `RUNTIME_CONNECTOR_REQUIRED` | ConnectorBacked action 缺少 connector capability，例如 WebService / XML / Workflow / Document / ML / ExternalObject / OData / FileDocument / Messaging。 | executor coverage 必须确认 connectorBacked 均声明 capability gate，运行时不得 silent success。 | R1/R3 |
| `RUNTIME_UNSUPPORTED_ACTION` | Nanoflow-only、未知 actionKind 或 explicitUnsupported action 到达服务端 runtime。 | explicitUnsupported 不得返回 success；unknown action 必须走 fallback unsupported executor。 | R1 |
| `RUNTIME_TYPE_MISMATCH` | R3 `cast` executor 按 metadata 校验继承/实现关系失败。 | R1 仅预留并在 executor 计划中标记；R3 实现后纳入 coverage。 | R3 |
| `TRANSACTION_REQUIRED` | R3 `rollback` 在 productionRun 缺少 UnitOfWork / transaction scope。 | R1 仅预留；R3 rollback 测试必须覆盖。 | R3 |
| `MIGRATION_FAILED` | R3 schema migration normalize/load/save/publish 失败或丢字段风险。 | R1 命名文档列明旧别名；R3 migration service 必须阻断 publish。 | R3 |
| `PARALLEL_VARIABLE_WRITE_CONFLICT` | R4 trueParallel 同 split 并发写同一 variable。 | R1 仅预留；R4 gateway verify 必须覆盖。 | R4 |
| `PARALLEL_WRITE_CONFLICT` | R4 trueParallel 同 split 并发写同一 object/member 或外部资源。 | R1 仅预留；R4 gateway verify 必须覆盖。 | R4 |
| `INCLUSIVE_NO_BRANCH_SELECTED` | R4 inclusive gateway 无 active branch 且无 otherwise。 | R1 仅预留；R4 inclusive gateway verify 必须覆盖。 | R4 |
| `EXPRESSION_PARSE_ERROR` | R4 Expression Parser 解析失败。 | R1 仅预留；R4 expression API/editor verify 必须覆盖。 | R4 |
| `EXPRESSION_TYPE_MISMATCH` | R4 Expression TypeChecker 与 expectedType 不一致。 | R1 仅预留；R4 diagnostics / publish blocker 必须覆盖。 | R4 |
| `DEBUG_SESSION_NOT_FOUND` | R4 Debug Session 不存在、过期或不属于当前工作区。 | R1 仅预留；R4 debug API 测试必须覆盖。 | R4 |
| `DEBUG_BREAKPOINT_STALE` | R4 breakpoint 指向已删除/迁移后的节点或表达式 range。 | R1 仅预留；R4/R5 debug UI 与 staleBreakpoint 测试覆盖。 | R4/R5 |
