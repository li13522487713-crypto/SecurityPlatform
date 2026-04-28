# Microflow Release Stage 06 - Canvas Core Interactions

## 1. Scope

本轮完成真实资源编辑器里的画布核心交互强化：Toolbox 拖拽节点进入当前 active `microflowId` 的 `MicroflowAuthoringSchema`，节点具备稳定 `id/kind/caption/relativeMiddlePoint/size/action|config/editor`，移动、删除、复制、连线增删、selection、viewport、dirty、undo/redo 与保存链路都绑定当前 editor schema。

本轮不做 Toolbox 发布化治理、属性面板深度表单、Call Microflow 真实 metadata、Domain Model metadata、Validation/Problems 专项、publish/run/trace、执行引擎、mock API、孤立 demo 页面或历史 schema migration。

## 2. Stage 0 Hotfix Status

第 0 轮 Hotfix 不作为本轮启动前置约束。源码审计显示 Hotfix 当前基本通过；若后续真实联调发现回归，只记录为 Release Blocker，不阻塞 Stage 06。

| Hotfix 检查项 | 当前状态 | 是否阻塞本轮 | 后续处理 |
|---|---|---|---|
| 是否仍有 uncaught promise | `CreateMicroflowModal` 已 catch `onSubmit` rejection | 否 | 继续保留前端/后端 Hotfix 测试 |
| 是否仍所有错误显示服务不可用 | 已按 status/code/message/traceId/fieldErrors 区分 | 否 | 后续统一 action-aware 文案 |
| 是否仍默认 moduleId=sales | 目标页不默认 sales；目标页 moduleId 仍来自 sample Procurement module | 否 | 真实 app/module 资产树后续轮次处理 |

## 3. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-microflow/src/editor/index.tsx` | 修改 | 补 copy/paste、viewport dirty/save、schema-change dirty 通知边界、唯一 flow id 调用 |
| `src/frontend/packages/mendix/mendix-microflow/src/editor/shortcuts/useMicroflowShortcuts.ts` | 修改 | 补 Ctrl/Cmd+C、Ctrl/Cmd+V 当前 editor 快捷键 |
| `src/frontend/packages/mendix/mendix-microflow/src/adapters/authoring-operations.ts` | 修改 | 新增 `createMicroflowObjectId`、`createMicroflowFlowId`，flow factory 支持外部传入唯一 id |
| `src/frontend/packages/mendix/mendix-microflow/src/flowgram/adapters/flowgram-edge-factory.ts` | 修改 | FlowGram 新连线使用当前 schema 内唯一 flow id |
| `src/frontend/packages/mendix/mendix-microflow/src/adapters/__tests__/authoring-operations.test.ts` | 新增 | 覆盖 id、move、delete 清线、duplicate、A/B schema 隔离 |
| `docs/microflow-p1-release-gap.md` | 修改 | 更新 Canvas core interactions 状态与 Stage 06 结论 |
| `docs/microflow-release-stage-06-canvas-core-interactions.md` | 新增 | 本轮链路盘点、策略与验收记录 |

## 4. Canvas Interaction Chain

