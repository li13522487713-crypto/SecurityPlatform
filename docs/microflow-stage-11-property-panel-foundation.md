# Microflow Stage 11 - Property Panel Foundation

## 1. Scope

本轮完成：

- selectedObject / selectedFlow 属性加载。
- node / action / flow 基础表单分发。
- caption、documentation、parameter、decision、loop、action config、edge label/case 等基础字段编辑。
- 表单变更通过 `onObjectChange` / `onFlowChange` / `onSchemaChange` 写回当前 `MicroflowAuthoringSchema`。
- 空配置兼容与 inline warning。
- dirty 状态通过 `MicroflowEditor.commitSchema` 同步。
- 保存刷新恢复复用 Stage 06 的 `PUT /api/microflows/{id}/schema`。
- A/B 微流属性更新隔离测试。

本轮不做：

- 不接 Call Microflow 真实 metadata。
- 不接 Domain Model metadata。
- 不实现表达式引擎。
- 不实现执行引擎。
- 不做 trace/debug。
- 不做 schema migration。

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/forms/object-base-form.tsx` | 修改 | 增加只读 ID；caption 空值提示。 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/forms/parameter-object-form.tsx` | 修改 | Parameter name 空值提示，继续写回参数 schema。 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/forms/exclusive-split-form.tsx` | 修改 | Decision expression 空值提示，分支 summary 继续来自真实 flows。 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/forms/action-activity-form.tsx` | 修改 | Call Microflow / REST / Object / Retrieve / Change Variable 空配置 warning。 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/forms/generic-action-fields-form.tsx` | 修改 | 通用 action 必填字段 warning；unknown action 只读 JSON fallback。 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/selectors/EntitySelector.tsx` | 修改 | mock metadata catalog 下禁用实体选择，避免把 Sales.* 当真实目标。 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/selectors/MicroflowSelector.tsx` | 修改 | mock metadata catalog 下禁用微流选择，避免把 mock 目标当真实目标。 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/utils/schema-patch.ts` | 修改 | 增强纯 schema update helper，支持 nested object/flow，并补 caption/description/flow label helper。 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/property-update-helpers.spec.ts` | 新增 | 覆盖属性更新 helper、action config、parameter、nested flow、A/B 隔离。 |
| `docs/microflow-stage-01-gap-matrix.md` | 修改 | 更新 Stage 11 属性面板状态。 |

## 3. Property Panel Dispatch Matrix

| 选中对象类型 | 判断字段 | 渲染表单 | 可编辑字段 | 是否写回 schema |
|---|---|---|---|---|
| 无选择 | `!selectedObject && !selectedFlow` | Empty state | 无 | 不写回 |
| 连线 | `selectedFlow` | `FlowEdgeForm` | label、description、line、caseValues、error handler 字段 | 是，`onFlowChange` -> `updateFlow` |
| Start | `object.kind === "startEvent"` | `ObjectBaseForm` + `EventNodesForm` | caption、documentation、disabled、trigger | 是 |
| End | `object.kind === "endEvent"` | `ObjectBaseForm` + `EventNodesForm` | caption、documentation、returnValue | 是 |
| Parameter | `object.kind === "parameterObject"` | `ObjectBaseForm` + `ParameterObjectForm` | parameter name、dataType、required、default/example value | 是，`onSchemaChange` 更新 `parameters` |
| Annotation | `object.kind === "annotation"` | `ObjectBaseForm` + `AnnotationObjectForm` | text、colorToken、pinned、exportToDocumentation | 是 |
| Decision / If | `object.kind === "exclusiveSplit"` | `ObjectBaseForm` + `ExclusiveSplitForm` | split type、result type、expression/rule、enum type | 是 |
| Merge | `object.kind === "exclusiveMerge"` | `ObjectBaseForm` + `MergeNodeForm` | caption、documentation；merge summary 只读 | 是 |
| Loop | `object.kind === "loopedActivity"` | `ObjectBaseForm` + `LoopNodeForm` | loop source、list variable、iterator、while expression、error handling | 是 |
| Break / Continue | `breakEvent` / `continueEvent` | `ObjectBaseForm` + control event hint | caption、documentation | 是 |
| Action Activity | `object.kind === "actionActivity"` | `ActionActivityForm` / `GenericActionFields` | action-specific config | 是 |
| Unknown action | action kind 无表单定义 | `GenericActionFields` fallback | JSON 只读 summary | 不崩溃 |

## 4. Form Capability Matrix

| 节点/动作 | 支持字段 | 空配置处理 | warning | 本轮状态 |
|---|---|---|---|---|
| Start | caption、documentation、trigger | 不崩溃 | caption 空提示 | 已完成 |
| End | caption、documentation、returnValue | void return 禁用输入 | caption / validator 提示 | 已完成 |
| Parameter | name、dataType、required、default/example | 空 name 保持待配置 | name 空提示 | 已完成 |
| Annotation | text、color、pinned、export flag | 空 text 不崩溃 | caption 空提示 | 已完成 |
| Decision | expression/rule、result type、branch summary | 空 expression 不崩溃 | expression 空提示 | 已完成 |
| Merge | merge strategy、incoming/outgoing count | 只读 summary | caption 空提示 | 已完成 |
| Loop | iterable/while、list variable、iterator、while expression | 空 list/condition 不崩溃 | validator/field warning | 已完成 |
| Create Variable | variableName、dataType、initialValue | 空值保存为待配置 | validator warning | 已完成 |
| Change Variable | targetVariableName、newValueExpression | 空 target 不崩溃 | target 空提示 | 已完成 |
| Object Actions | entity、output variable、member changes、commit flags | 空 entity 不崩溃 | entity 空提示 | 已完成 |
| List / Collection | list/entity/operation/expression/output | 空 list/entity 不崩溃 | 通用 required warning | 已完成 |
| Call Microflow | target、parameterMappings、returnValue、callMode | mock catalog 禁用选择，target 可保持空 | target 空提示 | 已完成 |
| REST Call | method、urlExpression、headers、query、body、response | 空 URL 不崩溃 | URL 空提示 | 已完成 |
| Flow / Edge | label、description、caseValues、line、source/target readonly、delete | 空 label/case 不崩溃 | validator/field warning | 已完成 |

## 5. Schema Update Strategy

属性面板不维护独立业务 state。字段变更路径：

`UI input` -> form handler -> `onObjectChange` / `onFlowChange` / `onSchemaChange` -> `updateObject` / `updateFlow` / `updateParameter` -> `commitSchema` -> dirty=true -> canvas/property panel re-render -> save -> `PUT /api/microflows/{id}/schema`。

本轮涉及 helper：

- `updateObject`
- `updateObjectCaption`
- `updateObjectDescription`
- `updateActionActivity`
- `updateActionConfig`
- `updateFlow`
- `updateFlowLabel`
- `updateFlowEditor`
- `updateParameter`

这些 helper 是纯函数，不依赖 React，不调用 API，不使用 mock metadata。nested object/flow 更新通过递归 `objectCollection` 保留 loop 内 schema-bound 行为。

## 6. Selection Isolation

- `MicroflowEditor` 从当前 schema 的 `editor.selection.objectId` / `flowId` 派生 `selectedObject` / `selectedFlow`。
- 选中节点和连线只更新当前 editor instance 的 selection。
- 删除对象/连线后，Stage 09/10 的删除 helper 会清理 selection，属性面板回到 empty state。
- 嵌入式编辑器 key 包含 `resource.id:schemaId:version`，切换 microflow 会 remount，避免沿用上一微流 selection。

## 7. Save & Reload Verification

属性字段修改后进入当前 `MicroflowAuthoringSchema`：

- 节点 caption/documentation 保存到 object。
- Parameter 字段保存到 `schema.parameters`。
- Decision expression 保存到 `splitCondition.expression`。
- Action config 保存到 `object.action`。
- Edge label/case 保存到 flow。

保存时由 Stage 06 save bridge 调用 `PUT /api/microflows/{activeMicroflowId}/schema`。刷新后重新加载该 schema，属性面板从 schema 派生显示值。

## 8. Verification

自动验证：

- `pnpm --filter @atlas/microflow run typecheck` 通过。
- `pnpm exec vitest run packages/mendix/mendix-microflow/src/microflow-interactions.spec.ts packages/mendix/mendix-microflow/src/property-panel/property-update-helpers.spec.ts` 通过，38 个测试全部通过。

新增测试覆盖：

- update object caption / documentation helper。
- update action config helper。
- update parameter config helper。
- update nested loop flow label helper。
- A/B schema 属性更新互不影响。
- Stage 07-10 交互回归测试继续通过。

手工验收建议：

1. 打开 `/space/:workspaceId/mendix-studio/:appId`。
2. 打开 `MF_ValidatePurchaseRequest`。
3. 分别选中 Start / Parameter / Decision / REST Call / Call Microflow / Flow。
4. 修改 caption、parameter name/type、Decision expression、REST method/url、edge label/case。
5. 保存，确认 Network `PUT /api/microflows/{id}/schema` body 包含修改。
6. 刷新后重新打开，确认属性恢复。
7. 打开另一个微流，确认不显示上一微流 selection 或属性。
8. 在 mock metadata catalog 下确认 Call Microflow / Entity selector 不显示 Sales.* / mock 目标为真实选项。
