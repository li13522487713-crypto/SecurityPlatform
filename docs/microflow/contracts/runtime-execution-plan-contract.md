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
