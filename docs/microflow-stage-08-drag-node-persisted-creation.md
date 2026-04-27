# Microflow Stage 08 - Drag Node Persisted Creation

## 1. Scope

本轮完成：

- 盘点 NodePanel 到 FlowGram canvas，再到 authoring schema 的拖拽创建链路。
- 修复拖拽创建节点的 id 生成策略，改为基于当前 schema 的碰撞检测唯一 id。
- 确认拖拽 payload 会转换为真实 `MicroflowObject`、`MicroflowActionActivity`、event、parameter、annotation 等 authoring schema 对象。
- 确认新节点进入当前编辑器持有的真实 `MicroflowAuthoringSchema.objectCollection.objects`，不会进入全局 sample schema。
- 确认 drop position 写入 `relativeMiddlePoint`，FlowGram reload 后按 schema 重新渲染。
- 确认拖拽创建后通过 `commitSchema` 设置 dirty，并在保存后由 Stage 06 save bridge 调用真实 `PUT /api/microflows/{id}/schema`。
- 补充 A/B schema 隔离测试，验证分别向两个 schema 添加节点不会串数据。
- 继续沿用 Stage 07 默认配置工厂，确保新节点不再带 `Sales.*`、`MF_ValidateOrder`、demo 默认值。

本轮不做：