| 环节 | 源码路径 | 函数/组件 | 当前行为 | 是否写回 authoring schema | 是否保存刷新恢复 | 缺口 | 本轮处理 |
|---|---|---|---|---|---|---|---|
| NodePanel 渲染 | `mendix-microflow/src/node-panel/index.tsx` | `MicroflowNodePanel` | 渲染 registry 节点、支持搜索/收藏/拖拽 | 不直接写 | N/A | Toolbox 治理未做 | 保持 |
| 节点 drag start | `node-panel/index.tsx` | `MicroflowNodeCard.onDragStart` | 写 `application/x-atlas-microflow-node` payload | 不直接写 | N/A | 无 | 保持 |
| drag payload | `node-registry/registry.ts` | `createDragPayloadFromRegistryItem` | 携带 registryKey、objectKind、actionKind、defaultConfig | 不直接写 | N/A | 发布化分类后续 | 保持 |
| FlowGram canvas drop | `flowgram/FlowGramMicroflowCanvas.tsx` | `handleDrop` | 校验 payload 与 disabled 状态后回调宿主 | 通过宿主写 | 是 | 多选 drop 不涉及 | 保持 |
| drop 坐标转换 | `FlowGramMicroflowCanvas.tsx`; `flowgram-coordinate.ts` | `dropPointFromEvent` | 使用 FlowGram mouse position，fallback 使用 viewport 反算并 snap | 是 | 是 | 需浏览器手工验收 zoom/pan | 保持 |
| registry item -> MicroflowObject | `node-registry/drag-drop.ts`; `adapters/authoring-operations.ts` | `addMicroflowObjectFromDragPayload`; `createObjectFromRegistry` | 创建 authoring object 与参数/动作配置 | 是 | 是 | Toolbox 默认配置治理后续 | 保持并测试 |
| objectCollection 更新 | `adapters/authoring-operations.ts` | `addObject`; `applyEditorGraphPatchToAuthoring` | 写入 root 或 loop collection | 是 | 是 | loop 深层 E2E 待补 | 保持 |
| FlowGram node 渲染 | `flowgram/adapters/authoring-to-flowgram.ts` | `authoringToFlowGram` | authoring object 转 FlowGram node JSON | 读取 schema | 是 | 无 | 保持 |
| selection 更新 | `useFlowGramMicroflowBridge.ts`; `editor/index.tsx` | `onSelectionChanged`; `onSelectionChange` | 单选 node/edge/empty 同步到 `schema.editor.selection` | 是 | selection 不标 dirty | 多选 selection 未发布化 | 保持 |
| node move | `useFlowGramMicroflowBridge.ts`; `flowgram-to-authoring-patch.ts` | `flowGramPositionPatch` | FlowGram 内容变化转 `movedNodes` patch | 是 | 是 | 需手工确认快速移动最后位置 | 保持 |
| node delete | `editor/index.tsx`; `authoring-operations.ts` | `handleDeleteSelection`; `deleteObject` | 删除 object、descendants、相关 flows、selection | 是 | 是 | 无确认 UX | 补测试 |
| node duplicate | `property-panel`; `editor/index.tsx`; `authoring-operations.ts` | `duplicateObject`; copy/paste | 生成新 id、新 position、新 caption，不复制 flows | 是 | 是 | 多选复制后续 | 补快捷键 |
| copy/paste | `editor/index.tsx`; `useMicroflowShortcuts.ts` | `handleCopySelection`; `handlePasteSelection` | 当前微流内复制单节点，粘贴调用 `duplicateObject` | 是 | 是 | 本轮禁止跨微流粘贴 | 新增 |
| edge cleanup | `authoring-operations.ts` | `deleteObject`; `deleteFlow` | 删除节点时清理 root/nested flows 与 selection | 是 | 是 | 无 | 补测试 |
| viewport | `editor/index.tsx`; `FlowGramMicroflowToolbar` | `onViewportChange` | 写 `schema.editor.viewport/zoom` | 是 | 是 | 需真实浏览器确认 pan 事件覆盖 | 改为 dirty |
| undo/redo | `editor/index.tsx`; `history/*` | `MicroflowHistoryManager` | 每个 editor instance 独立 manager，key 含 id/schema/version | 是 | history 不持久 | 顶部 workbench 按钮仍未接 editor state | 保持并记录 |
| dirty state | `editor/index.tsx`; `MendixMicroflowEditorEntry.tsx` | `commitSchema`; `onSchemaChange` | create/move/delete/duplicate/flow/viewport 标 dirty；selection 不标 dirty | 是 | 是 | 保存失败保持 dirty 依赖 catch | 修正 viewport/selection |
| save bridge | `mendix-studio-core/src/microflow/editor/editor-save-bridge.ts` | `createMicroflowEditorApiClient.saveMicroflow` | 调 `resourceAdapter.saveMicroflowSchema(resource.id, schema)` | 是 | 是 | conflict UX 后续 | 保持 |
| reload schema -> canvas | `MicroflowResourceEditorHost.tsx`; `MendixMicroflowEditorEntry.tsx` | `getMicroflowSchema`; editor key | GET 当前 microflow schema 后按 key remount | 是 | 是 | 请求慢返回用 seq ignore | 保持 |

## 5. Node Creation Contract

节点创建入口是 `FlowGramMicroflowCanvas.handleDrop` 或 NodePanel double-click/context add，统一进入 `MicroflowEditor.handleAddNode`，再调用 `addMicroflowObjectFromDragPayload` 写入当前 `schema.objectCollection` 或 loop child collection。

新节点 contract：`id/stableId` 来自当前 schema 内唯一 helper，`kind/officialType` 来自 registry 映射，`caption` 使用 registry title/defaultCaption，`relativeMiddlePoint` 使用 drop/quick add 坐标，`size` 使用 registry render size，action node 生成真实 `MicroflowAction`，event/decision/loop/parameter/annotation 生成对应 authoring 字段，`editor` 保存 icon/color/selection。创建后 selection 指向新节点，dirty=true，保存进入 `PUT /api/microflows/{id}/schema` body。

## 6. Position / Viewport Strategy

Drop 坐标优先使用 FlowGram `getPosFromMouseEvent`，fallback 通过 `clientPointToFlowGramPoint` + `flowGramPointToAuthoringPoint` 按 `schema.editor.viewport` 做 pan/zoom 反算，再 `snapMicroflowPoint` 对齐网格。节点移动由 FlowGram JSON 的 `meta.position` 差异生成 `movedNodes` patch，写回 `relativeMiddlePoint`。

`MicroflowAuthoringSchema.editor.viewport` 已存在。本轮将 viewport 改为 dirty 操作，pan/zoom/fit view 后可随保存持久化；minimap/grid 写入 `schema.editor.showMiniMap/gridEnabled`，不破坏 object/flow 编辑状态。

