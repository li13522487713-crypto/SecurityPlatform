# Runtime Semantics Contract v2

## 执行图

- Runtime 语义以 `MicroflowAuthoringSchema.objectCollection`、嵌套 Loop collection 与 `MicroflowFlow` 为准。
- `AnnotationFlow` 不参与执行图。
- `ErrorHandlerFlow` 不参与 normal graph，只参与 error handler scope 与运行时异常跳转。
- Loop collection 独立建图，外部变量可进入 Loop，Loop 内变量默认不回流外部主路径。

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
