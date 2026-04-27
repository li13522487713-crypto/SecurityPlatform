# 校验：MicroflowValidationIssue 契约

## 第 58 轮 ErrorHandling 校验补充

- `rollback` 不应配置 errorHandler flow，返回 `MF_ERROR_HANDLER_ROLLBACK_HAS_FLOW` warning。
- `customWithRollback` / `customWithoutRollback` 必须配置 errorHandler flow，缺失返回 `MF_ERROR_HANDLER_WITH_ROLLBACK_MISSING_FLOW`。
- 同一 source 多条 errorHandler flow 返回 `MF_ERROR_HANDLER_DUPLICATED`。
- errorHandler flow 目标不存在返回 `MF_FLOW_DESTINATION_MISSING`，目标为 StartEvent 返回 `MF_ERROR_HANDLER_SOURCE_UNSUPPORTED`。
- `continue` 仅允许 Runtime policy 标记支持的 action；当前后端校验与 Runtime 对齐为 `callMicroflow`、`restCall`，其它 action 返回 `MF_ERROR_HANDLER_CONTINUE_NOT_ALLOWED`。
- `$latestError`、`$latestHttpResponse`、`$latestSoapFault` 只允许 error handler scope；完整静态路径收窄仍继续沿 VariableIndex hardening 推进。

## 第 57 轮 RestCall / LogMessage 校验补充

- RestCall 校验 `request.method`、`request.urlExpression`、headers/query key、headers/query value expression、body expression/form fields、`timeoutSeconds > 0`、string/json/importMapping response 的 `outputVariableName`、status/header variable name。
- RestCall GET body 产生 warning；body mapping 与 importMapping 在缺 connector 的 publish/testRun 模式为 error，在 edit/save 模式为 warning。
- LogMessage 校验 `level`、`template.text` 与 `template.arguments` expression；未知 level 为 error。
- Runtime 仍保留兜底校验：即使 Validator 未阻断，RestCallActionExecutor 与 LogMessageActionExecutor 也会返回结构化 `RuntimeErrorCode`。

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

P0 字段路径使用 AuthoringSchema 路径，数组使用点号下标：`action.memberChanges.0.valueExpression`、`action.parameterMappings.0.argumentExpression`、`action.request.headers.0.valueExpression`。第 45 轮前端 FieldError 会兼容显示 `memberChanges[0]` 形式，但后端与契约仍以点号下标为准。常用稳定路径包括：

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
- `originConnectionIndex`
- `destinationConnectionIndex`
- `editor.edgeKind`
- `isErrorHandler`

## 第 56 轮 CallMicroflow 校验对齐

- `callMicroflow` target 允许 `action.targetMicroflowId` 或 `action.targetMicroflowQualifiedName`，二者均缺失为 error；目标不存在为 metadata error，Nanoflow target 不进入 Microflow Runtime。
- `action.parameterMappings.{index}.parameterName` 必须能匹配目标微流参数；目标 required parameter 必须有 mapping。
- `action.parameterMappings.{index}.argumentExpression` 仍用表达式校验，运行时会再次用目标 parameter type 做 expectedType 兜底。
- `action.returnValue.storeResult=true` 时 `action.returnValue.outputVariableName` 必填；目标 returnType=void 时不允许 storeResult。
- 输出变量与已存在变量冲突使用 `MF_VARIABLE_DUPLICATED`，Runtime 仍会以 `RUNTIME_VARIABLE_DUPLICATED` / `RUNTIME_VARIABLE_TYPE_MISMATCH` 做兜底失败。

## 第 28 轮规则

- Expression diagnostics 进入 `ValidationIssue`，并保留 `MF_EXPR_*` 码。
- P0 Action 校验覆盖必填字段、metadata 引用、变量类型、表达式 expectedType、ErrorHandling 和 Reachability。
- ErrorHandler 校验要求 custom handler 有 error flow、rollback 不应有 error flow、每个 source 最多一个 error handler flow。
- Reachability 校验不把 Annotation/ParameterObject 作为可执行对象；ErrorHandlerFlow 不参与 normal path。

