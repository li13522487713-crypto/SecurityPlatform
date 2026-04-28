# Mendix Microflow 画布界面设计（mendix-studio-core 复刻）

> 本文档对齐用户 §2.1 / §2.3 / §3.x 清单，描述 Mendix Studio 风格 Microflow 画布在 Atlas
> `app-web` + `mendix-studio-core` + `@atlas/microflow` 三个层级中的最终结构与各区域的实现位置。
>
> 修改本文件时同步审视：
> - [`docs/microflow-node-registry.md`](microflow-node-registry.md) 节点目录
> - [`docs/microflow-runtime-engine-design.md`](microflow-runtime-engine-design.md) 后端引擎
> - [`docs/microflow-e2e-checklist.md`](microflow-e2e-checklist.md) 端到端验收清单

## 1. 路由与宿主

```
/space/:space_id/mendix-studio              -> MendixStudioIndexPage（应用入口卡片）
/space/:space_id/mendix-studio/:appId       -> MendixStudioApp（IDE 主壳）
/microflow/:microflowId/editor              -> MendixMicroflowEditorPage（独立路由，向后兼容）
```

- 路由文件：[`src/frontend/apps/app-web/src/app/pages/mendix-studio-route.tsx`](../src/frontend/apps/app-web/src/app/pages/mendix-studio-route.tsx)
- 工作区上下文：[`src/frontend/apps/app-web/src/app/workspace-context.tsx`](../src/frontend/apps/app-web/src/app/workspace-context.tsx)
- Adapter 配置工厂：[`src/frontend/apps/app-web/src/app/microflow-adapter-config.ts`](../src/frontend/apps/app-web/src/app/microflow-adapter-config.ts)
- Mendix Studio 主壳：[`src/frontend/packages/mendix/mendix-studio-core/src/index.tsx`](../src/frontend/packages/mendix/mendix-studio-core/src/index.tsx)

`MicroflowResourceEditorHost` **强制 `adapterBundle.mode === "http"` 且必须有 `metadataAdapter`**；
local / mock adapter 不会进入此宿主，元数据缺失时显示明确错误，**不会回退到 mock catalog**。

## 2. 布局总图（用户 §2.1）

```
+--------------------------------------------------------------+
| StudioHeader（应用名 / 搜索 / 帮助 / 通知 / 用户）             |
+----+--------------------------------------------------------+
| A  | WorkbenchTabs                                           |
| p  +---------------------------------------------------------+
| p  | MicroflowWorkbenchToolbar（外置工具栏，§3.2）           |
|    +---------------------------------------------------------+
| E  | +-----------------------+ +---------------------------+ |
| x  | | Node Toolbox          | | FlowGram Canvas + Property | |
| p  | | (mendix-microflow)    | |  Panel (mendix-microflow)  | |
| l  | +-----------------------+ +---------------------------+ |
| o  +---------------------------------------------------------+
| r  | MicroflowStudioBottomPanel（验证 / 配置 / 引用 / 信息） |
| e  +---------------------------------------------------------+
| r  |                                                         |
+----+---------------------------------------------------------+
```

- 顶部 Header：[`components/studio-header.tsx`](../src/frontend/packages/mendix/mendix-studio-core/src/components/studio-header.tsx)
- App Explorer：[`components/app-explorer.tsx`](../src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer.tsx)
- Workbench Tabs：[`components/workbench-tabs.tsx`](../src/frontend/packages/mendix/mendix-studio-core/src/components/workbench-tabs.tsx)
- 微流外置工具栏：[`components/microflow-workbench-toolbar.tsx`](../src/frontend/packages/mendix/mendix-studio-core/src/components/microflow-workbench-toolbar.tsx)
- 资源编辑器宿主：[`microflow/studio/MicroflowResourceEditorHost.tsx`](../src/frontend/packages/mendix/mendix-studio-core/src/microflow/studio/MicroflowResourceEditorHost.tsx)
- 微流编辑器适配器：[`microflow/editor/MendixMicroflowEditorEntry.tsx`](../src/frontend/packages/mendix/mendix-studio-core/src/microflow/editor/MendixMicroflowEditorEntry.tsx)
- 微流核心编辑器：[`packages/mendix/mendix-microflow/src/editor/index.tsx`](../src/frontend/packages/mendix/mendix-microflow/src/editor/index.tsx)
- 节点工具箱：[`packages/mendix/mendix-microflow/src/node-panel/index.tsx`](../src/frontend/packages/mendix/mendix-microflow/src/node-panel/index.tsx)
- 属性面板：[`packages/mendix/mendix-microflow/src/property-panel/index.tsx`](../src/frontend/packages/mendix/mendix-microflow/src/property-panel/index.tsx)
- 工作室级底部面板：[`components/microflow-studio-bottom-panel.tsx`](../src/frontend/packages/mendix/mendix-studio-core/src/components/microflow-studio-bottom-panel.tsx)

## 3. Workbench Tabs（用户 §3.1）

数据由 [`store.ts`](../src/frontend/packages/mendix/mendix-studio-core/src/store.ts) 维护：

- `workbenchTabs`、`activeWorkbenchTabId`
- `dirtyByWorkbenchTabId`、`saveStateByMicroflowId`、`validationSummaryByMicroflowId`

行为：

- 同一 `microflowId` 不重复打开（tabId 固定为 `microflow:${microflowId}`，已存在则只激活）。
- 切换 dirty Tab 时弹 Modal 阻止；关闭 dirty Tab 时通过 `atlas:microflow-save-request` 事件
  让编辑器主动保存后再关闭。
