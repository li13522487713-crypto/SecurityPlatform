# Microflow Stage 13 - Variable Foundation

## 1. Scope

本轮完成 Create Variable 节点配置、Change Variable 节点配置、schema-level `variables` / variable index 同步、variable selector、变量 name/type 基础校验、变量 rename/delete/copy 同步、表达式可用变量基础提示、dirty 状态接入、保存刷新恢复和 A/B 微流隔离。

本轮不做表达式执行引擎、全局表达式自动重写、运行输入面板、Call Microflow metadata、Domain Model metadata、Object/List 真实实体绑定、trace/debug 和历史 schema migration。

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-microflow/src/variables/microflow-variable-foundation.ts` | 新增 | 变量索引、Create/Change Variable 同步、冲突和引用探测纯 helper。 |
| `src/frontend/packages/mendix/mendix-microflow/src/variables/index.ts` | 修改 | 导出 Stage 13 helper。 |
| `src/frontend/packages/mendix/mendix-microflow/src/variables/variable-index.ts` | 修改 | 变量名大小写不敏感冲突、parameter 冲突 warning。 |
| `src/frontend/packages/mendix/mendix-microflow/src/validators/validate-variables.ts` | 修改 | Change Variable target 按当前 schema variable index 校验，严格拓扑作用域留后续。 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/selectors/VariableSelector.tsx` | 修改 | 支持 `scopeMode="index"`，Change Variable 可从当前微流变量索引选择。 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/common/VariableNameInput.tsx` | 修改 | 展示 variable/parameter 冲突诊断。 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/forms/action-activity-form.tsx` | 修改 | Create/Change Variable 表单补 warning、description、类型提示和 index selector。 |
| `src/frontend/packages/mendix/mendix-microflow/src/adapters/authoring-operations.ts` | 修改 | Create/Change Variable 安全默认值；复制 Create Variable 生成新变量名。 |
| `src/frontend/packages/mendix/mendix-microflow/src/node-registry/action-registry.ts` | 修改 | Create Variable registry 默认 `newVariable:string`，不写 fake metadata。 |
| `src/frontend/packages/mendix/mendix-microflow/src/schema/__tests__/microflow-variables.test.ts` | 新增 | 覆盖变量索引、改名、类型、删除、复制、Change target、冲突、A/B 隔离。 |
| `docs/microflow-stage-01-gap-matrix.md` | 修改 | 更新 Stage 13 P1 状态。 |

## 3. Variable Schema Contract

| 语义 | 源码字段 | 类型 | 同步规则 |
|---|---|---|---|
| schema-level variables | `MicroflowAuthoringSchema.variables` | `MicroflowVariableIndex` | 由当前 schema 派生，保存时随 schema 序列化；不是手写 definitions 数组。 |
| parameters | `MicroflowAuthoringSchema.parameters` | `MicroflowParameter[]` | 进入 variable index，source=`parameter`，readonly=true。 |
| Create Variable action | `MicroflowCreateVariableAction` | action config | `variableName/dataType/initialValue/documentation/readonly` 是变量定义来源。 |
| Change Variable action | `MicroflowChangeVariableAction` | action config | `targetVariableName/newValueExpression` 保存到真实 action config。 |
| variable id | `MicroflowCreateVariableAction.id` | string | 当前 schema 无单独 variable id 字段，本轮用 action id 作为稳定变量身份。 |
| variable name | `action.variableName` | string | 改名后 variable index 重新派生；不自动重写表达式。 |
| variable type | `action.dataType` | `MicroflowDataType` | 修改后 variable index 重新派生。 |
| initialValueExpression | `action.initialValue` | `MicroflowExpression` | 仅保存表达式文本，不执行。 |
| newValueExpression | `action.newValueExpression` | `MicroflowExpression` | 仅保存表达式文本，不执行。 |
| targetVariableName / targetVariableId | `action.targetVariableName` / 无 id 字段 | string | 当前 schema 只支持 name 快照；selector 来源为当前微流 variable index。 |

## 4. Variable Index Strategy

变量索引由 `buildMicroflowVariableIndex(schema)` 基于当前 active microflow schema 计算，不读取其他微流，也不使用 mock metadata 作为变量来源。索引来源包括 schema-level parameters、Create Variable local variables，以及源码已有的 action output / loop / system 变量建模。Change Variable 本轮使用 `scopeMode="index"` 从当前 schema 索引选择变量；严格拓扑作用域阻断留到 Stage 20 validation。

A/B 微流隔离依赖 schema 入参：helper 只处理传入的 schema，不共享全局状态，不写 localStorage。

## 5. Create Variable Strategy

Create Variable 节点创建 action activity object，默认 `variableName="newVariable"`、`dataType={ kind: "string" }`，不包含 Sales.* 或 fake entity。变量定义由 createVariable action 派生：action id 是稳定 variable id，object id 是变量来源节点，变量名、类型、initialValue、documentation 均保存在 action config。

修改 variableName/dataType/initialValue/documentation 后，属性面板通过现有 `commitSchema` 写回 object，并由 `refreshDerivedState` 重建 `schema.variables`。删除节点后变量从派生 index 消失。复制节点时生成新 object id、新 action id，变量名采用 `${oldName}_Copy`，不复制连线。

## 6. Change Variable Strategy

Change Variable target selector 使用当前微流 `MicroflowVariableIndex`，展示 name/type/source/scope/readonly，不显示其他微流变量。selector 排除 system 和 readonly 变量；参数为 readonly，默认不可作为修改目标。当前 schema 没有 targetVariableId 字段，因此保存 `targetVariableName` name 快照。target 缺失时显示 warning，保存是否阻断沿用现有 validation。

`newValueExpression` 使用现有 `ExpressionEditor` 保存 `MicroflowExpression`，expectedType 来自当前 target 变量类型；本轮不做表达式执行。

## 7. Warning Strategy

空 variableName 使用现有 `MF_VARIABLE_NAME_REQUIRED` 诊断和 inline warning。变量重名按 trim 后大小写不敏感检查，产生 `MF_VARIABLE_DUPLICATED`。变量名与 parameter 冲突产生 `MF_VARIABLE_PARAMETER_CONFLICT` warning。变量重命名不自动重写表达式，表单显示固定提示；helper `getVariableReferences` 可基础探测 Change Variable target 与表达式 raw 文本引用。Object/List 类型的真实 entity metadata 留到 Stage 19，表单只提示不写 fake entity。

## 8. Verification

自动测试：

- `src/frontend/packages/mendix/mendix-microflow/src/schema/__tests__/microflow-variables.test.ts`
- 已覆盖 Create Variable 进入 index、rename/type/delete/duplicate、Change Variable target/expression、参数冲突、A/B 隔离。

手工验收建议：

- 在 `/space/:workspaceId/mendix-studio/:appId` 打开 `MF_ValidatePurchaseRequest`，拖入 Create Variable，配置 `approvalLevel:String` 和 initial value `"L1"`。
- 拖入 Change Variable，从 selector 选择 `approvalLevel`，配置 new value `"L2"`，保存并确认 `PUT /api/microflows/{activeMicroflowId}/schema` body 包含 createVariable、changeVariable 和 `variables` index。
- 刷新后重新打开该微流，确认变量与 target 恢复；再打开 `MF_CalculateApprovalLevel`，确认不显示前一微流的变量。
