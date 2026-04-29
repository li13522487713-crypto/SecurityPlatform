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
| external object / legacy connector | `deleteExternalObject`, `sendExternalObject`, `externalObject`, `connectorCall`, `externalConnectorCall` |
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