## 第 29 轮 Flow / Edge 规则

- `validateFlows` 必须校验 origin/destination 存在、端口方向和 connectionIndex 可解析、edgeKind 与 source kind 匹配、`isErrorHandler` 与 `editor.edgeKind` 一致。
- `decisionCondition` 只能来自 ExclusiveSplit；boolean/enum/empty/noCase case 与 source 类型匹配，重复 case 报错，`noCase` 在 edit 为 warning、save/publish/testRun 为 error。
- `objectTypeCondition` 只能来自 InheritanceSplit；inheritance/empty/fallback/noCase case 与 specialization 匹配，重复 specialization/empty/fallback 报错。
- `AnnotationFlow` 至少一端为 Annotation；`SequenceFlow` 不得连接 Annotation 或 ParameterObject。
- root 与 Loop internal collection 禁止直接互连；flow 必须存放在端点所在的同一 `objectCollection`。

## 后端建议

持久化或 CI 可返回 **相同 JSON 结构**，前端 ProblemPanel 即可复用。

## 第 40 轮后端实现

- 已实现 `POST /api/microflows/{id}/validate`，响应为 `MicroflowApiResponse<ValidateMicroflowResponse>`。
- 支持 `schema` inline 校验；未传 `schema` 时读取后端当前 `MicroflowSchemaSnapshot.SchemaJson`。
- 后端 validator 基于 `MicroflowAuthoringSchema`，拒绝根级 `nodes` / `edges` / `workflowJson` / `flowgram`，不读取 FlowGram JSON。
- 校验依赖第 39 轮 `IMicroflowMetadataService` 获取 `MicroflowMetadataCatalog`，不依赖前端 mock metadata。
- P0 覆盖 root、objectCollection、flows、events、decisions、loop、P0 action、metadata references、variables、expressions、error handling、reachability。
- `issue.id` 由 `code + objectId + flowId + actionId + parameterId + collectionId + fieldPath` 哈希生成，保持相对稳定。
- 当前表达式与变量作用域是基础版：扫描 `$variable` / `$object/member`、必填表达式、未闭合字符串和未知变量/成员；完整表达式执行器和变量作用域图留 Runtime 前深化。
- `edit/save/publish/testRun` mode 已生效：`edit` 对部分未完成配置降级 warning；`testRun` 对 unsupported/modeledOnly P0 执行阻断类问题按 error 返回。

## 第 55 轮 Loop Validation 对齐

- `iterableList` 要求 `loopSource.listVariableName` 必填、存在且类型为 `list`，`iteratorVariableName` 必填且满足变量名正则。
- `whileCondition` 要求 `loopSource.expression` 必填；Runtime 会按 boolean expectedType 再做最终求值校验。
- loop body 仍禁止 StartEvent / EndEvent；Runtime 若遇到缺失 body entry 或 dead-end 返回 `RUNTIME_LOOP_BODY_NOT_FOUND` / `RUNTIME_LOOP_DEAD_END`。
- Break / Continue 必须位于 loop 内且不允许 outgoing；Runtime 兜底错误码为 `RUNTIME_LOOP_CONTROL_OUT_OF_SCOPE`。
- loop internal flow 不能跨 collection；testRun / publish mode 下 loop 相关阻断类问题保持 error。

## 第 60 轮 Validation / ProblemPanel 回归

- 自动化入口：`scripts/verify-microflow-validation-integration.ts`，由 Round60 总控脚本调用。
- 必须覆盖 edit/save/publish/testRun mode、missing Start、FlowGram JSON 拒绝、missing action fieldPath、invalid metadata reference、expression/loop/errorHandling/unsupported action。
- 前端 ProblemPanel 必须显示后端 issues；点击 issue 定位 object/flow，字段错误按 `fieldPath` 进入 FieldError。
- Publish/TestRun 前置校验失败必须阻止发布或运行，不生成假成功 session。
