# Microflow Release Stage 04 - Workbench Tabs Document Lifecycle

## 1. Scope

本轮完成 microflowId 级别的 Workbench document tab 生命周期：点击真实微流节点打开 `microflow:{microflowId}` tab，同一微流重复点击只激活已有 tab，不同微流打开不同 tab；`activeWorkbenchTabId` 与 `activeMicroflowId` 随打开、切换、关闭同步；建立 per-tab dirty state、close guard、refresh/page unload guard、rename/delete/duplicate tab linkage、microflow resource placeholder 和 sample schema isolation。

本轮不做 schema load/save、真实 MicroflowEditor resource host、canvas editing、Call Microflow metadata、publish/run/trace，也不新增 mock API 或孤立 demo 页面。

## 2. Stage 0 Hotfix Status

第 0 轮 Create Microflow Hotfix 不作为本轮前置约束。本轮审查结果显示 Hotfix 基本完成，未记录为本轮 Release Blocker；目标页仍存在 sample moduleId 来源问题，但归属第 2 轮真实资产树/模块上下文，不扩大到本轮修复。

| Hotfix 检查项 | 当前状态 | 是否阻塞本轮 | 后续处理 |
|---|---|---|---|
| uncaught promise | `CreateMicroflowModal` 已 catch `onSubmit` rejection | 否 | 保持现有失败态 |
| 错误是否全部显示服务不可用 | 已按 status/code/message/traceId/fieldErrors 区分 | 否 | 后续统一 action 文案 |
| 是否仍默认 moduleId=sales | 目标页不默认 sales；模块仍来自 sample procurement module | 否 | 作为第 2 轮/资产树 blocker 记录 |

