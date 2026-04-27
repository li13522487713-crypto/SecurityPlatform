# Microflow Stage 09 - Node Editing Persistence

## 1. Scope

本轮完成：

- 节点移动持久化：FlowGram move patch 写回 `MicroflowObject.relativeMiddlePoint`。
- 节点删除持久化：删除 object，并同步删除所有引用该 object 或其 descendant 的 flow。
- 节点复制持久化：复制当前 object 到同一 collection，生成新 id、新 action/parameter id、新 caption、新 offset position，默认不复制原连线。
- 节点名称 / caption 编辑持久化：属性面板基础 Caption 写回 schema，画布 label 随 schema 刷新。
- 基础 documentation 编辑继续通过属性面板写回 schema。
- ParameterObject 删除同步清理 `schema.parameters` 中对应 parameter。
- dirty 状态继续通过 `commitSchema` 与 Stage 06 `onDirtyChange` 上抛到当前 Workbench tab。
- 保存刷新恢复继续复用 Stage 06 真实保存链路。
- A/B schema move/delete/duplicate/rename 隔离新增自动测试覆盖。

本轮不做：

- 不新增节点类型。
- 不做连线创建/删除专项增强，第 10 轮处理。
- 不接入 `Call Microflow` metadata。
- 不接入 Domain Model metadata。
- 不实现 runtime / trace / execution engine。
- 不做 schema migration。

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-microflow/src/adapters/authoring-operations.ts` | 修改 | 加固 `deleteObject` / `duplicateObject`，增加 schema-aware id、参数清理、同 collection 复制、selection 更新。 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/forms/object-base-form.tsx` | 修改 | 基础 Caption 编辑增加空值防御，避免写入空 caption。 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/forms/action-activity-form.tsx` | 修改 | Action Activity Caption 编辑增加空值防御。 |
| `src/frontend/packages/mendix/mendix-microflow/src/microflow-interactions.spec.ts` | 修改 | 增加 move/delete/duplicate/rename/A-B 隔离测试。 |
| `docs/microflow-stage-01-gap-matrix.md` | 修改 | 更新 Stage 09 节点基础编辑持久化状态。 |

## 3. Move Data Flow

`FlowGram node move` -> `useFlowGramMicroflowBridge` detects `flowGramPositionPatch` -> `applyEditorGraphPatchToAuthoring` -> `moveObject` -> update `MicroflowObject.relativeMiddlePoint` -> `commitSchema` -> dirty=true -> save -> `PUT /api/microflows/{id}/schema` -> reload restores position.

| 移动环节 | 源码路径 | 函数/事件 | 当前行为 | 本轮处理 |
|---|---|---|---|---|
| FlowGram 内容变更 | `flowgram/hooks/useFlowGramMicroflowBridge.ts` | `doc.onContentChange` | 检测 FlowGram JSON 与 schema 差异 | 保持。 |
| 位置 patch | `flowgram/adapters/flowgram-to-authoring-patch.ts` | `flowGramPositionPatch` | 生成 movedNodes patch | 保持。 |
| schema 写回 | `adapters/authoring-operations.ts` | `moveObject` | 更新 `relativeMiddlePoint` | 自动测试覆盖。 |
| dirty | `editor/index.tsx` | `commitSchema` | 与 saved schema 比较后 dirty=true | 复用。 |

## 4. Delete Data Flow

`select node` -> Delete/Backspace 或属性面板 delete -> `deleteObject(schema, objectId)` -> collect object descendants -> remove object -> remove related root/nested flows -> remove deleted ParameterObject 的 parameter -> clear invalid selection -> dirty=true -> save -> reload confirms removal.

删除关联连线策略：

- 删除 root `schema.flows` 中 origin/destination 命中被删 object 或 descendant object 的 flow。
- 递归清理 loop `objectCollection.flows` 中 origin/destination 命中被删 object 或 descendant object 的 flow。
- 删除 loop 时 descendant object 一并纳入清理集合。
- 删除 ParameterObject 时同步删除 `schema.parameters` 中对应 `parameterId`。

## 5. Duplicate Data Flow

`select node` -> property panel duplicate -> `duplicateObject(schema, objectId)` -> clone object only -> generate new object id/stableId -> regenerate action id 或 parameter id -> offset position by `x+80/y+60` -> caption `${source caption} Copy` 或 parameter unique name -> add to same collection -> select duplicate -> dirty=true -> save -> reload restores duplicate.

本轮默认不复制 incoming/outgoing flows，避免误连原节点。Start Event 仍保持单入口语义，复制 Start 会被 helper 忽略。

## 6. Rename Data Flow

`property panel caption edit` -> `ObjectBaseForm` / `ActionActivityForm` emits object patch -> `updateObject(schema, objectId, ...)` -> object caption in schema updates -> `toEditorGraph` / `authoringToFlowGram` refresh canvas label -> dirty=true -> save -> reload restores caption.

空 caption 防御：

- 基础 Object caption 输入 trim 后为空时回退到现有 caption 或 object kind。
- Action Activity caption 输入 trim 后为空时回退到现有 caption 或 action kind。

## 7. Schema Helper Contract

| helper | 输入 | 输出 | 是否纯函数 | 用途 |
|---|---|---|---|---|
| `moveObject(schema, objectId, position)` | schema、object id、position | 更新后的 schema | 是 | 移动节点。 |
| `deleteObject(schema, objectId)` | schema、object id | 删除 object/flow/parameter 后的 schema | 是 | 删除节点与相关连线。 |
| `duplicateObject(schema, objectId)` | schema、object id | 同 collection 新增复制 object 后的 schema | 是 | 复制节点本体，不复制 flow。 |
| `updateObject(schema, objectId, mapper)` | schema、object id、mapper | 更新后的 schema | 是 | caption/documentation/action 属性更新。 |
| `applyEditorGraphPatchToAuthoring(schema, patch)` | schema、graph patch | 更新后的 schema | 是 | FlowGram 与属性面板统一 patch 入口。 |

## 8. Dirty Strategy

- `MicroflowEditor.commitSchema` 是 dirty 入口。
- move/delete/duplicate/rename 都最终调用 `commitSchema`，并与 `savedSchemaRef.current` 比较。
- dirty 通过 `props.onSchemaChange` 上抛到 `MendixMicroflowEditorEntry`，再由 Stage 06 `onDirtyChange` 标记当前 `microflow:{id}` Workbench tab。
- 保存成功后 `savedSchemaRef.current = schema` 且 `setDirty(false)`，Stage 06 `onSave` 同步 tab dirty=false。
- 保存失败时 catch 分支不会更新 `savedSchemaRef`，dirty 不会被当作已保存清掉。
- A/B tab dirty 隔离依赖 Stage 05/06 的 `activeWorkbenchTabId` 与 `MicroflowEditor` key remount。

## 9. Isolation Verification

自动测试新增 A/B schema 隔离场景：

- `schemaA` 执行 move + duplicate + rename。
- `schemaB` 执行 delete。
- 断言 A 保留自己的 renamed/duplicated object，B 的 objectCollection 被清空，A/B object 不互相引用。

运行时隔离继续依赖：

- Stage 05 `microflow:{microflowId}` tab。
- Stage 06 `StudioEmbeddedMicroflowEditor` 按 active microflow 加载 schema。
- Stage 06 `MicroflowEditor` key 包含 resource id/schema id/version。
- Stage 06 save bridge 使用当前 resource id 调用 `PUT /api/microflows/{id}/schema`。

## 10. Verification

自动验证：

- `pnpm --filter @atlas/microflow run typecheck` 通过。
- `pnpm exec vitest run packages/mendix/mendix-microflow/src/microflow-interactions.spec.ts` 通过，29 个测试全部通过。

测试覆盖：

- move node 后 schema `relativeMiddlePoint` 更新，原 schema 不变。
- delete node 后 objectCollection 移除，相关 flows 移除。
- delete ParameterObject 后对应 `schema.parameters` 移除，selection 清理。
- duplicate node 后 id 不同、action id 不同、position 偏移、caption 为 Copy、不复制 flows。
- rename node 后 caption 写入 schema，canvas graph title 更新。
- A/B schema move/delete/duplicate/rename 互不影响。

手工验收建议：

1. 打开 `/space/:workspaceId/mendix-studio/:appId`。
2. 打开真实微流 `MF_ValidatePurchaseRequest`。
3. 拖入 Start / Decision / End 并保存。
4. 移动 Start 到左上、End 到右下，保存并刷新，确认位置恢复。
5. 选中 Decision，在属性面板复制，确认新 Decision id 不同且位置偏移。
6. 重命名 End 为 `End_Validate`，确认画布 label 更新，保存刷新后恢复。
7. 删除复制出的 Decision，保存刷新后确认消失；如有关联 flow，确认 flow 一并消失。
8. 打开 `MF_CalculateApprovalLevel`，确认不显示上一个微流的编辑结果。
9. 在第二个微流执行移动/复制/删除/重命名，保存刷新后分别打开两个微流，确认互不污染。
10. 模拟保存失败，确认 dirty 不被清除。
