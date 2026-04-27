# Runtime Semantics Contract v2

## 执行图

- Runtime 语义以 `MicroflowAuthoringSchema.objectCollection`、嵌套 Loop collection 与 `MicroflowFlow` 为准。
- 第 30 轮起，前端 Mock Runner 的唯一执行输入是 `MicroflowExecutionPlan`；旧 schema test-run 入口仅负责 `toRuntimeDto → toExecutionPlan → mockRunExecutionPlan` 转换。
- `AnnotationFlow` 不参与执行图。
- `ErrorHandlerFlow` 不参与 normal graph，只参与 error handler scope 与运行时异常跳转。
- Loop collection 独立建图，外部变量可进入 Loop，Loop 内变量默认不回流外部主路径。
- Runtime DTO / ExecutionPlan 必须保留 `caseValues` 与 `isErrorHandler`，但不得依赖 FlowGram JSON 或视觉顺序执行。
- ExecutionPlan 将 control flow 分为 `normalFlows`、`decisionFlows`、`errorHandlerFlows`；AnnotationFlow 可用于编辑器导出元数据，但不进入这些分组。
- AutoLayout 只能改对象坐标/容器尺寸/可视 routing，必须保持 flow semantic hash 不变：origin、destination、connectionIndex、edgeKind、caseValues、isErrorHandler、kind 不可改变。
- Runtime Trace / RunSession 只使用 objectId、flowId、actionId、collectionId 定位，不得携带 FlowGram JSON。

## 变量语义

- Runtime DTO 必须携带 `variables: MicroflowVariableIndex`。
- ExecutionPlan 必须携带 `variableDeclarations`、`actionOutputs`、`loopVariables`、`systemVariables`、`errorContextVariables`、`variableScopes` 与 `variableDiagnostics`。
- 变量 declaration 只包含 Runtime 需要的信息：name、dataType、source、scope、readonly、objectId/actionId/flowId/loopObjectId；不包含 UI-only 信息或 FlowGram JSON。

## Branch / Merge

- Decision 分支变量在兄弟分支不可见。
- Merge 后若变量不是所有 incoming path 都 definite，则为 `maybe`。
- 同名变量类型不一致必须进入 diagnostic，后端实现可按 save/publish 策略升级为阻断。

## Error Handler

- `$latestError` 在所有 custom error handler scope 内可见。
- RestCall error handler 额外暴露 `$latestHttpResponse`。
- WebService error handler 额外暴露 `$latestSoapFault`。
- Error handler 中声明的变量默认不回流 normal path。
- P0 每个 source object 最多一个 error handler flow；rollback/continue/custom 模式与 error flow 的一致性由 Validator 阻断。
