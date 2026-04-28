# Microflow UI Interaction Gap

## 1. Mendix Studio Pro Interaction Baseline

本轮没有引用外部实现，只按 Mendix Studio Pro 类 IDE 的发布交互基线定义验收口径：App Explorer 必须展示真实应用/模块/资产树；Microflows 节点支持真实 New/Rename/Duplicate/Delete/References/Refresh；打开资产应进入以 microflowId 隔离的 Workbench Tab；编辑器应按资源加载/保存 schema，具备 dirty guard、Problems 定位、Reference 保护、Publish gate、Run/Trace 可视化；Toolbox/Property Panel 对节点配置必须写回 authoring schema 并可保存后恢复。

## 2. Current Web Studio Interaction

当前目标页 `/space/:workspaceId/mendix-studio/:appId` 的真实交互路径为：

| 区域 | 当前状态 | 证据 |
|---|---|---|
| Route | 已读 `workspaceId/appId`，创建 `adapterConfig` | `src/frontend/apps/app-web/src/app/pages/mendix-studio-route.tsx` |
| Adapter | `MendixStudioApp` 创建 `_resolvedBundle` 并传入 Explorer/Editor | `src/frontend/packages/mendix/mendix-studio-core/src/index.tsx` |
| App Explorer | 外壳仍是 `TREE_DATA`，Microflows children 调真实 list | `src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer.tsx` |
| Workbench | 有 tab 数组和 active tab，微流 tab id 为 `microflow:{id}` | `src/frontend/packages/mendix/mendix-studio-core/src/store.ts` |
| Editor Host | 微流 tab 通过 `StudioEmbeddedMicroflowEditor` 加载真实 resource/schema | `src/frontend/packages/mendix/mendix-studio-core/src/microflow/studio/StudioEmbeddedMicroflowEditor.tsx` |
| Canvas | 节点/连线/位置/布局可进入 authoring schema | `src/frontend/packages/mendix/mendix-microflow/src/editor/index.tsx`; `flowgram/*` |
| Metadata | 目标页注入 HTTP metadata adapter；缺 adapter 不 fallback mock | `src/frontend/packages/mendix/mendix-microflow/src/metadata/metadata-provider.tsx` |

## 3. App Explorer Gap

| 交互 | 当前状态 | 证据 | 缺口 | 修复轮次 |
|---|---|---|---|---|
| 真实应用/模块树 | 未完成 | `app-explorer.tsx` `TREE_DATA`; `SAMPLE_PROCUREMENT_APP` | `appId` 未驱动 tree/module | 第 2 轮 |
| Microflows 真实列表 | 部分完成 | `loadMicroflows` 调 `resourceAdapter.listMicroflows` | moduleId 来自 sample | 第 2 轮 |
| loading/empty/error/retry | 已有 | `createMicroflowStateChildren`; retry action | 仅 Microflows 节点 | 第 2 轮 |
| search/filter | 部分完成 | `searchText` 传 `ExplorerTreeNode` | 本地过滤 TREE_DATA，无远程 filter | 第 2 轮 |
| context menu | 部分完成 | `renderContextMenu` | 只覆盖 Microflows/微流节点 | 第 2/3 轮 |
| New | 部分完成 | `CreateMicroflowModal` + `handleCreateMicroflow` | `moduleOptions` sample/locked | 第 2/3 轮 |
| Rename/Duplicate/Delete | 部分完成 | `handleRenameMicroflow`; `handleDuplicateMicroflow`; `handleDeleteMicroflow` | 权限态不足，目标 E2E 缺 | 第 3 轮 |
| References | 部分完成 | `MicroflowReferencesDrawer` | callees API 缺失 | 第 3 轮 |
| Refresh | 部分完成 | `refreshMicroflows` | 不刷新真实模块树 | 第 2 轮 |
| 409 delete protect | 部分完成 | catch `MICROFLOW_REFERENCE_BLOCKED`/409 | 后端权限/ownership 缺 | 第 3 轮 |

## 4. Workbench Tabs Gap