## 3. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-studio-core/src/store.ts` | 修改 | 扩展 Workbench tab model、dirty map、close guard state、per-tab history flags 和 CRUD/tab 联动 actions |
| `src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer.tsx` | 修改 | 真实 microflow 节点点击打开 `microflow:{id}` tab |
| `src/frontend/packages/mendix/mendix-studio-core/src/components/workbench-tabs.tsx` | 修改 | 多 tab dirty indicator、close guard modal、Discard/Cancel/disabled Save |
| `src/frontend/packages/mendix/mendix-studio-core/src/components/workbench-toolbar.tsx` | 修改 | Undo/Redo 按 active tab history flag 禁用，微流 tab 显示资源信息状态 |
| `src/frontend/packages/mendix/mendix-studio-core/src/index.tsx` | 修改 | active microflow tab 渲染 placeholder，接入 beforeunload dirty guard |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/studio/MicroflowWorkbenchPlaceholder.tsx` | 修改 | 展示真实 resource 信息与 Stage 05 提示，不加载 schema |
| `src/frontend/packages/mendix/mendix-studio-core/src/studio.css` | 修改 | tab dirty/status 和 placeholder 样式 |
| `src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer.spec.tsx` | 修改 | 更新点击真实微流后打开 workbench tab 的预期 |
| `src/frontend/packages/mendix/mendix-studio-core/src/workbench-tabs-lifecycle.spec.ts` | 新增 | 覆盖 open/switch/close/dirty guard/rename/delete/duplicate 生命周期 |
| `docs/microflow-release-stage-04-workbench-tabs.md` | 新增 | 本轮实现说明与验收 |
| `docs/microflow-p1-release-gap.md` | 修改 | 更新 Stage 04 Workbench 状态 |

## 4. Workbench Tab Model

Microflow tab 规则：`id = microflow:{microflowId}`，`kind = "microflow"`，`resourceId = microflowId`，`microflowId = resource.id`，`moduleId = resource.moduleId`，`title = displayName || name`，`subtitle/qualifiedName = resource.qualifiedName`，同步 `status`、`publishStatus`，`closable = true`，并记录 `openedAt`、`updatedAt`、`historyKey = tab.id`。tab 不保存 schema。

`dirtyByWorkbenchTabId: Record<string, boolean>` 独立保存 dirty state；tab 上的 `dirty` 仅用于 UI 冗余标识。`canUndoByWorkbenchTabId` 与 `canRedoByWorkbenchTabId` 是 per document history 框架，本轮默认禁用。

## 5. Store Actions

| Action | 输入 | 行为 | 注意事项 |
|---|---|---|---|
| `openMicroflowWorkbenchTab` | `microflowId` | 从 `microflowResourcesById` 读真实资源，存在则创建或激活 `microflow:{id}` | 不创建 fake tab，不加载 schema，不修改 `microflowSchema` |
| `setActiveWorkbenchTab` | `tabId` | 激活 tab，microflow tab 同步 `activeMicroflowId`/`activeModuleId` | 非 microflow tab 清空 `activeMicroflowId` |
| `closeWorkbenchTab` | `tabId`, `{ force? }` | dirty 且非 force 时打开 close guard；force 或 clean 时关闭并切换邻居 | 不调用后端 API，不删除 resource |
| `markWorkbenchTabDirty` | `tabId`, `dirty` | 更新 `dirtyByWorkbenchTabId` 和 tab dirty 标识 | per tab，不使用全局 boolean |
| `renameMicroflowWorkbenchTab` | `microflowId`, `title`, `subtitle?` | 同步已打开 tab 标题/副标题 | microflowId 不变 |
| `removeMicroflowWorkbenchTab` | `microflowId` | Delete 后强制关闭对应 tab 并清理 active/dirty | 不显示 deleted resource placeholder |
| `updateMicroflowWorkbenchTabFromResource` | `StudioMicroflowDefinitionView` | 同步 title/subtitle/status/publishStatus/module/resource 字段 | 用于 rename/refresh/publish status 后续联动 |

## 6. App Explorer Click Flow

App Explorer real microflow node click -> `selectedExplorerNodeId = microflow:{id}` -> 校验 `microflowResourcesById[id]` -> `selectedKind = microflow` -> `openMicroflowWorkbenchTab(id)` -> `activeWorkbenchTabId = microflow:{id}` -> `activeMicroflowId = id` -> `activeModuleId = resource.moduleId` -> 主工作区渲染 `MicroflowWorkbenchPlaceholder`。

## 7. Active Workbench Render Flow

`activeWorkbenchTab` 为 microflow 时，从 `microflowResourcesById[tab.microflowId]` 读取真实 resource view；存在则渲染 `MicroflowWorkbenchPlaceholder`，不存在则显示 missing resource placeholder。本轮不调用 schema load，不渲染 `StudioEmbeddedMicroflowEditor` 或 `MicroflowEditor`，不使用 `sampleOrderProcessingMicroflow`。

## 8. Dirty / Guard Strategy

dirty state 存在 `dirtyByWorkbenchTabId`，Workbench tab 显示 `*` 和 dot。关闭 dirty tab 会打开 close guard，Save 按钮 disabled 并说明 Stage 05 后接入，Discard 调用 `closeWorkbenchTab(tabId, { force: true })`，Cancel 只关闭 guard。页面存在任意 dirty tab 时注册 `beforeunload` guard。switch guard 本轮默认允许切换，dirty state 不丢失。

## 9. CRUD Linkage

Rename 通过 `upsertStudioMicroflow` / `updateMicroflowWorkbenchTabFromResource` 同步 tab title。Duplicate 只 upsert 新资源，不自动打开，不覆盖源 tab。Delete 调 `removeStudioMicroflow` 后强制关闭对应 tab，active microflow 不残留已删 id。Refresh list 会同步已打开 tab 的资源信息；刷新列表缺失但 tab 已打开的资源不会被当作 404 立即清理。

第 3 轮 App Explorer 菜单 CRUD 仍未完全接入，本轮只补齐 tabs 必需的最小 store 联动。

## 10. Undo / Redo Framework

本轮预留 `historyKey = tab.id`、`canUndoByWorkbenchTabId`、`canRedoByWorkbenchTabId`。顶部 Undo/Redo 根据 active tab 查询状态，当前默认 disabled，tooltip 说明 editor integration 后启用。Stage 5/6 接入真实 MicroflowEditor history 后，A 微流 history 不会影响 B 微流。

## 11. Sample Isolation

真实 microflow tab 只显示 `MicroflowWorkbenchPlaceholder`，不渲染 `sampleOrderProcessingMicroflow`，不消费 `store.microflowSchema`。`store.microflowSchema` 和 sample page/workflow 仍作为 legacy/sample 路径存在，但不用于真实 microflow tab。

## 12. Verification

自动测试：

- `src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer.spec.tsx` 覆盖真实微流点击打开 `microflow:{id}` tab 且不重复。
- `src/frontend/packages/mendix/mendix-studio-core/src/workbench-tabs-lifecycle.spec.ts` 覆盖 open/switch/close/dirty guard/rename/delete/duplicate。

手工验收清单：打开 `/space/:workspaceId/mendix-studio/:appId`，展开 Procurement/Microflows，点击 `MF_ValidatePurchaseRequest` 和 `MF_CalculateApprovalLevel`，确认分别打开 `microflow:{id}` tab、可切换、重复点击不重复、placeholder 显示真实 id/module/status/version/referenceCount；关闭 tab 后 active 切邻居，dirty tab close guard 的 Cancel/Discard 生效；rename/delete/duplicate 通过已接入口或 store 测试验证；Network 不应出现 `GET /api/microflows/{id}/schema` 或 `PUT /api/microflows/{id}/schema`。
