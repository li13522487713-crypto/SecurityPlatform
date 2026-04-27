# Runtime ExecutionPlan Contract

ExecutionPlan 是前端 Mock Runner 与后端真实 Runtime 的共同执行输入，不包含 FlowGram / WorkflowJSON。

## Pipeline

`MicroflowAuthoringSchema → toRuntimeDto → toExecutionPlan → mockRunExecutionPlan → MicroflowRunSession`

旧编辑器入口可以继续传入 AuthoringSchema，但只能作为 wrapper：先生成 Runtime DTO 和 ExecutionPlan，再执行。

## 顶层字段

- `id` / `schemaId` / `resourceId` / `version`
- `parameters`
- `nodes`
- `flows`
- `normalFlows`
- `decisionFlows`
- `errorHandlerFlows`
- `loopCollections`
- `variableDeclarations`
- `actionOutputs` / `loopVariables` / `systemVariables` / `errorContextVariables`
- `variableScopes` / `variableDiagnostics`
- `metadataRefs`
- `unsupportedActions`
- `startNodeId` / `endNodeIds`
- `createdAt`

## Node

`MicroflowExecutionNode` 必须包含 `objectId`、`actionId`、`kind`、`actionKind`、`officialType`、`collectionId`、`parentLoopObjectId`、`supportLevel`、`runtimeBehavior`、`errorHandling` 与可选 `p0ActionRuntime`。

`runtimeBehavior`：

- `executable`
- `terminal`
- `ignored`
- `unsupported`

## Flow

`MicroflowExecutionFlow` 只包含 control flow，AnnotationFlow 不进入 `flows`。

`controlFlow`：

- `normal`
- `decision`
- `objectType`
- `errorHandler`
- `ignored`（仅类型保留，P0 control plan 不应包含）

`caseValues`、`isErrorHandler`、`collectionId`、`branchOrder` 必须从 AuthoringSchema 保留。

## Validation

`validateExecutionPlan(plan)` 至少校验：

- start node 存在
- flow origin/destination 存在
- ignored/annotation flow 不在 control plan
- supported action node 必须有 P0 runtime config

## 第 48 轮后端 ExecutionPlanLoader

后端已新增只读链路：`MicroflowAuthoringSchema -> MicroflowRuntimeDtoBuilder -> MicroflowExecutionPlanBuilder -> MicroflowExecutionPlanValidator`。输入只能是 AuthoringSchema；不得从 FlowGram JSON、`nodes` / `edges` 或画布协议生成 plan。

后端 `MicroflowExecutionPlan` 是前端结构的 superset，包含 `schemaVersion`、`objectTypeFlows`、`ignoredFlows`、`diagnostics`、`validation`，并保留 `objectId`、`flowId`、`actionId`、`collectionId`、`caseValues`、`isErrorHandler`。`AnnotationFlow` 进入 `ignoredFlows`，不进入 `normalFlows` / `decisionFlows` / `objectTypeFlows` / `errorHandlerFlows`。

诊断入口：

- `POST /api/microflows/runtime/plan`：inline schema 生成 plan，不要求资源存在。
- `GET /api/microflows/{id}/runtime/plan`：读取当前 `CurrentSchemaSnapshot`。
- `GET /api/microflows/{id}/versions/{versionId}/runtime/plan`：读取版本快照。

所有入口返回 `MicroflowApiResponse<MicroflowExecutionPlan>`。`failOnUnsupported=true` 时 unsupported/modeledOnly/requiresConnector/nanoflowOnly 会升级为 `MICROFLOW_VALIDATION_FAILED`。

本轮不执行 action，不访问业务数据库执行 Retrieve/Commit/Delete，不调用 REST，不求表达式值；下一轮由 FlowNavigator 消费该 plan。