| 交互 | 当前状态 | 证据 | 缺口 | 修复轮次 |
|---|---|---|---|---|
| 多 tab | 已有 | `store.ts` `workbenchTabs`; `openMicroflowWorkbenchTab` | 初始 page/workflow 仍 sample | 第 4 轮 |
| tab id 隔离 | 部分完成 | `getMicroflowWorkbenchTabId` -> `microflow:${id}` | 无 workspace/app 前缀 | 第 4 轮 |
| active tab | 已有 | `activeWorkbenchTabId` | 无 switch guard | 第 4 轮 |
| dirty 状态 | 部分完成 | tab 上 `dirty` 字段；`markWorkbenchTabDirty` | 无 `dirtyByTabId` map | 第 4 轮 |
| close guard | 未完成 | `WorkbenchTabs` 直接 `closeWorkbenchTab` | 未保存可直接关闭 | 第 4 轮 |
| switch guard | 未完成 | `handleTabClick` 直接切换 | 未保存可直接切换 | 第 4 轮 |
| refresh guard | 源码中未发现 | 源码中未发现 | 刷新/重新加载保护缺失 | 第 4 轮 |
| rename title sync | 已有 | `renameMicroflowWorkbenchTab`; `upsertStudioMicroflow` | 外部 rename 需 refresh | 第 4 轮 |
| delete closes tab | 已有 | `removeStudioMicroflow` | 外部 delete 需 refresh | 第 4 轮 |

## 5. Canvas Gap

| 交互 | 当前状态 | 证据 | 缺口 | 修复轮次 |
|---|---|---|---|---|
| drag node | 已写 schema | `FlowGramMicroflowCanvas.tsx`; `editor/index.tsx` | 目标页 E2E 缺 | 第 5 轮 |
| click/double-click add | 已写 schema | `node-panel/index.tsx` | 默认位置需优化 | 第 5 轮 |
| node position | 已写 schema | `useFlowGramMicroflowBridge.ts` `flowGramPositionPatch` | viewport dirty 策略待定 | 第 5 轮 |
| node delete | 已写 schema | `editor/index.tsx` `deleteObject` | 无确认/undo UX 验收 | 第 5 轮 |
| node duplicate | 部分完成 | `editor/index.tsx` 导入 `duplicateObject` | 多选 duplicate 未完整发现 | 第 5 轮 |
| multiselect | 部分/不完整 | `WorkflowSelectService` selection 同步 | 主编辑器 selection 模型偏单选 | 第 5 轮 |
| copy/paste | 未完成 | 源码中未发现完整 clipboard copy/paste | 缺失 | 第 5 轮 |
| edge create/delete | 已写 schema | `useFlowGramMicroflowBridge.ts` | case edge 需 E2E | 第 5 轮 |
| true/false branch | 部分完成 | `registry.ts` true/false ports; `FlowGramMicroflowCaseEditor` | 发布交互未验收 | 第 5 轮 |
| undo/redo | 已有内部 history | `editor/index.tsx`; `history` | 不持久到 tab store | 第 4/5 轮 |
| auto layout | 已写 schema | `layout`; `handleAutoLayout` | 无目标页 E2E | 第 5 轮 |
| minimap/grid | 已有 | `FlowGramMicroflowCanvas.tsx`; `schema.editor.showMiniMap/gridEnabled` | 保存触发策略需定义 | 第 5 轮 |

## 6. Toolbox Gap

| 交互 | 当前状态 | 证据 | 缺口 | 修复轮次 |
|---|---|---|---|---|
| 基础节点 Start/End/Parameter/Decision/Merge/Loop/Break/Continue/Annotation | 已注册、可拖拽、可保存 | `node-registry/registry.ts` | 需要目标页交互 E2E | 第 5 轮 |
| 变量动作 | 已注册 | `node-registry/action-registry.ts`; `registry.ts` | 表达式/变量冲突 UX 需验收 | 第 5 轮 |
| Object/List 动作 | 已注册 | `action-registry.ts`; `action-activity-form.tsx` | 依赖真实 metadata | 第 5 轮 |
| Call Microflow | 已注册 | `action-registry.ts`; `MicroflowSelector.tsx` | 目标 metadata 必须真实，callees API 缺 | 第 5 轮 |
| REST Call | 已注册 | `action-registry.ts`; runtime Rest policy | real HTTP 默认受安全策略限制 | 第 5 轮 |
| Components/Templates tab | 占位 | `node-panel/index.tsx` `componentsPlaceholder/templatesPlaceholder` | 不是生产资产 | 后续轮次 |

## 7. Property Panel Gap

| 交互 | 当前状态 | 证据 | 缺口 | 修复轮次 |
|---|---|---|---|---|
| 选中节点显示表单 | 已有 | `property-panel/index.tsx`; `ObjectPanel` | 无选中时只有 empty | 第 5 轮 |
| 选中边显示表单 | 已有 | `FlowEdgeForm` | case/value UX 待验收 | 第 5 轮 |
| Start/End/Parameter | 已有 | `event-nodes-form.tsx`; `parameter-object-form.tsx` | 完整字段验收不足 | 第 5 轮 |
| Decision/Merge | 已有 | `exclusive-split-form.tsx`; `merge-node-form.tsx` | expression IDE 不完整 | 第 5 轮 |
| Object/List/Variable/Call/REST | 部分完成 | `action-activity-form.tsx`; `generic-action-fields-form.tsx` | metadata/error/loading UX 不统一 | 第 5 轮 |
| expression editor | 部分完成 | `property-panel/expression` | 不是完整 Mendix 表达式编辑器 | 后续 |
| metadata selector | 部分完成 | `selectors/*`; `metadata-provider.tsx` | stale/cache 策略不完整 | 第 5 轮 |