- 状态徽标使用 Semi `Tag`：草稿（draft，蓝）、已发布（published，绿）、已修改（modified，橙）、
  归档（archived，灰）、运行中（running，紫）、失败（error，红）、信息（info，淡绿）。
  `dirty=true` 时优先映射到「已修改」。

## 4. 顶部画布工具栏（用户 §3.2）

`MicroflowWorkbenchToolbar` 在微流 Tab 激活时取代 `WorkbenchToolbar`，按钮集合：

| 区段 | 按钮 |
|-----|-----|
| 主操作 | 保存 / 运行 / 调试运行 / 校验 / 发布 |
| 历史 | 撤销 / 重做 |
| 视口 | 缩小 / 缩放百分比 / 放大 / 适应画布 / 自动布局 / 小地图 / 全屏 |
| 状态 | 草稿/已保存/保存中 Tag、错误/警告/校验中 Tag、引用快捷入口 |

按钮通过 `editorRef.current.<method>()` 命令式触发；`getStatus()` 返回 `dirty / saving / running /
errorCount / warningCount / canUndo / canRedo / zoomPercent / fullscreen` 等用于 disabled / loading
判断。`MicroflowEditor` 在 `toolbarMode === "external"` 时隐藏自己的内部 toolbar，避免双层 toolbar。

## 5. 节点工具箱（用户 §4 / §5）

- 主组件：`MicroflowNodePanel`（mendix-microflow）。
- 注册表：[`node-registry/registry.ts`](../src/frontend/packages/mendix/mendix-microflow/src/node-registry/registry.ts)
  + [`node-registry/action-registry.ts`](../src/frontend/packages/mendix/mendix-microflow/src/node-registry/action-registry.ts)。
- 拖放协议：`MicroflowNodeDragPayload` -> 画布 `onDropRegistryItem` -> `handleAddNode`。
- engineSupport 角标：每个节点条目展示 `Partial Runtime` 或 `Runtime Unsupported` 标签 + tooltip 解释；
  `supported` 节点不显示徽标，避免视觉噪声。
- 过滤器：`all / favorites / enabled / supported / 各类别`。
- 全部 30+ 节点见 [`microflow-node-registry.md`](microflow-node-registry.md)。

## 6. 画布与逻辑关系（用户 §6）

- 画布主体：FlowGram Free Layout（[`flowgram/FlowGramMicroflowCanvas.tsx`](../src/frontend/packages/mendix/mendix-microflow/src/flowgram/FlowGramMicroflowCanvas.tsx)）。
- Authoring schema：`objectCollection.objects[]` + `flowCollection.flows[]`，由 `useFlowGramMicroflowBridge`
  双向同步。
- 边连接合法性：`canConnectPorts`（Decision/ObjectType/Loop/ErrorHandler 等连接策略）。
- 拖入节点 -> 加 object；连线 -> 加 flow；调整位置 -> `relativeMiddlePoint` 字段更新；
  保存 -> `PUT /api/v1/microflows/{id}/schema`；刷新 -> `GET /api/v1/microflows/{id}/schema` 后重建。

## 7. 属性面板（用户 §7）

- 入口：[`property-panel/index.tsx`](../src/frontend/packages/mendix/mendix-microflow/src/property-panel/index.tsx)
- 各 kind 表单：`forms/` 目录下 `event-nodes-form / exclusive-split-form / loop-node-form / merge-node-form
  / inheritance-split-form / parameter-object-form / annotation-object-form / action-activity-form`，
  以及本轮新增的 `parallel-gateway-form / inclusive-gateway-form / try-catch-form / error-handler-form`。
- 选中边时：`flow-edge-form`（Sequence / Decision / ObjectType / Annotation / ErrorHandler 各分支条件）。

## 8. 底部面板（用户 §2.1）

`MicroflowStudioBottomPanel` 4 个 Tab：

| Tab | 数据来源 |
|-----|----------|
| 验证结果 | `store.validationByMicroflowId / validationSummaryByMicroflowId`（来自 `apiClient.validateMicroflow`） |
| 配置检查 | 上述 issues 按 `source` 分组聚合 |
| 引用检查 | `adapter.getMicroflowReferences(microflowId)` 真实后端调用，懒加载，错误时显示重试 |
| 微流信息 | `StudioMicroflowDefinitionView`（id / module / version / publishStatus / archived / referenceCount / updatedAt） |

`MicroflowEditor` 内部仍保留自己的 Problems / Debug 面板用于独立路由 `/microflow/:id/editor`；
工作室路径下两层并存但主权交给工作室级面板。

## 9. 状态与去 mock

- 资源加载、保存、运行、校验、发布、引用查询、版本查询全部走 `MicroflowApiClient` 接 HTTP API。
- 默认 toolbox 配置不允许包含 `Sales.*` / `MF_<Name>` 等 mock 占位（由
  [`toolbox-cleanliness.spec.ts`](../src/frontend/packages/mendix/mendix-microflow/src/node-registry/toolbox-cleanliness.spec.ts)
  守门）。
- 元数据缺失时直接显示明确错误，不会偷偷使用 mock catalog；mock catalog 仅作为 dev/test 夹具保留。

## 10. 快捷键

由 `useMicroflowShortcuts`（[`editor/keyboard-shortcuts.ts`](../src/frontend/packages/mendix/mendix-microflow/src/editor)）注册：

| 快捷键 | 动作 |
|-------|------|
| Ctrl+S | 保存 |
| Ctrl+Z / Ctrl+Y | 撤销 / 重做 |
| Ctrl+C / Ctrl+V | 复制 / 粘贴节点 |
| Delete / Backspace | 删除选中节点或连线 |
| Esc | 清除选择 |
| Ctrl+F | 节点搜索（Toolbox 中聚焦搜索框） |
