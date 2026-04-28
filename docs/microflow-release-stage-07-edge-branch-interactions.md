# Microflow Release Stage 07 - Edge & Branch Interactions

## 1. Scope

本轮完成真实 Microflow 编辑器画布中的 edge creation、edge deletion、source/target/handle persistence、origin/destination connection index persistence、Decision true/false branch persistence、Edge Property Form、branch label/case editing、node delete related edge cleanup、dirty state integration、save reload recovery 与 A/B/C microflow isolation 的源码闭环。

本轮不做 full Problems panel、Call Microflow metadata、Domain Model metadata、publish/run/trace、execution engine、mock API、孤立 demo 页面和历史 schema migration。

依赖缺口与本轮最小补齐点：

| 依赖缺口 | 影响 | 本轮最小补齐点 |
|---|---|---|
| FlowGram 原生节点删除未同步到 schema deleteObject | 删除节点可能只删 UI 节点，无法保证相关 flows 清理 | 新增 `findDeletedObjectId`，在 bridge 中优先触发 `{ deleteObjectId }` |
| FlowGram edge 与 schema flow 映射散落在多个文件 | 难以专项测试 handle/connectionIndex/caseValues | 新增 `flowgram-edge-mapping.ts` 纯 helper |
| `conditionKey` 不是持久化字段 | 文档验收不能按不存在字段判断 | 记录为由 `caseValueKey/caseValueIdentity` 从 `caseValues` 派生 |

## 2. Stage 0 Hotfix Status

第 0 轮 Hotfix 不阻塞本轮。本轮只记录状态，不回头重做第 0 轮。

| Hotfix 检查项 | 当前状态 | 是否阻塞本轮 | 后续处理 |
|---|---|---|---|
| Create Microflow 弹窗失败 catch | 已有源码覆盖 | 否 | 保持现状 |
| 失败不关闭弹窗且 loading 恢复 | 已有源码覆盖 | 否 | 保持现状 |
| 错误 envelope/status/code/traceId 展示 | 已有源码覆盖 | 否 | 继续在 Hotfix 专项跟踪 |
| moduleId 仍来自 sample Procurement module | 仍是发布化缺口 | 否 | 资产树真实化后续轮次处理 |

## 3. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-microflow/src/flowgram/adapters/flowgram-edge-mapping.ts` | 新增 | FlowGram edge 与 Microflow flow 双向映射、handle/connectionIndex helper |
| `src/frontend/packages/mendix/mendix-microflow/src/flowgram/adapters/index.ts` | 修改 | 导出 edge mapping helper |
| `src/frontend/packages/mendix/mendix-microflow/src/flowgram/adapters/flowgram-to-authoring-patch.ts` | 修改 | 复用 mapping helper，新增 `findDeletedObjectId` |
| `src/frontend/packages/mendix/mendix-microflow/src/flowgram/hooks/useFlowGramMicroflowBridge.ts` | 修改 | FlowGram 节点删除同步到 AuthoringSchema |
| `src/frontend/packages/mendix/mendix-microflow/src/adapters/authoring-operations.ts` | 修改 | duplicate flow 判定纳入 caseValues，selection 同步 legacy selected ids |
| `src/frontend/packages/mendix/mendix-microflow/src/editor/index.tsx` | 修改 | `flowgramNodeDelete` 映射为 `deleteNode` history/dirty reason |
| `src/frontend/packages/mendix/mendix-microflow/src/microflow-interactions.spec.ts` | 修改 | 补 handle/case mapping、节点删除清线、schema flow 映射测试 |
| `docs/microflow-p1-release-gap.md` | 修改 | 更新 Stage 07 edge/branch 状态 |

## 4. Edge Schema Contract

