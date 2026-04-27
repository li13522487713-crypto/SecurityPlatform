# Microflow Stage 10 - Edge / Sequence Flow Persistence

## 1. Scope

本轮完成：

- 连线创建持久化：FlowGram connect 映射为 `MicroflowSequenceFlow` / `MicroflowAnnotationFlow` 并写入 authoring schema。
- 连线删除持久化：删除 FlowGram edge 后删除 schema 中对应 flow，并清理 selection。
- source / target / handle 持久化：保存 `originObjectId`、`destinationObjectId`、`originConnectionIndex`、`destinationConnectionIndex`。
- Decision true / false 基础分支持久化：boolean decision 出边写入 `caseValues`。
- 删除节点时相关连线清理：沿用并验证 Stage 09 的 root/nested flow 清理。
- 连线 label / caseValues 基础编辑持久化：`FlowEdgeForm` 已通过 `updateFlow` 写回 schema。
- dirty 状态同步：create/delete/update flow 均走 `commitSchema`。
- 保存刷新恢复：复用 Stage 06 真实 schema save/load。
- A/B schema 连线隔离测试覆盖。

本轮不做：

- 不新增节点类型。
- 不实现执行引擎。
- 不做 trace/debug。
- 不接入 `Call Microflow` metadata。
- 不接入 Domain Model metadata。
- 不做 schema migration。

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-microflow/src/adapters/authoring-operations.ts` | 修改 | flow id 改用 `createStableId`；`addFlow` 防重复；`deleteFlow` 清理 selection / selectedFlowId。 |
| `src/frontend/packages/mendix/mendix-microflow/src/flowgram/adapters/flowgram-edge-factory.ts` | 修改 | 修复 boolean decision `False` 端口大小写导致 false case 被保存成 true 的问题。 |
| `src/frontend/packages/mendix/mendix-microflow/src/microflow-interactions.spec.ts` | 修改 | 增加 flow 创建、删除、handle、Decision case、A/B 隔离测试。 |
| `docs/microflow-stage-01-gap-matrix.md` | 修改 | 更新 Stage 10 连线创建/删除/handle/branch 状态。 |

## 3. Sequence Flow Schema Contract

| 语义 | 源码字段 | 示例 | 说明 |
|---|---|---|---|
| flow id | `MicroflowFlow.id` / `stableId` | `flow-m...` | 由 `createStableId("flow")` 或 `createStableId("annotation-flow")` 生成。 |
| 连线集合 | `schema.flows` + loop 内 `objectCollection.flows` | `schema.flows[0]` | root flow 存在 `schema.flows`；loop 内 flow 存在 loop collection。 |
| source object | `originObjectId` | `start-1` | 连线起点 object id。 |
| target object | `destinationObjectId` | `end-1` | 连线终点 object id。 |
| source handle | `originConnectionIndex` | `1` | schema 保存端口索引；FlowGram handle 由 `toEditorGraph` 转为 `sourcePortId`。 |
| target handle | `destinationConnectionIndex` | `0` | schema 保存目标端口索引。 |
| flow kind | `kind` | `sequence` / `annotation` | 区分执行流与注释流。 |
| edge kind | `editor.edgeKind` | `sequence` / `decisionCondition` / `errorHandler` | 编辑器语义，保存到 schema。 |
| label | `editor.label` | `Ready` | FlowEdgeForm 可编辑并持久化。 |
| branch case | `caseValues` | `{ kind: "boolean", value: true }` | Decision / ObjectType 分支持久化字段。 |
| line metadata | `line` / `editor.description` | routing/style/description | 连线路由、样式、描述。 |

## 4. Create Edge Data Flow

`FlowGram connect` -> `useFlowGramMicroflowBridge.doc.onContentChange` -> `findNewFlowGramEdge` -> `portById` -> `canConnectPorts` -> `createMicroflowFlowFromPorts` -> `createSequenceFlow` / `createAnnotationFlow` -> `applyEditorGraphPatchToAuthoring({ addFlow })` -> `addFlow` writes root or loop collection -> `commitSchema` -> dirty=true -> save -> `PUT /api/microflows/{id}/schema` -> reload restores edge.

`createMicroflowFlowFromPorts` 会保存：

- `originObjectId`
- `destinationObjectId`
- `originConnectionIndex`
- `destinationConnectionIndex`
- `editor.edgeKind`
- `caseValues`
- `isErrorHandler`

## 5. Delete Edge Data Flow

`select edge` -> Delete/Backspace or FlowGram edge removal or property panel delete -> `findDeletedFlowId` / `onDeleteFlow` -> `applyEditorGraphPatchToAuthoring({ deleteFlowId })` -> `deleteFlow` -> remove root/nested flow -> clear `selection.flowId` and `selectedFlowId` -> `commitSchema` -> dirty=true -> save -> reload confirms removal.

## 6. Decision Branch Strategy

- Boolean decision 使用 `decisionOut` port label 区分 true/false。
- `True` / `true` 端口保存为 `caseValues: [{ kind: "boolean", value: true, persistedValue: "true" }]`。
- `False` / `false` 端口保存为 `caseValues: [{ kind: "boolean", value: false, persistedValue: "false" }]`。
- `canConnectPorts` 会阻止同一个 decision 重复创建 true 或 false case。
- 删除 true/false 出边后，flow 从 schema 删除，case 自然释放，新出边可重新使用该 case。
- Enumeration/ObjectType 仍保留现有 `FlowEdgeForm` / case editor 能力，本轮不实现复杂表达式引擎。

## 7. Handle Mapping Strategy

- schema -> editor graph：`toEditorGraph` 根据 `originConnectionIndex` / `destinationConnectionIndex` 找到 port，再生成 `sourcePortId` / `targetPortId`。
- editor graph -> FlowGram：`authoringToFlowGram` 将 `sourcePortId` / `targetPortId` 写入 FlowGram edge。
- FlowGram -> schema：`createFlowFromFlowGramEdge` 通过 FlowGram edge 的 `sourcePortID` / `targetPortID` 找回 `MicroflowEditorPort`，再写入 connection index。
- 刷新后 handle 不丢失，因为真实保存的是 connection index，FlowGram handle 每次由 schema 重新派生。

## 8. Dirty Strategy

- 创建连线：`useFlowGramMicroflowBridge` 调用 `onSchemaChange`，`MicroflowEditor.commitSchema` 标记 dirty=true。
- 删除连线：`deleteFlow` 后通过 `commitSchema` 标记 dirty=true。
- 修改 label/case：`FlowEdgeForm` 调用 `onFlowChange` -> `updateFlow` -> `commitSchema`。
- 删除节点导致 flow 清理：`deleteObject` 由 Stage 09 路径进入 `commitSchema`。
- 保存成功 dirty=false；保存失败不更新 `savedSchemaRef`，dirty 不被误清。
- A/B tab dirty 隔离沿用 Stage 05/06 的 Workbench tab dirty 和 editor key remount。

## 9. Isolation Verification

自动测试新增：

- `schemaA` 创建 Start -> End flow 后，`schemaB.flows` 仍为空。
- A/B object ids 和 flow ids 分别存在各自 schema 中。

运行时隔离继续依赖：

- Stage 05 `microflow:{microflowId}` tab。
- Stage 06 `StudioEmbeddedMicroflowEditor` 按 active microflow 加载 schema。
- Stage 06 save bridge 使用当前 resource id 调用 `PUT /api/microflows/{id}/schema`。

## 10. Verification

自动验证：

- `pnpm --filter @atlas/microflow run typecheck` 通过。
- `pnpm exec vitest run packages/mendix/mendix-microflow/src/microflow-interactions.spec.ts` 通过，33 个测试全部通过。

新增/补充测试覆盖：

- create sequence flow 后 `flows` 长度 +1。
- flow id 非空且不会重复添加完全相同连线。
- source/target object id 正确。
- origin/destination connection index 正确。
- Decision true 出边保存 true case。
- Decision false 出边保存 false case。
- schema -> FlowGram edge 保留 source handle。
- delete flow 后 schema 中不存在且 selection 清理。
- delete node 后相关 flows 全部删除。
- A/B schema 创建连线互不影响。

手工验收建议：

1. 打开 `/space/:workspaceId/mendix-studio/:appId`。
2. 打开真实微流 `MF_ValidatePurchaseRequest`。
3. 拖入 Start / Decision / End / 另一个普通节点。
4. 创建 Start -> Decision。
5. 创建 Decision true -> End。
6. 创建 Decision false -> 另一个节点。
7. 保存，确认 Network 调用 `PUT /api/microflows/{id}/schema`。
8. 检查 body 中包含 `flows`，并包含 source/target id、connection index、true/false caseValues。
9. 刷新并重新打开，确认连线和 true/false 分支恢复。
10. 删除一条连线，保存刷新后确认消失。
11. 删除 Decision，确认相关连线全部清理。
12. 打开另一个微流并创建自己的连线，保存刷新后分别打开两个微流，确认连线互不污染。