## 8. Problems / References / Run / Trace Gap

| 交互 | 当前状态 | 证据 | 缺口 | 修复轮次 |
|---|---|---|---|---|
| Problems panel | 已有 | `editor/index.tsx` Problems bottom tab | 与 Studio 全局 bottom panel 分离 | 第 5 轮 |
| 点击 problem 定位 | 已有 | `viewportForProblemIssue`; selection patch | E2E 缺 | 第 5 轮 |
| save gate | 部分/不完整 | `saveCurrentSchema` 直接保存，异常后显示 issues | 未统一阻止 error 保存 | 第 5 轮 |
| publish gate | 部分完成 | `PublishMicroflowModal`; backend publish | UI 定位/确认链路弱 | 第 5 轮 |
| references drawer | 部分完成 | `MicroflowReferencesDrawer`; `app-explorer.tsx` | callees 缺；delete protect 需 E2E | 第 3 轮 |
| test-run input | 部分完成 | `MicroflowTestRunModal.tsx`; `run-input-model.ts` | 目标页 E2E 缺 | 第 5 轮 |
| run history | 部分完成 | `MicroflowRunHistoryPanel.tsx`; `/runs` APIs | 权限/ownership 缺 | 第 5 轮 |
| trace panel | 部分完成 | `MicroflowTracePanel.tsx`; `/trace` API | 目标页定位 E2E 缺 | 第 5 轮 |

## 9. Production Interaction Checklist

| 交互能力 | 当前状态 | 发布要求 | 是否达标 | 修复轮次 |
|---|---|---|---|---|
| 目标 route 读取 workspaceId | 已完成 | 必须从 `/space/:workspaceId` 进入目标页上下文 | 是 | 已完成 |
| 目标 route 读取 appId | 已完成但未使用到资产树 | appId 必须驱动 app/module/resource | 否 | 第 2 轮 |
| adapter bundle 注入目标页 | 已完成 | App Explorer/Editor 共享真实 HTTP bundle | 是 | 已完成 |
| Authorization/currentUser 注入 | 未完成 | 所有写操作携带真实用户/权限 | 否 | 权限轮 |
| App Explorer 真实资产树 | 未完成 | 不得使用 sample/TREE_DATA | 否 | 第 2 轮 |
| Microflows 真实 list | 部分完成 | 按 workspace/app/module 查询真实后端 | 否 | 第 2 轮 |
| New Microflow | 部分完成 | 使用真实 moduleId，失败不关弹窗 | 部分 | 第 2/3 轮 |
| Rename/Duplicate/Delete | 部分完成 | CRUD 闭环且有引用保护/权限 | 部分 | 第 3 轮 |
| References | 部分完成 | callers/callees/delete protect/reference refresh | 部分 | 第 3 轮 |
| Workbench 多 tab | 部分完成 | microflowId 隔离，多 tab，dirty guard | 部分 | 第 4 轮 |
| Close/switch/refresh guard | 未完成 | dirty 时阻止或确认 | 否 | 第 4 轮 |
| Editor load/save | 部分完成 | 按 activeMicroflowId 加载保存真实 schema | 部分 | 第 5 轮 |
| save conflict UX | 部分完成 | baseVersion 冲突可恢复 | 否 | 第 5 轮 |
| Metadata selector | 部分完成 | 真实 metadata，无 mock fallback，stale 可处理 | 部分 | 第 5 轮 |
| Canvas drag/add/delete/edge | 部分完成 | 操作写 schema，保存后恢复 | 部分 | 第 5 轮 |
| Copy/paste/multiselect | 未完成/不完整 | 常用画布批量编辑能力 | 否 | 第 5 轮 |
| Problems 定位 | 部分完成 | error 可定位并阻止 publish/save gate | 部分 | 第 5 轮 |
| Publish | 部分完成 | validate/impact/version/snapshot/audit | 部分 | 第 5 轮 |
| Run/Trace | 部分完成 | input/run history/trace highlight/call stack | 部分 | 第 5 轮 |
| E2E | 未完成 | 覆盖目标页 create/list/save/publish/run/trace | 否 | 第 2 轮起 |