| 语义 | 源码字段 | 类型 | 说明 |
|---|---|---|---|
| flow id | `MicroflowFlow.id` | `string` | 持久化 flow 主键，`createStableId("flow")` 生成 |
| stable id | `MicroflowFlow.stableId` | `string` | 稳定标识 |
| flow collection | `MicroflowAuthoringSchema.flows` | `MicroflowFlow[]` | 顶层 flow 集合 |
| nested flow collection | `MicroflowObjectCollection.flows` | `MicroflowFlow[] \| undefined` | Loop objectCollection 内嵌 flow |
| flow type | `MicroflowSequenceFlow.kind` | `"sequence"` | 真实 sequence flow 类型 |
| source/origin object id | `originObjectId` | `string` | 源节点 id |
| target/destination object id | `destinationObjectId` | `string` | 目标节点 id |
| source handle | FlowGram `sourcePortID` | `string \| number \| undefined` | UI 投影字段，不直接持久化 |
| target handle | FlowGram `targetPortID` | `string \| number \| undefined` | UI 投影字段，不直接持久化 |
| source connection index | `originConnectionIndex` | `number` | 源端口索引，持久化 |
| target connection index | `destinationConnectionIndex` | `number` | 目标端口索引，持久化 |
| label | `flow.editor.label` | `string \| undefined` | 普通连线/分支 label |
| branch case | `caseValues` | `MicroflowCaseValue[]` | Decision/Inheritance 分支条件 |
| condition key | `caseValueKey/caseValueIdentity(caseValue)` | derived `string` | 源码无 `conditionKey` 字段，由 case 派生 |
| editor line metadata | `line` | `MicroflowLine` | routing、bendPoints、style |

字段盘点：

| 字段语义 | 源码字段 | 类型 | 用途 | 是否保存 | 本轮处理 |
|---|---|---|---|---|---|
| 连线集合 | `flows` / `objectCollection.flows` | `MicroflowFlow[]` | 保存 root/nested flows | 是 | 通过 collection-aware helper 读写 |
| 连线对象 | `MicroflowSequenceFlow` | interface | sequence flow 持久化对象 | 是 | 新增映射测试 |
| source | `originObjectId` | `string` | 源节点 | 是 | 创建/映射校验 |
| target | `destinationObjectId` | `string` | 目标节点 | 是 | 创建/映射校验 |
| source handle | `sourcePortID` | FlowGram runtime | UI 端口 | 否，投影 | 映射到 `originConnectionIndex` |
| target handle | `targetPortID` | FlowGram runtime | UI 端口 | 否，投影 | 映射到 `destinationConnectionIndex` |
| source index | `originConnectionIndex` | `number` | 源端口持久化 | 是 | helper 读写 |
| target index | `destinationConnectionIndex` | `number` | 目标端口持久化 | 是 | helper 读写 |
| Decision case | `caseValues` | `MicroflowCaseValue[]` | true/false/enum/object/fallback | 是 | 属性表单可编辑 |
| branch label | `editor.label` | `string` | 边显示文案 | 是 | 属性表单写回 |

## 5. FlowGram Mapping Strategy

`mapFlowGramEdgeToMicroflowFlow(schema, edge)` 从 `sourcePortID/targetPortID` 解析 editor ports，生成 `MicroflowSequenceFlow` 或 `MicroflowAnnotationFlow`，并保存 `originObjectId`、`destinationObjectId`、`originConnectionIndex`、`destinationConnectionIndex`、`caseValues` 和 `editor.label`。

`mapMicroflowFlowToFlowGramEdge(schema, flow)` 从 schema flow 回投 FlowGram edge。handle 映射使用 `getSourceHandleFromConnectionIndex`、`getTargetHandleFromConnectionIndex`、`getConnectionIndexFromSourceHandle`、`getConnectionIndexFromTargetHandle`。这些 helper 不依赖 React/API/mock metadata，可单测。

Dirty 由 `FlowGramMicroflowCanvas.onSchemaChange` -> `commitSchema(..., { source: "flowgram" })` 触发；selection change 单独 `skipDirty`。

## 6. Edge Creation Flow

FlowGram connect -> `canConnectPorts` 校验 source/target/port/self-loop/cardinality/duplicate case -> `mapFlowGramEdgeToMicroflowFlow` -> `applyEditorGraphPatchToAuthoring({ addFlow })` -> `addFlow` 写入当前 active schema 的 root/nested flow collection -> `dirty=true` -> Save 调 `PUT /api/microflows/{id}/schema` -> reload 通过 `authoringToFlowGram` 恢复 edge。

