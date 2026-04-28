# Microflow Stage 12 - Start / End / Parameter Foundation

## 1. Scope

本轮完成 Start 节点基础配置、End 节点基础配置、Parameter 节点基础配置、Parameter node 与 schema-level `parameters` 同步、End `returnType` / `returnValue` 基础同步、参数基础校验、dirty 状态接入、保存刷新恢复闭环和 A/B 微流 schema 隔离。

本轮不做表达式引擎、后端执行引擎、运行输入面板、Call Microflow metadata、Domain Model metadata、trace/debug 和历史 schema migration。

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-microflow/src/schema/utils/microflow-signature.ts` | 新增 | Start/End/Parameter 输入输出相关纯 schema helper。 |
| `src/frontend/packages/mendix/mendix-microflow/src/schema/utils/index.ts` | 修改 | 导出 Stage 12 helper。 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/utils/schema-patch.ts` | 修改 | `updateParameter` 统一走 schema-level parameter 与 Parameter object 同步。 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/forms/parameter-object-form.tsx` | 修改 | 补全参数 id、node id、name/type/required/default/description 编辑与 warning。 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/forms/event-nodes-form.tsx` | 修改 | Start/End flow summary；End `returnType` 可编辑并同步 schema；End `returnValue` 写入真实 End object。 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/selectors/DataTypeSelector.tsx` | 修改 | 支持按场景禁用 `void`，Parameter 类型不允许选择 `void`。 |
| `src/frontend/packages/mendix/mendix-microflow/src/adapters/authoring-operations.ts` | 修改 | 复制 Parameter 时生成新 parameter id 和基于原名的 `_Copy` 名称。 |
| `src/frontend/packages/mendix/mendix-microflow/src/adapters/parameter-operations.ts` | 修改 | 参数重命名默认不重写表达式，保留显式 opt-in。 |
| `src/frontend/packages/mendix/mendix-microflow/src/validators/validate-root.ts` | 修改 | 参数空名与大小写不敏感重名校验。 |
| `src/frontend/packages/mendix/mendix-microflow/src/schema/__tests__/microflow-parameters.test.ts` | 新增 | 覆盖参数同步、End 返回值、A/B 隔离与校验。 |
| `docs/microflow-stage-01-gap-matrix.md` | 修改 | 更新 Stage 12 P1 状态。 |

## 3. Input / Output Schema Contract

| 语义 | 源码字段 | 类型 | 当前是否存在 | 当前是否同步 | 本轮处理 |
|---|---|---|---|---|---|
| schema-level parameters | `MicroflowAuthoringSchema.parameters` | `MicroflowParameter[]` | 是 | 创建/删除/复制已有，属性编辑本轮补强 | 统一 helper 同步 name/type/required/default/description。 |
| variables | `MicroflowAuthoringSchema.variables` | `MicroflowVariableIndex` | 是 | 由 `refreshDerivedState` / `buildVariableIndex` 派生 | 本轮不做复杂作用域分析。 |
| schema-level returnType | `MicroflowAuthoringSchema.returnType` | `MicroflowDataType` | 是 | End 表单此前只读 | 本轮 End 表单可编辑并写回 schema。 |
| schema-level returnValue | 无根级字段 | 不适用 | 否 | 不适用 | 以 End object `returnValue` 为真实返回表达式。 |
| objectCollection | `MicroflowAuthoringSchema.objectCollection` | `MicroflowObjectCollection` | 是 | 已同步 | Parameter/Start/End object 均在此持久化。 |
| flowCollection | 无此字段，真实字段为 `flows` / nested `objectCollection.flows` | `MicroflowFlow[]` | 否 | `flows` 已同步 | 文档统一按真实 `flows` 描述。 |
| Parameter object | `MicroflowParameterObject.parameterId` / `parameterName` / `caption` | object + string | 是 | 创建/删除/复制已有 | 本轮改名同步 object 与 `parameters`。 |
| Start object | `MicroflowStartEvent.trigger` / `caption` / `documentation` | object + string | 是 | object 层同步 | 本轮补 flow summary 和多 Start warning。 |
| End object | `MicroflowEndEvent.returnValue` / `caption` / `documentation` | object + `MicroflowExpression` | 是 | object 层同步 | 本轮 return expression 明确通过 helper 写回 End object。 |

## 4. Parameter Sync Strategy

每个 `parameterObject` 对应一个 `MicroflowParameter`，关联键为 `parameterObject.parameterId === parameter.id`。`MicroflowParameter` 使用 `id/name/dataType/type/required/defaultValue/description/documentation`，其中 `type` 从 `dataType` 派生为 `MicroflowTypeRef`。

创建 Parameter 时，拖拽逻辑同时创建 Parameter object 和 `schema.parameters` 项。修改 name/type/required/default/description 时，通过 helper 更新 `schema.parameters`；name 同时更新 object `caption` 与 `parameterName`。删除 Parameter object 时，同步删除对应 `schema.parameters` 并清理相关连线和 selection。复制 Parameter object 时生成新的 object id、parameter id，名称采用 `${oldName}_Copy`，若冲突继续追加数字，不复制连线。

排序规则：`schema.parameters` 保持数组顺序，拖入时追加，复制时追加到数组末尾；本轮不实现拖拽参数排序。

## 5. Start Node Strategy

Start 基础字段为 `id/kind/officialType/caption/documentation/trigger`，caption 和 documentation 继续由基础对象表单编辑。Start 显示 outgoing flow summary。当前拖拽层限制一个 Start；如果历史 schema 已存在多个 Start，表单显示 warning，校验器继续报告 `MF_START_DUPLICATED`。删除策略沿用现有 object 删除策略，本轮不额外禁止。

## 6. End Node Strategy

End 基础字段为 `id/kind/officialType/caption/documentation/returnValue/endBehavior`，caption 和 documentation 继续由基础对象表单编辑。End 显示 incoming flow summary，`returnType` 编辑写入 `schema.returnType`，`returnValueExpression` 写入当前 End object 的 `returnValue.raw`。

多个 End 共享同一个 schema-level `returnType`，表单显示提示；返回表达式的类型兼容性仍由现有 validation 做基础检查。`returnType.kind === "void"` 时，helper 会清空 End `returnValue`，并禁用表达式编辑。

## 7. TypeDescriptor Strategy

当前源码真实类型系统是 `MicroflowDataType`，并兼容 `MicroflowTypeRef`。本轮复用 `MicroflowDataType`，不引入新的 TypeDescriptor 结构。Parameter 与 End 类型选择器支持 primitive：`boolean/integer/long/decimal/string/dateTime`，End 额外支持 `void`。`object/list/enumeration/fileDocument/json` 保留现有选择能力，但 Object/List 的真实 entity metadata 不在本轮接入，UI 显示 Stage 19 warning，且不写入 mock entity。

## 8. Warning Strategy

空参数名显示 inline warning，并在 validation 中产生 `MF_PARAMETER_NAME_MISSING`。同一微流内参数名按 trim 后大小写不敏感比较，重复时显示 inline warning，并在 validation 中产生 `MF_PARAMETER_DUPLICATED`。Parameter rename 不自动重写 Decision、Variable、End return 等表达式，表单固定提示该风险。多 Start 通过表单 warning 与 validation 提示；多 End 通过表单提示共享 `returnType`。End return type mismatch 继续使用现有 `MF_END_RETURN_TYPE_MISMATCH`。

## 9. Verification

自动测试：

- `src/frontend/packages/mendix/mendix-microflow/src/schema/__tests__/microflow-parameters.test.ts` 覆盖 add/rename/update type/delete/duplicate Parameter，End `returnType` / `returnValue`，A/B schema 隔离，空名和重名校验。

手工验收建议：

- 打开 `/space/:workspaceId/mendix-studio/:appId`，分别在 `MF_ValidatePurchaseRequest` 与 `MF_CalculateApprovalLevel` 中配置参数和 End 返回值，确认保存请求为 `PUT /api/microflows/{activeMicroflowId}/schema`。
- 刷新后重新打开各自微流，确认 `parameters`、Start caption/description、End `returnType` 与 `returnValue.raw` 恢复，且两个微流互不污染。
