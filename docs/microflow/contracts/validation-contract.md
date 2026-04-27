# 校验：MicroflowValidationIssue 契约

## 权威类型

`MicroflowValidationIssue`（`@atlas/microflow/schema`）。可选别名：`ValidationIssue`（由 `mendix-studio-core/contracts` 导出 `ValidationIssue`）。

## 定位字段（冻结）

| 字段 | 用途 |
|------|------|
| `id` | 稳定键 |
| `severity` | error / warning / info |
| `code` | `MF_*` 机器码 |
| `message` | 人读说明 |
| `objectId` / `flowId` / `actionId` / `parameterId` / `collectionId` | 图与参数定位 |
| `fieldPath` | 属性路径，供 ProblemPanel / 属性栏 |
| `source` | root / objectCollection / flow / action / … |
| `quickFixes` / `relatedObjectIds` / `relatedFlowIds` / `details` | 扩展展示 |

## ValidationCode 分层

代码以 `MF_` 前缀为主。**冻结清单**为 `@atlas/microflow/validators` 中导出的 `microflowValidationCodes` 常量与 `MicroflowValidationCode` 类型；`@atlas/mendix-studio-core` 的 `microflow/contracts` 已再导出，便于后端与 OpenAPI 对齐。

- **Root**：`MF_ROOT_*`
- **Object / Flow / Decision / ObjectType / Loop / Action**
- **Metadata**：`MF_METADATA_*`（含 `MF_METADATA_SPECIALIZATION_NOT_FOUND`）
- **Action 扩展**：`MF_ACTION_NANOFLOW_ONLY`、`MF_ACTION_DEPRECATED` 等
- **Variable / Expression**
- **ErrorHandling / Reachability**

运行期 `code` 仍可能为**清单外**字符串（`MicroflowValidationCode` 含 `string` 并集）；后端应宽容接收并在迭代中合入 `microflowValidationCodes`。

## 统一入口

`validateMicroflowSchema({ schema, metadata, variableIndex?, options: { mode: "edit"|"save"|"publish"|"testRun", includeWarnings, includeInfo } })`。

## 模式差异

- `edit`：建模态，部分必填缺失可降级为 warning；适合实时防抖校验。
- `save`：保存态，结构错误、必填缺失、变量/表达式错误为 error。
- `publish`：发布态，metadata missing、unsupported/nanoflowOnly/requiresConnector 升级为 error；modeledOnly 默认 warning。
- `testRun`：试运行态，modeledOnly、unsupported、requiresConnector、关键表达式 unknown 升级为 error。

## fieldPath 规范

P0 字段路径使用 AuthoringSchema 路径，数组使用点号下标：`action.memberChanges.0.valueExpression`、`action.parameterMappings.0.argumentExpression`、`action.request.headers.0.valueExpression`。常用稳定路径包括：

- `action.retrieveSource.kind`
- `action.retrieveSource.entityQualifiedName`
- `action.retrieveSource.associationQualifiedName`
- `action.outputVariableName`
- `action.entityQualifiedName`
- `action.changeVariableName`
- `action.memberChanges.{index}.memberQualifiedName`
- `action.memberChanges.{index}.valueExpression`
- `action.objectOrListVariableName`
- `action.variableName`
- `action.dataType`
- `action.initialValue`
- `action.targetVariableName`
- `action.newValueExpression`
- `action.targetMicroflowId`
- `action.parameterMappings.{index}.argumentExpression`
- `action.returnValue.outputVariableName`
- `action.request.method`
- `action.request.urlExpression`
- `action.request.headers.{index}.key`
- `action.request.headers.{index}.valueExpression`
- `action.request.queryParameters.{index}.key`
- `action.request.queryParameters.{index}.valueExpression`
- `action.request.body.fields.{index}.valueExpression`
- `action.response.handling.outputVariableName`
- `action.timeoutSeconds`
- `action.template.text`
- `returnValue`
- `splitCondition.expression`
- `loopSource.listVariableName`
- `loopSource.iteratorVariableName`
- `loopSource.expression`
- `caseValues`

## 第 28 轮规则

- Expression diagnostics 进入 `ValidationIssue`，并保留 `MF_EXPR_*` 码。
- P0 Action 校验覆盖必填字段、metadata 引用、变量类型、表达式 expectedType、ErrorHandling 和 Reachability。
- ErrorHandler 校验要求 custom handler 有 error flow、rollback 不应有 error flow、每个 source 最多一个 error handler flow。
- Reachability 校验不把 Annotation/ParameterObject 作为可执行对象；ErrorHandlerFlow 不参与 normal path。

## 后端建议

持久化或 CI 可返回 **相同 JSON 结构**，前端 ProblemPanel 即可复用。