## 7. Delete / Duplicate Strategy

删除使用 `deleteObject(schema, objectId)`：删除 object 与 loop descendants，过滤 root `schema.flows` 和 nested `objectCollection.flows` 中 origin/destination 指向被删节点的 flow，同时清理 selection 与属性面板选中对象。

复制使用 `duplicateObject(schema, objectId)`：深拷贝 object，重新生成 object/action/parameter/loop collection id，position 固定偏移 `{ x:+80, y:+60 }`，caption 追加 `Copy`，默认不复制 incoming/outgoing flows。Ctrl/Cmd+C 仅记录当前微流内单节点；Ctrl/Cmd+V 只允许在同一 `microflowId` 粘贴，跨微流粘贴本轮明确禁用。

## 8. Selection / Multiselect Strategy

FlowGram selection 通过 `WorkflowSelectService.onSelectionChanged` 同步为 `{ objectId, flowId, collectionId }`，写入当前 editor instance 的 `schema.editor.selection`。点击节点/连线选中，空白或 Esc 清空，删除节点/连线后清空悬挂 selection。selection 不标 dirty，也不通知 workbench dirty。

当前仅确认单选稳定。FlowGram 框选/多选没有完整 schema-bound 批量操作契约，本轮记录为后续缺口。

## 9. Undo / Redo Strategy

`MicroflowEditorInner` 每个组件实例持有自己的 `MicroflowHistoryManager`，`MicroflowResourceEditorHost` 与 `MendixMicroflowEditorEntry` 的 key 包含 `microflowId/schemaId/version`，因此 A/B/C tab 的 history 不复用。create/move/delete/duplicate/flow/grid/minimap 进入当前实例 history；selection 与 viewport 不入 history。保存后按现有策略 `replaceCurrent`，不跨 tab 清空历史。

Workbench 顶部全局 Undo/Redo 按钮仍是 store 预留状态，尚未接 editor 内部 history，这是后续集成缺口。

## 10. Dirty Strategy

以下操作 dirty=true：拖拽/双击创建节点、移动节点、删除节点/连线、复制/粘贴节点、创建连线、grid/minimap 切换、viewport 持久化变更、属性面板 schema 修改、auto layout。selection、runtime trace 定位、问题定位选择不标 dirty。

保存成功后 editor 内 dirty=false，`MicroflowResourceEditorHost` 仅在返回 resource id 等于当前 microflowId 时清理当前 tab dirty；保存失败保留 dirty。A/B/C dirty 通过 `dirtyByWorkbenchTabId` 独立记录。

## 11. Save & Reload Verification

真实保存路径为 `MicroflowEditor.saveCurrentSchema` -> `createMicroflowEditorApiClient.saveMicroflow` -> `resourceAdapter.saveMicroflowSchema(resource.id, schema)` -> HTTP adapter `PUT /api/microflows/{microflowId}/schema`。刷新后由 `MicroflowResourceEditorHost` 重新调用 `GET /api/microflows/{id}/schema` 加载保存后的 schema，再由 `authoringToFlowGram` 还原画布。

本轮已通过单元测试覆盖 schema 层 create/move/delete/duplicate/isolation；真实浏览器保存刷新恢复需按手工步骤验证。

## 12. Isolation Verification

A/B/C 隔离依赖四层：tab id 为 `microflow:{microflowId}`；资源宿主按 prop `microflowId` 发送 GET/PUT；editor key 含 `microflowId/schemaId/version`；history/clipboard/schema state 位于 editor instance 内。跨微流粘贴本轮禁用，避免源微流对象带 id 进入目标微流。

## 13. Verification

自动测试：

- `pnpm exec vitest run packages/mendix/mendix-microflow/src/adapters/__tests__/authoring-operations.test.ts`
- `pnpm --filter @atlas/microflow run typecheck`

手工验收步骤：

1. 启动 AppHost 与 AppWeb。
2. 打开 `/space/:workspaceId/mendix-studio/:appId`。
3. 展开 Procurement / Microflows，打开 `MF_ValidatePurchaseRequest`。
4. 拖入 Start 与 End，确认 dirty=true。
5. 移动 Start/End，保存，确认 PUT `/api/microflows/{A.id}/schema`。
6. 刷新后重新打开 A，确认节点与位置恢复。
7. 复制 End，确认新 id、新位置，保存刷新恢复。
8. 删除复制节点，保存刷新后不再出现。
9. 创建 Start -> End 连线，删除 Start，确认连线清理，保存刷新后无悬挂连线。
10. 打开 `MF_CalculateApprovalLevel` 与第三个微流，确认 A/B/C 创建、移动、删除、dirty、undo/redo 不串。
11. 确认没有真实保存调用 `createLocalMicroflowApiClient` 或 localStorage，也没有真实画布展示 `sampleOrderProcessingMicroflow`。