- 不新增节点类型。
- 不深改属性表单。
- 不接入 `Call Microflow` 真实 metadata。
- 不接入 Domain Model metadata。
- 不实现 runtime / trace / execution engine。
- 不做历史 schema migration。

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-microflow/src/node-registry/drag-drop.ts` | 修改 | 新增 schema-aware id 生成，拖拽创建时为 object / parameter / action 使用碰撞检测唯一 id。 |
| `src/frontend/packages/mendix/mendix-microflow/src/node-registry/factories.ts` | 修改 | action activity fallback id 改用 `createStableId`。 |
| `src/frontend/packages/mendix/mendix-microflow/src/adapters/authoring-operations.ts` | 修改 | registry 直接创建对象的 fallback id 改用 `createStableId`。 |
| `src/frontend/packages/mendix/mendix-microflow/src/node-registry/registry.ts` | 修改 | legacy registry helper fallback id 改用 `createStableId`。 |
| `src/frontend/packages/mendix/mendix-microflow/src/editor/index.tsx` | 修改 | 插入节点路径使用 schema-aware id；NodePanel 传入 `microflowId/moduleId/metadataAvailable` create context。 |
| `src/frontend/packages/mendix/mendix-microflow/src/microflow-interactions.spec.ts` | 修改 | 新增拖拽创建、id 唯一、position、demo 默认值、A/B schema 隔离测试；修正 nested loop flow 递归断言。 |
| `docs/microflow-stage-01-gap-matrix.md` | 修改 | 更新 Stage 08 拖拽创建、保存恢复、隔离状态。 |

## 3. Drag Creation Data Flow

| 环节 | 源码路径 | 函数/组件 | 当前行为 | 是否进入 authoring schema | 缺口 | 本轮处理 |
|---|---|---|---|---|---|---|
| NodePanel 渲染节点 | `node-panel/index.tsx` | `MicroflowNodePanel` / `MicroflowNodeCard` | 从 registry 分组渲染卡片，支持搜索、双击、拖拽 | 否 | 无 | 保持 Stage 07 分类与 warning。 |
| 节点拖拽开始 | `node-panel/index.tsx` | `onDragStart` | 写入 `application/x-atlas-microflow-node` payload | 否 | 无 | 保持 payload。 |
| 拖拽 payload 内容 | `node-registry/registry.ts` | `createNodeDragPayloadFromNodeRegistry` | 包含 registryKey、nodeType、objectKind、actionKind、defaultConfig | 否 | defaultConfig 已由 Stage 07 治理 | 本轮验证无 demo。 |
| FlowGram canvas 接收 drop | `flowgram/FlowGramMicroflowCanvas.tsx` | `handleDrop` | 解析 payload，校验 registry item，计算 drop position | 否 | 无 | 保持。 |
| drop position 计算 | `FlowGramMicroflowCanvas.tsx` / `flowgram-coordinate.ts` | `dropPointFromEvent` | client 坐标结合 viewport/zoom 转 authoring point 并 snap | 否 | 需文档化 | 本轮确认写入 schema position。 |
| registry 生成对象 | `node-registry/drag-drop.ts` / `factories.ts` | `addMicroflowObjectFromDragPayload` | 按 payload 创建 action/event/parameter/annotation/loop 等对象 | 是 | id 依赖 `Date.now()` | 改为 schema-aware 唯一 id。 |
| schema objectCollection 更新 | `adapters/authoring-operations.ts` | `applyEditorGraphPatchToAuthoring` | `addObject` 写入 root 或 loop collection | 是 | 无 | 保持 schema-first。 |
| FlowGram node 渲染 | `flowgram/adapters/authoring-to-flowgram.ts` | `authoringToFlowGram` | 从 authoring schema 转 FlowGram JSON | 是 | 无 | 保持 schema 为真相。 |
| selected object 设置 | `node-registry/drag-drop.ts` | `selectCreatedObject` | 新节点写入 `schema.editor.selection.objectId` | 是 | 无 | 保持。 |
| dirty state 设置 | `editor/index.tsx` | `commitSchema` | 与 savedSchemaRef 比较后 `dirty=true`，触发 `onSchemaChange` | 是 | 无 | 保持，并由 Stage 06 上抛 tab dirty。 |
| save bridge 保存 | `mendix-studio-core/src/microflow/editor/editor-save-bridge.ts` | `createMicroflowEditorApiClient` | `saveMicroflow` 调用 resource adapter | 是 | 无 | 复用 Stage 06。 |
| reload 后 schema 转 FlowGram | `StudioEmbeddedMicroflowEditor.tsx` / `MicroflowEditor` | schema load + key remount | 当前 `microflowId` schema 加载后重新渲染 | 是 | 无 | 复用 Stage 06。 |

数据流：

`NodePanel` -> drag payload -> `FlowGramMicroflowCanvas.handleDrop` -> canvas/world position -> `addMicroflowObjectFromDragPayload` -> Stage 07 registry/default config factory -> `MicroflowObject` -> `objectCollection.objects` -> editor schema state -> dirty=true -> save bridge -> `PUT /api/microflows/{id}/schema` -> reload restores node。

## 4. Node Creation Contract

| 字段 | 来源 | 规则 | 备注 |
|---|---|---|---|
| `id` | `createUniqueMicroflowObjectId(schema, prefix)` | 基于当前 schema 已有 object/parameter/flow/action id 做碰撞检测 | 不直接使用 label，不使用固定 id。 |
| `stableId` | 与 `id` 一致 | 创建后随 schema 保存并刷新恢复 | 对 object/action activity 保持稳定。 |
| `kind/type` | registry `objectKind` / action registry `kind` | 使用源码真实类型，如 `actionActivity`、`startEvent`、`exclusiveSplit`、`parameterObject` | 不新增类型。 |
| `caption/name` | registry title/defaultCaption | Start/End/Decision/Call Microflow 等使用可读标题 | 不默认 Order/Customer/ProcessOrder。 |
| `relativeMiddlePoint` | FlowGram drop position | client -> viewport/world -> snap 后写入 schema | schema 坐标即渲染真相。 |
| `size` | registry render/default size | event/action/loop/annotation 各自默认尺寸 | 由现有 registry/authoring factory 提供。 |
| `action/config` | action registry + Stage 07 default factory | Call Microflow target 为空，Object/List/REST 待配置 | 不读取 mock metadata。 |
| ports/connection points | `toEditorGraph` / `authoringToFlowGram` | 由 object kind 和 action kind 生成渲染端 ports | schema 不额外存每个 port。 |
| editor metadata | registry/action factory | 写入 icon/color/selection 等 editor 字段 | 用于渲染和属性面板定位。 |

节点自身不写入 `microflowId`。隔离边界是当前 `MicroflowEditor` 持有的 `resource.id` 对应 schema，以及 Stage 06 save path `PUT /api/microflows/{activeMicroflowId}/schema`。

## 5. Isolation Strategy

- Studio Workbench 由 Stage 05/06 保证 `microflow:{microflowId}` tab 隔离，并通过 `StudioEmbeddedMicroflowEditor` 按 active tab 加载对应 resource/schema。
- `MicroflowEditor` 内部只修改当前 props schema remount 后持有的本地 schema state。
- 拖拽创建调用 `addMicroflowObjectFromDragPayload({ schema, ... })`，schema 参数来自当前 editor state，不访问 `sampleOrderProcessingMicroflow` 或全局 store schema。
- 保存由 Stage 06 `MendixMicroflowEditorEntry` 注入真实 api client，保存当前 resource id，不使用 localStorage fallback。
- 测试中分别对 `MF_A` 和 `MF_B` 两个 schema 添加节点，验证 objectCollection 独立、id 不同、kind 不串。

## 6. Demo Default Check

本轮自动测试和源码复查覆盖保存 body 中新节点默认配置不应出现：

- `Sales`
- `MF_ValidateOrder`
- `ValidateOrder`
- `ProcessOrder`
- `CheckInventory`
- `NotifyUser`
- `OrderLine`
- `Customer`
- `Product`
- `Inventory`
- `/api/orders`
- `api.example.com`

这些关键词仍可能存在于 sample、mock metadata、local adapter 或历史 schema 中；本轮不迁移历史数据，只保证新拖入节点不再由 registry/default factory 注入这些默认值。

## 7. Verification

自动验证：

- `pnpm --filter @atlas/microflow run typecheck` 通过。
- `pnpm exec vitest run packages/mendix/mendix-microflow/src/microflow-interactions.spec.ts` 通过，24 个测试全部通过。
- 新增测试覆盖：
  - 拖拽创建节点会写入传入的 authoring schema。
  - 连续创建 Call Microflow object id 唯一。
  - drop position 写入 `relativeMiddlePoint`。
  - 新节点默认配置不含 demo 关键词。
  - A/B schema 分别添加节点不互相污染。

手工验收建议：

1. 打开 `/space/:workspaceId/mendix-studio/:appId`。
2. 展开 `Procurement` -> `Microflows`，打开真实微流 `MF_ValidatePurchaseRequest`。
3. 拖入 Start 到画布左侧，拖入 End 到画布右侧，确认立即显示并可选中。
4. 保存，确认 Network 调用 `PUT /api/microflows/{id}/schema`，body 的 `objectCollection.objects` 包含 Start / End。
5. 刷新后重新打开该微流，确认节点和位置恢复。
6. 打开另一个真实微流 `MF_CalculateApprovalLevel`，确认没有看到前一个微流的节点。
7. 在第二个微流拖入 Decision 并保存刷新，确认 A/B 各自恢复自己的节点。
8. 拖入 Call Microflow / Create Object / REST Call，确认 target/entity/url 为空，保存 body 不含 demo 关键词。
9. zoom/pan 后拖入节点，确认位置合理。
10. 快速连续拖入多个节点，确认 id 不重复。
