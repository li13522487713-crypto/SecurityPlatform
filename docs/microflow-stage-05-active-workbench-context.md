# Microflow Stage 05 - Active Microflow Workbench Context

## 1. Scope

本轮完成：

- 点击 App Explorer 中的真实微流后，按 `microflowId` 打开或切换 Workbench tab。
- `activeMicroflowId` / `activeModuleId` 与当前微流 tab 同步。
- 微流 tab 标题与主工作区资源信息展示当前真实微流。
- 禁止 `sampleOrderProcessingMicroflow` 污染真实微流 Workbench 上下文。
- Rename / Duplicate / Delete 后补齐与已打开 tab 的联动。

本轮不做：

- schema load。
- schema save。
- canvas rendering。
- node drag save。
- property save。
- Call Microflow metadata。
- runtime / trace。

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-studio-core/src/store.ts` | 修改 | 新增 `workbenchTabs`、`activeWorkbenchTabId` 与微流 tab actions。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer.tsx` | 修改 | 真实微流节点点击改为打开 `microflow:{id}` Workbench tab，CRUD 后同步 tab。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/components/workbench-tabs.tsx` | 修改 | 从 store 渲染动态 Workbench tabs，支持关闭真实微流 tab。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/components/workbench-toolbar.tsx` | 修改 | 微流上下文下不再提供 sample editor 沉浸入口，提示 Stage 06 接入画布。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/index.tsx` | 修改 | 主工作区按 active Workbench tab 渲染真实微流资源占位信息。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/studio.css` | 修改 | 增加动态 tab 与微流占位页样式。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/studio/MicroflowWorkbenchPlaceholder.tsx` | 新增 | 展示真实微流 metadata 与 Stage 06 接入提示。 |
| `docs/microflow-stage-01-gap-matrix.md` | 修改 | 更新 P0 中真实微流打开上下文与 Stage 06 schema 边界。 |

## 3. Workbench Tab Model

Workbench tab 状态保存在 store：

- `workbenchTabs: StudioWorkbenchTab[]`
- `activeWorkbenchTabId?: string`

微流 tab 约定：

- `id = microflow:{microflowId}`
- `kind = "microflow"`
- `resourceId = microflowId`
- `microflowId = resource.id`
- `moduleId = resource.moduleId`
- `title = resource.displayName || resource.name`
- `closable = true`

同步规则：

- 激活微流 tab 时，`activeWorkbenchTabId`、旧兼容字段 `activeTabId` 均设为 `microflow:{id}`。
- 激活微流 tab 时，`activeMicroflowId = microflowId`，`activeModuleId = resource.moduleId`。
- 激活非微流 tab 时，`activeMicroflowId` 清空，避免非微流编辑区误读微流上下文。

## 4. Click Data Flow

App Explorer real microflow node click
-> `openMicroflowWorkbenchTab(microflowId)`
-> `store.activeMicroflowId = microflowId`
-> `store.activeModuleId = resource.moduleId`
-> `store.activeWorkbenchTabId = microflow:{microflowId}`
-> `MicroflowWorkbenchPlaceholder`
-> display resource metadata

点击时会先确认真实节点携带 `microflowId`，并确认 `microflowResourcesById[microflowId]` 已存在；资源缺失时不会创建空 tab，也不会切到固定 sample tab。

## 5. CRUD Linkage

- Rename：成功后 `upsertStudioMicroflow` 更新 `microflowResourcesById`，`renameMicroflowWorkbenchTab` 同步已打开 tab 的标题；`microflowId`、`activeMicroflowId`、`activeWorkbenchTabId` 不变。
- Duplicate：成功后新资源写入 store 与 explorer 列表；当前产品行为不自动打开新 tab，因此不会覆盖源微流 tab。
- Delete：成功后 `removeStudioMicroflow` 移除资源索引，并调用 `removeMicroflowWorkbenchTab` 关闭对应 tab；若删除的是 active 微流，`activeMicroflowId` 清空或切换到其他仍有效的 active 微流 tab。

## 6. Sample Isolation

`sampleOrderProcessingMicroflow` 仍保留在 `store.ts` 的 legacy `microflowSchema` 初始化中，用于尚未接真实资源的旧 sample/demo 场景。

真实微流节点打开后不会再显示 sample schema，因为：

- App Explorer 不再把真实微流打开到固定 `microflow` tab。
- 真实微流 tab 使用 `microflow:{id}` 独立标识。
- 主工作区识别 `activeWorkbenchTab.kind === "microflow"` 后只渲染 `MicroflowWorkbenchPlaceholder`。
- `MicroflowWorkbenchPlaceholder` 只消费 `StudioMicroflowDefinitionView` metadata，不消费 `microflowSchema`。

Stage 06 可以在 `activeWorkbenchTabId` / `activeMicroflowId` 稳定后接入：

- `GET /api/microflows/{id}/schema` 加载真实 schema。
- 按 `microflowId` 隔离编辑器实例状态。
- `PUT /api/microflows/{id}/schema` 保存真实 schema。

## 7. Verification

手工验收步骤：

1. 打开 `/space/:workspaceId/mendix-studio/:appId`。
2. 展开 `Procurement`。
3. 展开 `Microflows`。
4. 确认显示真实后端微流列表。
5. 点击 `MF_ValidatePurchaseRequest`。
6. 确认 `activeMicroflowId` 是该资源 id。
7. 确认打开 tab `microflow:{id}`。
8. 确认主工作区显示该微流名称、`qualifiedName`、`id`、`moduleId`、`status`。
9. 确认没有显示 `sampleOrderProcessingMicroflow`。
10. 点击 `MF_CalculateApprovalLevel`。
11. 确认打开另一个 tab `microflow:{另一个id}`。
12. 确认两个 tab 可切换，资源信息不互相污染。
13. 重命名 `MF_CalculateApprovalLevel`。
14. 确认 tab 标题同步更新，id 不变。
15. 复制 `MF_CalculateApprovalLevel`。
16. 确认不会覆盖源 tab。
17. 删除一个已打开且无引用的微流。
18. 确认对应 tab 关闭，`activeMicroflowId` 不再指向已删除 id。
19. 确认没有调用 `/api/microflows/{id}/schema`。
20. 确认没有画布保存请求。
