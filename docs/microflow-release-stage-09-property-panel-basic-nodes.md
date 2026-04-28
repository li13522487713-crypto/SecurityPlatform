# Microflow Release Stage 09 - Property Panel Basic Nodes

## 1. Scope

本轮完成：
- document properties
- selected node / selected edge / no selection 分发
- Start form
- End form
- Parameter form
- Annotation form
- Decision form
- Merge form
- Edge form compatibility
- schema-bound form updates
- dirty state integration
- save reload recovery
- A/B/C selection isolation
- empty config crash guard

本轮不做：
- Variable actions deep form
- Object/List deep form
- Loop/Break/Continue deep form
- Call Microflow metadata selector
- Domain Model metadata selector
- full Problems panel
- publish/run/trace
- execution engine

依赖缺口：属性面板只能直接读取 `MicroflowAuthoringSchema`，没有注入完整 `MicroflowResource` 的 `schemaId/referenceCount/publishStatus/latestPublishedVersion` 等 resource-level 字段。本轮最小补齐点是在 no selection 状态新增 schema-bound document properties 表单，并把 resource-only 字段明确只读为 unavailable，不伪造 sample 或 mock 值。

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/index.tsx` | 修改 | no selection 改为渲染文档属性表单，保留 selectedFlow 优先分发 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/forms/microflow-document-properties-form.tsx` | 新增 | Microflow document properties，documentation 写回 schema |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/forms/object-base-form.tsx` | 修改 | caption 空值不再自动写回 fallback，增加 position 与 description 展示 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/forms/object-panel.tsx` | 修改 | unsupported node type 安全态 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/forms/event-nodes-form.tsx` | 修改 | Start 无出边 warning |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/forms/parameter-object-form.tsx` | 修改 | 使用 schema-bound parameter object helper，同步 schema-level parameters |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/selectors/DataTypeSelector.tsx` | 修改 | unknown/missing type 可安全展示并提示，不生成业务默认值 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/utils/schema-patch.ts` | 修改 | 增加第 9 轮显式 schema update helper |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/property-update-helpers.spec.ts` | 修改 | 覆盖 document、End、Parameter、Decision、Annotation、Merge 与 A/B 隔离 helper |
| `docs/microflow-p1-release-gap.md` | 修改 | 更新 Property Panel 基础节点状态 |

## 3. Property Panel Dispatch

`selectedFlow` -> `FlowEdgeForm`

`selectedObject` -> `ObjectPanel` -> Start / End / Parameter / Annotation / Decision / Merge

no selection -> `MicroflowDocumentPropertiesForm`

Unknown object kind 显示 “Unsupported node type”、node id、kind，不白屏，不写 fake data。已删除或不存在的 selection 在编辑器中解析为 `null`，进入 document properties 安全态。

## 4. Form Capability Matrix

| 表单 | 支持字段 | 写回 schema | warning | 保存刷新恢复 |
|---|---|---|---|---|
| Document Properties | id、name、displayName、qualifiedName、schemaVersion、returnType、description、documentation、parameters、audit、referenceCount unavailable | documentation | resource-only 字段说明 | 是 |
| Start | id、kind、caption、description、position、disabled、trigger、outgoing summary | caption、description、disabled、trigger | 空 caption、无出边、多 Start | 是 |
| End | id、kind、caption、description、position、returnType、returnValueExpression、incoming summary | caption、description、returnType、returnValue | 空 return value 不崩溃，多 End 共享 returnType | 是 |
| Parameter | parameter id、node id、name、dataType、required、defaultValue、exampleValue、description | schema.parameters + Parameter object 同步 | 空 name、重名、空/unknown type、Object/List metadata | 是 |
| Annotation | id、kind、caption、description、position、text、colorToken、pinned、exportToDocumentation | annotation object/editor | 空 text 不崩溃 | 是 |
| Decision | caption、description、expression、decision type、result type、branch summary | exclusiveSplit object | 空 expression、缺 true/false、重复 case、无出边 | 是 |
| Merge | caption、description、strategy、incoming/outgoing count、flow summary | exclusiveMerge object | 入边少于 2、无出边、多出边 | 是 |
| Edge | id、kind、officialType、source、target、label、description、case/error/loop fields | flow/editor/caseValues | missing case、duplicate branch | 是 |

## 5. Schema Update Strategy

helper 路径：`src/frontend/packages/mendix/mendix-microflow/src/property-panel/utils/schema-patch.ts`。

新增/整理 helper：
- `updateMicroflowDocumentProperties`
- `updateMicroflowObjectBase`
- `updateMicroflowObjectCaption`
- `updateMicroflowObjectDescription`
- `updateStartEventConfig`
- `updateEndEventConfig`
- `updateParameterObjectConfig`
- `updateAnnotationObjectConfig`
- `updateDecisionObjectConfig`
- `updateMergeObjectConfig`
- `updateMicroflowFlowBase`

表单更新通过 `onSchemaChange` 或 `onObjectChange` / `onFlowChange` 进入 `MicroflowEditor.commitSchema`。`commitSchema` 刷新派生状态、更新 React schema、调用宿主 `onSchemaChange`，`MendixMicroflowEditorEntry` 再触发 `onDirtyChange(true)`，目标页通过 `markWorkbenchTabDirty(activeMicroflowTabId, true)` 标记当前 tab。保存成功后 `onSaveComplete` 重新拉取 resource/schema 并 `onDirtyChange(false)`。画布 label 来自当前 schema 投影，因此 caption/label 修改后实时刷新。

## 6. Parameter Sync Strategy

当前同步状态：Parameter 节点有 `parameterId/parameterName/caption`，schema-level `parameters` 是权威参数定义。

本轮最小同步点：属性表单修改 name/type/required/defaultValue/description 时调用 `updateParameterObjectConfig`，内部复用 `renameMicroflowParameter`、`updateMicroflowParameterType`、`upsertMicroflowParameter`、`syncParameterDefinitionToObject`，保证 schema-level parameters 与 Parameter object caption/name 同步。

剩余缺口：删除/复制 Parameter 的同步已由画布/对象操作 helper 覆盖基础路径，但复杂历史 schema 或外部损坏 schema 的 repair/migration 本轮不做。

## 7. Expression Field Strategy

Decision expression 与 End returnValueExpression 都只保存 `MicroflowExpression.raw` 及现有 expression metadata，不执行表达式，不写 demo 表达式，不依赖 mock metadata。空 expression/returnValueExpression 只显示 warning 或允许空值，不自动填 `amount > 100`、`order.status` 等业务示例。

## 8. Empty Config Guard

- Parameter name 为空：warning，不崩溃。
- Parameter type 为空或 unknown：warning，不写默认业务类型。
- End returnValueExpression 为空：可保存空值或 undefined，不崩溃。
- End returnType 为空：按 schema 现有值展示；void 时禁用 return value。
- Decision expression 为空：warning，不执行。
- Decision 没有出边：warning。
- Merge 没有入边/出边：warning。
- Annotation text 为空：空文本写回 schema，不崩溃。
- Unknown object kind：Unsupported node type 安全态。
- selectedObject/selectedFlow id 不存在：解析为 no selection，显示 document properties。

## 9. Selection Isolation

目标页每个 microflow tab 渲染独立 `MicroflowResourceEditorHost` / `MendixMicroflowEditorEntry`，editor key 包含 `resource.id:schemaId:version`。A/B/C 切换会按 active workbench tab 重新加载/挂载对应 schema；selection 存在 `schema.editor.selection`，属于当前 schema，不写入全局 `store.microflowSchema`。异步保存刷新有 `saveRefreshSeqRef` 与 mounted guard，避免 stale response 覆盖当前资源。

## 10. Verification

自动验证：
- `pnpm --filter @atlas/microflow run typecheck`
- `pnpm exec vitest run packages/mendix/mendix-microflow/src/property-panel/property-update-helpers.spec.ts packages/mendix/mendix-microflow/src/schema/__tests__/microflow-parameters.test.ts`

手工验收建议：
1. 启动后端 `dotnet run --project src/backend/Atlas.AppHost`。
2. 启动前端 `pnpm run dev:app-web`。
3. 打开 `/space/:workspaceId/mendix-studio/:appId`。
4. 展开 Procurement / Microflows，打开 `MF_ValidatePurchaseRequest`。
5. 点击画布空白，确认 document properties。
6. 拖入并修改 Start、Parameter、Decision、End、Annotation、Merge。
7. 保存，确认 `PUT /api/microflows/{id}/schema` body 含 caption、parameter、expression、returnValue、annotation、merge 属性。
8. 刷新后重新打开 A，确认属性恢复。
9. 打开 B/C，确认 selection、表单值、dirty 不串。
10. 检查 Console 无新增 uncaught promise，不出现 `sampleOrderProcessingMicroflow`、`Sales.*`、`MF_ValidateOrder`，真实保存不走 `createLocalMicroflowApiClient` 或 localStorage adapter。

## 11. Current Capability Inventory

| 能力 | 源码路径 | 当前实现 | 是否写回 schema | 是否 dirty | 是否保存刷新恢复 | 缺口 | 本轮处理 |
|---|---|---|---|---|---|---|---|
| no selection / microflow document properties | `property-panel/index.tsx`; `forms/microflow-document-properties-form.tsx` | 空白画布显示 schema-bound document properties | 是，documentation | 是 | 是 | resource-only 字段未注入 | 新增表单并记录缺口 |
| selected node | `forms/object-panel.tsx` | ObjectPanel 按 kind 分发 | 是 | 是 | 是 | unknown 安全态不足 | 增加 unsupported node type |
| selected edge | `forms/flow-edge-form.tsx` | FlowEdgeForm 优先分发 | 是 | 是 | 是 | 仅基础属性 | 保持兼容 |
| Start form | `forms/event-nodes-form.tsx`; `forms/object-base-form.tsx` | caption/description/trigger/outgoing | 是 | 是 | 是 | 无出边提示不足 | 补 warning |
| End form | `forms/event-nodes-form.tsx` | returnType + returnValue | 是 | 是 | 是 | 表达式不执行 | 保持文本保存 |
| Parameter form | `forms/parameter-object-form.tsx` | name/type/required/default/description | 是 | 是 | 是 | 损坏 schema repair 不做 | 接 helper 同步 schema.parameters |
| Annotation form | `forms/annotation-object-form.tsx` | text/style/editor flags | 是 | 是 | 是 | 无深度样式系统 | 基础闭环 |
| Decision / exclusive split form | `forms/exclusive-split-form.tsx` | expression/type/branch summary | 是 | 是 | 是 | 不做 expression engine | 基础闭环 |
| Merge form | `forms/merge-node-form.tsx` | strategy + flow summary | 是 | 是 | 是 | strategy 只有源码支持的 firstArrived | 基础闭环 |
| Object base form | `forms/object-base-form.tsx` | id/kind/caption/description/position/disabled | 是 | 是 | 是 | 空 caption 不做自动修复 | 移除自动 fallback 写回 |
| Edge form | `forms/flow-edge-form.tsx` | id/source/target/label/description/case | 是 | 是 | 是 | 深度 routing 非本轮重点 | 兼容 |
| Expression editor | `property-panel/expression/*` | raw expression 编辑 | 是 | 是 | 是 | 不执行、不完整 IDE | 记录策略 |
| field-level warning | `forms/*`; `common`; `utils/issue-filter` | validation issue + inline warning | 部分 | 不适用 | 不适用 | Problems 面板不完整 | 补基础 warning |
| dirty state | `editor/index.tsx`; `MendixMicroflowEditorEntry.tsx`; `store.ts` | commitSchema -> onSchemaChange -> onDirtyChange -> tab dirty | 是 | 是 | 保存成功 false | conflict UX 后续 | 复用 |
| save body 验证 | `editor-save-bridge.ts`; `http-resource-adapter.ts` | `saveMicroflow({ schema })` -> `PUT /api/microflows/{id}/schema` | 是 | 是 | 是 | 本文档未做浏览器抓包 | 自动验证 helper + 手工步骤 |
| tab 切换 selection isolation | `MicroflowResourceEditorHost.tsx`; `MendixMicroflowEditorEntry.tsx`; `editor/index.tsx` | schema/editor key 按 microflow resource 隔离 | 是 | 是 | 是 | 浏览器快速切换 E2E 未补 | 单测覆盖 schema A/B 不污染 |