## 7. Edge Deletion Flow

select edge -> Delete/Backspace、FlowGram 删除或 Edge Property Form 删除 -> `deleteFlow` / `{ deleteFlowId }` -> 从 root/nested flow collection 删除 -> 清理 selection -> `dirty=true` -> Save -> reload 后不再出现。Decision case 没有独立状态表，删除 flow 即删除其 `caseValues`，重新连线可重新分配 true/false。

## 8. Decision Branch Strategy

Boolean Decision 使用真实 `caseValues`：

| 分支 | 持久化 |
|---|---|
| true | `{ kind: "boolean", persistedValue: "true", value: true }` |
| false | `{ kind: "boolean", persistedValue: "false", value: false }` |
| duplicate | `canConnectPorts` 阻止或 validation issue 展示 |
| empty/custom | enumeration/object type 场景使用 `empty/fallback/inheritance/enumeration/noCase` |

`conditionKey` 不落库；UI/validation 用 `caseValueKey` 或 `caseValueIdentity` 派生。branch label 保存到 `editor.label`，属性面板修改后写回 schema。

## 9. Edge Property Form

`MicroflowPropertyPanel` 在 `selectedFlow` 存在时渲染 `FlowEdgeForm`。普通连线展示 flow id、kind、officialType、origin/destination object、runtime effect、connection indexes、routing、label、description 和 Delete 操作。Decision 分支额外展示 Branch Case selector、duplicate warning、empty/noCase warning、branch order 和 label。

当前缺口：还没有独立 Edge Form Registry，所有 edge 类型仍在 `FlowEdgeForm` 内部分支。

## 10. Node Delete Edge Cleanup

节点删除入口包括 editor Delete/Backspace、属性面板 Delete Object、以及本轮补齐的 FlowGram 原生节点删除同步。`deleteObject` 收集对象及 Loop 子孙 id，删除所有 `originObjectId` 或 `destinationObjectId` 命中的 root/nested flows，并清理 selection，防止 dangling flow。

## 11. Dirty Strategy

创建连线、删除连线、修改 label、修改 case、删除节点导致清理连线都会经 `commitSchema` 默认标脏。保存成功由 `MicroflowResourceEditorHost.onSave` 调 `onDirtyChange(false)`；保存失败由 editor catch 后保持 dirty。Workbench dirty key 为 `microflow:{microflowId}`，A/B/C 独立。

## 12. Isolation Verification

源码隔离点：

| 隔离点 | 结果 |
|---|---|
| editor key | `${microflowId}:${schemaId}:${version}` |
| save id | `adapter.saveMicroflowSchema(resource.id, request.schema)` |
| schema state | `MendixMicroflowEditorEntry` 随 resource/schema reset |
| helper mutation | `addFlow/deleteFlow/deleteObject` 返回新 schema，不写共享 sample |
| local/mock | 真实目标 tab 要求 http mode；不会 fallback localStorage |

单测覆盖 A/B schema flow 操作互不影响。未运行浏览器三微流手工联调，需按验收步骤在真实前后端环境验证。

## 13. Verification

自动验证：

- `pnpm --filter @atlas/microflow run typecheck`：通过。
- `pnpm exec vitest run "packages/mendix/mendix-microflow/src/microflow-interactions.spec.ts" -t "maps FlowGram edges|maps schema flows|detects FlowGram node deletion|persists decision true and false"`：4 个相关用例通过。
- 全量 `microflow-interactions.spec.ts` 当前有 1 个既有失败：`blocks invalid dragged nodes before mutating AuthoringSchema` 的 `blockedReason` 断言拿到 `undefined`，与本轮 edge/branch 改动无关。

手工验收：本轮未启动前后端和浏览器，未实际抓取 PUT body。待人工按 40 步验收清单验证 Start -> Decision -> End、true/false label 保存刷新、删除 edge、删除 Decision、A/B/C 隔离、无 localStorage/local adapter、无 sampleOrderProcessingMicroflow。
