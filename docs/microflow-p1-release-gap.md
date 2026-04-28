# Microflow P1 Release Gap

## 1. Executive Summary

当前 P1 真实完成度：后端 Microflow resource/schema/metadata/validate/publish/references/test-run/run-trace API 已有较完整雏形，前端目标页 `/space/:workspaceId/mendix-studio/:appId` 已能创建 HTTP adapter bundle。Release Stage 02 已完成 App Explorer 中 Microflows 分组的真实只读列表：通过 `resourceAdapter.listMicroflows({ workspaceId, moduleId })` 加载，支持 loading/empty/error/retry/search/refresh，并将真实资源写入 `microflowResourcesById` 与 `microflowIdsByModuleId`。Release Stage 04 已完成 microflowId 驱动的 Workbench tab 文档生命周期。Release Stage 05 已把真实 microflow tab 从资源 placeholder 切换为 `MicroflowResourceEditorHost`：按 `microflowId` 加载 resource/schema，复用 `MendixMicroflowEditorEntry` 与 save bridge，保存走 `PUT /api/microflows/{id}/schema`，并更新 store/tab/dirty。第 0 轮 Create Microflow Hotfix 基本通过：弹窗已 catch `onSubmit` rejection，失败不关闭弹窗，loading 可恢复，并展示 status/code/message/traceId/fieldErrors。

目标 `mendix-studio` 页面仍未达到完整发布化：Microflows 分组已真实化为只读列表，但 App Explorer 其他分组仍以 `SAMPLE_PROCUREMENT_APP` 静态数据为主，`moduleId` 来源仍是 sample procurement module；`appId` 没有驱动真实应用/模块资产树；Workbench Tabs 已具备 `microflow:{id}` 多文档 tab、`activeWorkbenchTabId`/`activeMicroflowId` 同步、per-tab dirty、close guard 和 beforeunload guard；Stage 05 已完成 schema load/save 与真实 MicroflowEditor host；Call Microflow metadata、Domain Model metadata、publish/run/trace 仍未完成。权限仍主要依赖 header/context 和生产 guard，Controller 标注 `[AllowAnonymous]`，未发现 workspace ownership 校验。

最大 blocker：目标页仍不是真实 app/module 资产树，`appId` 未进入 module tree 查询维度；Call Microflow metadata、Domain Model metadata、publish/run/trace、执行引擎仍未完成，且后端 API 无版本前缀、权限/租户隔离不足。Stage 0 Hotfix 若未来发现回归，作为单独 Release Blocker 记录，但不阻塞 Stage 05 资源感知编辑器宿主。

## 2. Hotfix Verification

| 检查项 | 当前状态 | 源码路径 | 证据 | 是否通过 | 遗留问题 | 后续建议 |
|---|---|---|---|---|---|---|
| CreateMicroflowModal 是否 catch onSubmit rejection | 已 catch | `src/frontend/packages/mendix/mendix-studio-core/src/microflow/resource/CreateMicroflowModal.tsx` | `handleSubmit` 中 `try { await onSubmit(...) } catch (caught) { ... } finally { setSubmitting(false) }` | 通过 | 无明显 uncaught 分支 | 保留现有模式 |
| 创建失败是否不会关闭弹窗 | 失败分支不调用 `onClose` | 同上 | `onClose()` 只在成功后调用 | 通过 | 无 | 保持失败态可见 |
| loading 是否恢复 | `finally` 恢复 | 同上 | `submittingRef.current = false; setSubmitting(false)` | 通过 | 无 | 无 |
| 展示 status/code/message/traceId/fieldErrors | 已展示 | 同上 | `submitError.status/code/message/traceId/fieldErrors` 渲染在“创建失败”区 | 通过 | field path 只做 `input.` 前缀归一 | 可继续增强字段定位 |
| name 前端校验是否与后端一致 | 基本一致 | `CreateMicroflowModal.tsx`; `src/backend/Atlas.Application.Microflows/Services/MicroflowResourceService.cs` | 前端 `/^[A-Za-z][A-Za-z0-9_]*$/u`; 后端 `^[A-Za-z][A-Za-z0-9_]*$` | 通过 | 参数名校验仍允许 `_` 开头，与微流 name 不同但不在本项范围 | 无 |
| 是否仍默认 moduleId = "sales" | 目标页不直接默认 sales；测试仍用 sales | `CreateMicroflowModal.spec.tsx`; `app-explorer.tsx` | 测试 `defaultModuleId="sales"`；目标页 `defaultModuleId={moduleId}`，moduleId 来自 sample procurement module | 部分通过 | 仍是 sample module，不是 URL/app 真实模块 | 第 2 轮替换 module 来源 |
| 是否仍可能出现 Uncaught in promise | 目标弹窗路径未发现 | `CreateMicroflowModal.tsx`; `app-explorer.tsx` | Modal `onOk={() => void handleSubmit()}`，内部 catch；AppExplorer `handleCreateMicroflow` 向上 throw 给弹窗 catch | 通过 | 其他独立旧 modal 需另审 | 后续移除重复旧实现 |
| 是否有成功、重复名、moduleId 缺失、后端不可用测试 | 大部分有，后端不可用前端有通用 reject | `CreateMicroflowModal.spec.tsx`; `tests/Atlas.AppHost.Tests/Microflows/MicroflowCreateHotfixTests.cs` | 前端覆盖 reject/loading/409/422/invalid/missing/double click；后端覆盖 success/invalid/missing/duplicate/envelope | 部分通过 | 未发现真实 HTTP 后端不可用集成测试 | 补 E2E/contract test |
| 后端 POST /api/microflows 是否返回统一 envelope / traceId | 已返回 envelope | `MicroflowResourceController.cs`; `MicroflowApiControllerBase.cs`; `MicroflowApiExceptionFilter.cs` | `Create` 返回 `MicroflowOk(resource)`；异常 filter 写 `X-Trace-Id` | 通过 | 成功 traceId 来自 base；非异常模型验证路径需继续覆盖 | 增加 contract test |
| 是否仍把 409/422/401/403/500 全部显示为“微流服务不可用” | 已区分 | `CreateMicroflowModal.tsx`; `microflow-api-error.ts` | `resolveReadableErrorMessage` 区分 401/403/409/422/500/network | 通过 | 通用 `getMicroflowErrorUserMessage` 的 403 文案偏“创建微流” | 统一按 action 生成文案 |

Hotfix 结论：第 0 轮 Hotfix 通过本轮源码审计；不是 Release Blocker。本轮未修改 `CreateMicroflowModal`，也不依赖 Create Microflow 成功路径。`moduleId` 来源仍是 sample Procurement module，作为真实 module tree 后续 blocker 保留。

## 3. Route / Adapter / Store

### Route Context

| 检查项 | 源码路径 | 当前实现 | 是否接入目标页面 | 缺口 | 发布风险 | 建议 |
|---|---|---|---|---|---|---|
| 读取 workspaceId | `apps/app-web/src/app/pages/mendix-studio-route.tsx`; 实际路径 `src/frontend/apps/app-web/src/app/pages/mendix-studio-route.tsx` | `useWorkspaceContext()` 后传 `workspace.id` | 是 | 用户列出的 `apps/...` 根路径源码中未发现，实际在 `src/frontend/apps/...` | 路径文档需统一 | 以后按实际 monorepo 路径引用 |
| 读取 appId | `mendix-studio-route.tsx` | `useParams<{ appId: string }>()` | 是 | `appId` 只传入 `MendixStudioApp`，未用于 app/module 资产查询 | 不同 app 可能看到同一 sample 树 | 第 2 轮把 appId 接入资产树/模块 API |
| 读取 tenantId/orgId | `workspace-context.tsx`; `mendix-studio-route.tsx` | `workspace.orgId` 作为 `tenantId` | 是 | 未发现 org ownership 验证 | 租户隔离弱 | 与登录态/租户中间件对齐 |
| 创建 `createAppMicroflowAdapterConfig` | `microflow-adapter-config.ts` | 默认 `mode` 为 http，`apiBaseUrl` 默认 `/api` | 是 | 未传 Authorization；只提供 `requestHeaders` 扩展但 route 未传 | 依赖浏览器同源 cookie 或全局 fetch，Bearer 不显式 | 接入 auth header 来源 |
| 传入 adapterConfig/bundle | `mendix-studio-route.tsx`; `index.tsx` | route 传 `adapterConfig`; `MendixStudioApp` 创建 `_resolvedBundle` | 是 | bundle 创建失败只 `console.warn` | 页面进入半可用状态 | 目标页展示硬错误 |
| 传 currentUser | `microflow-adapter-config.ts`; `index.tsx` | 类型支持，route 未传 | 否 | `X-User-Id` 不会由目标页注入 | 审计字段缺失 | 从登录态注入 currentUser |
| Authorization/requestHeaders 透传 | `microflow-api-client.ts` | client 支持 `requestHeaders`，自动加 workspace/tenant/user header | 部分 | 目标 route 未构造 Authorization/requestHeaders | 401/403 真实场景不稳定 | 第 2/权限轮接入 token |
| 区分 workspace/app/module | `store.ts`; `app-explorer.tsx` | store 有 `workspaceId/appId`；moduleId 来自 `SAMPLE_PROCUREMENT_APP.modules[0]` | 部分 | `appId` 不参与 moduleId | 多应用错读错写 | 发布化真实 module |
| 独立 editor route 差异 | `app.tsx`; `MendixMicroflowEditorPage.tsx` | `/microflow/:microflowId/editor` 独立加载 resource，不在 studio workbench | 否，独立路由 | 不能等同目标页能力 | 审计误判风险 | 分开验收 |

### Adapter Bundle

| Adapter | 源码路径 | 当前实现 | 是否用于目标页面 | 是否可能 fallback mock/local | 发布缺口 | 建议 |
|---|---|---|---|---|---|---|
| createMicroflowAdapterBundle | `mendix-studio-core/src/microflow/adapter/microflow-adapter-factory.ts` | 根据 mode 创建 `mock/local/http` bundle | 是 | env 可设 mock/local | 生产禁用策略未在目标页强制 | 生产构建校验 mode=http |
| resourceAdapter | `http/http-resource-adapter.ts` | GET/POST/PATCH/PUT/DELETE `/api/microflows` | 是 | http 模式不 fallback | moduleId 仍 sample | 第 2 轮接真实模块 |
| metadataAdapter | `microflow/metadata/http-metadata-adapter.ts` | GET `/api/microflow-metadata*` | 是，传给 editor | mock/local bundle 会用 mock/local metadata | 目标页未显式禁止 mock mode | 生产 guard |
| runtimeAdapter | `http/http-runtime-adapter.ts` | schema load/save/validate/test-run/run trace | 是 | mock/local bundle 走 local runtime | `StudioEmbeddedMicroflowEditor` 非 http 拒绝，但 bundle 仍可生成 | 生产配置锁定 |
| validationAdapter | `microflow-validation-adapter.ts` | http POST validate；local adapter 本地 validate | 是 | `validationMode=local` 可本地 | save/publish gate 仍需端到端验证 | 发布前强制 server validate |
| App Explorer bundle | `index.tsx`; `app-explorer.tsx` | `_resolvedBundle` 传入 `AppExplorer` | 是 | bundle undefined 时错误态 | 无全局失败页 | 明确配置错误页 |
| MicroflowEditor bundle | `index.tsx`; `StudioEmbeddedMicroflowEditor.tsx` | 传 resource/metadata/validation/runtime adapter | 是 | 非 http 明确拒绝 | 保存依赖 active tab | 加载失败可观测 |

### Store

| 状态位置 | 源码路径 | 当前实现 | 缺口 | 建议 |
|---|---|---|---|---|
| `workspaceId/appId` | `mendix-studio-core/src/store.ts` | `setStudioContext({ appId, workspaceId })` | 未影响资产树/API | 第 2 轮纳入查询上下文 |
| `microflowResourcesById` | `store.ts`; `app-explorer.tsx` | Stage 02 App Explorer list 后写入真实只读资源 | 未按 appId 隔离 | key 增加 workspace/app/module |
| `activeMicroflowId` | `store.ts`; `app-explorer.tsx` | Stage 02 点击真实微流节点时设置，不打开 editor tab | 无 dirty guard/真实 editor host | Workbench/editor 轮处理 |
| `microflowSchema` | `store.ts` | 初始仍 `sampleOrderProcessingMicroflow` | 目标页真实 editor 不直接用；但 store 仍残留 sample | 后续清理示例态 |

## 4. App Explorer

| 能力 | 源码路径 | 当前实现 | 是否真实数据 | 是否硬编码/mock | 是否接入目标页面 | 缺口 | 发布建议 |
|---|---|---|---|---|---|---|---|
| 是否仍使用 TREE_DATA | `components/app-explorer.tsx` | Stage 02 改为 `STATIC_TREE_DATA`，仅非 Microflows 分组保留 sample/static | Microflows children 是真实数据 | 非 Microflows 仍 sample/static | 是 | 完整 module tree 未真实化 | 后续 module API |
| Microflows 分组 list | `app-explorer.tsx` `loadMicroflows` | `resourceAdapter.listMicroflows({workspaceId,moduleId})` | 是，Microflows children 真实 list | moduleId sample | 是 | 只真实微流列表，不是真实模块树 | module API |
| loading/empty/error/retry | `microflow-tree-section.tsx`; `app-explorer.tsx` | loading/empty/error/retry node | 是 | 否 | 是 | 粒度仅 Microflows | 已完成 Stage 02 |
| search/filter | `app-explorer-tree.tsx` | 200ms debounce，本地过滤 label/name/displayName/qualifiedName | 是，已加载数据本地过滤 | 非 Microflows 仍静态 | 是 | 无后端 search | 后续可接服务端 search |
| context menu | `app-explorer-tree.tsx` | Microflows Refresh；微流 Open/Select/Refresh；CRUD disabled | 只读项真实 | CRUD 禁用 | 是 | CRUD 未实现 | 第 3 轮 |
| New | `CreateMicroflowModal`; `app-explorer-tree.tsx` | App Explorer 本轮不再触发 Create；菜单 disabled | 否，本轮禁止 | disabled 占位 | 是 | CRUD 未闭环 | 第 3 轮 |
| Rename/Duplicate/Delete/References/Refresh | `app-explorer-tree.tsx`; `http-resource-adapter.ts` | Refresh 调 list；Rename/Duplicate/Delete/References disabled | Refresh 真实 | CRUD/References 占位 | 是 | CRUD 未闭环 | 第 3 轮 |
| 点击微流打开 tab | `handleSelect` | Stage 02 只设置 selected/active ids，不打开 tab | 是，id 来自真实 list | 否 | 是 | Workbench tab 未接入本轮 | 第 4/5 轮 |
| 是否仍打开 sampleOrderProcessingMicroflow | `store.ts`; `app-explorer.tsx` | store 初始仍有 sample；点击真实微流不打开 editor，不展示 sample | 点击真实微流否 | store 残留 sample | 是 | page/workflow 初始示例仍存在 | 后续清理 |
| moduleId 来源 | `getCurrentExplorerModuleId`; `SAMPLE_PROCUREMENT_APP` | `SAMPLE_PROCUREMENT_MODULE?.moduleId`，集中封装 | 否 | 是 | 是 | 真实模块缺失 | 后续 module API |
| 是否仍默认 sales | `CreateMicroflowModal.spec.tsx` | 测试默认 sales；目标页不是 sales 而是 sample procurement | 否 | 测试硬编码 | 目标页否 | 测试与目标页不完全一致 | 调整测试夹具 |

## 5. Workbench Tabs

| 能力 | 源码路径 | 当前实现 | 是否 microflowId 隔离 | 缺口 | 发布风险 | 建议 |
|---|---|---|---|---|---|---|
| `workbenchTabs` | `store.ts` | 数组状态，初始 page/workflow sample | 部分 | 初始示例 | 用户误以为真实 app | 真实 app 初始化 |
| tab id 按 microflowId | `getMicroflowWorkbenchTabId` | `microflow:${microflowId}` | 是 | 无 app/workspace 前缀 | 跨 app 同 id 理论冲突 | key 增上下文 |
| 多 microflow tab | `openMicroflowWorkbenchTab` | 可 append 多 tab | 是 | 依赖资源索引 | 可用性取决于 Explorer | 保留 |
| `activeWorkbenchTabId` | `store.ts` | 已有，打开/切换/关闭 microflow tab 时同步 `activeMicroflowId` | 是 | 非微流 tab 会清空 activeMicroflowId | 符合 Stage 04 策略 | 保留 |
| `dirtyByTabId` | `store.ts` | 已有 `dirtyByWorkbenchTabId` 独立 map，tab.dirty 仅用于 UI 标识 | 是 | 尚无真实 editor dirty 来源 | Stage 5 接 editor 后需要接真实 dirty | 保留 |
| close guard | `workbench-tabs.tsx`; `store.ts` | dirty tab 首次关闭打开确认框，Save disabled，Discard force close，Cancel 保留 | 是 | Save 尚未接入 | Stage 5 schema save 后启用 | 保留 |
| switch guard | `WorkbenchTabs.handleTabClick` | 默认允许切换 dirty tab且不清 dirty | 是 | 未做切换确认 | 当前策略符合第 4 轮 | 保留 |
| refresh guard | `index.tsx`; `store.ts` | dirty map 非空时注册 `beforeunload` guard | 是 | 不自动保存 | Stage 5/14 完善 save/autosave/conflict | 保留 |
| undo/redo per microflow | `store.ts`; `workbench-toolbar.tsx` | 预留 `historyKey`、`canUndoByWorkbenchTabId`、`canRedoByWorkbenchTabId`，按钮默认 disabled | 框架已建 | 未接真实 editor history | Stage 5/6 接真实 history | 保留 |
| 删除微流后 tab 关闭 | `removeStudioMicroflow` | 调 `removeMicroflowWorkbenchTab` | 是 | 仅前端状态 | 后端外部删除需刷新 | 保留 |
| rename title 同步 | `renameMicroflowWorkbenchTab`; `upsertStudioMicroflow` | 已同步 | 是 | 依赖操作路径 | 外部 rename 需 reload | 保留 |
| duplicate 不覆盖源 tab | `handleDuplicateMicroflow` | sync 新 resource，不主动打开覆盖源 | 是 | 不自动打开 duplicate | UX 待定 | 第 3 轮定义 |
| 是否固定 activeTab | `store.ts`; `setActiveWorkbenchTab` | 不再只有固定 `microflowDesigner`，会按 tab kind 映射 | 是 | 初始 activeTab pageBuilder | 无 | 保留 |

## 6. Editor Host / Save Load

| 能力 | 源码路径 | 当前实现 | 是否真实后端 | 是否 microflowId 隔离 | 缺口 | 发布建议 |
|---|---|---|---|---|---|---|
| 目标页使用 Entry | `index.tsx`; `MicroflowResourceEditorHost.tsx` | 目标页 microflow tab 渲染 `MicroflowResourceEditorHost`，宿主复用 `MendixMicroflowEditorEntry` | 是 | 是 | toolbox/property/metadata 专项未做 | 后续专项 |
| 按 activeMicroflowId 加载 resource | `MicroflowResourceEditorHost.tsx`; `http-resource-adapter.ts` | active tab -> `microflowId` -> `resourceAdapter.getMicroflow(id)` | 是 | 是 | 请求取消为忽略旧响应策略 | 保留 |
| 按 activeMicroflowId 加载 schema | `MicroflowResourceEditorHost.tsx`; `http-resource-adapter.ts` | `resourceAdapter.getMicroflowSchema(id)` -> `GET /api/microflows/{id}/schema` | 是 | 是 | 不做历史 migration | 后续需要时处理 |
| 注入真实 apiClient/resource/metadata/validation | `microflow-adapter-factory.ts`; `MendixMicroflowEditorEntry.tsx`; `MicroflowResourceEditorHost.tsx` | http bundle 注入；metadata 缺失时报错；validation 缺失警告 | 是 | 是 | Authorization 未显式 | 权限轮补 |
| 是否仍传 store.microflowSchema | `index.tsx`; `store.ts` | 目标微流 tab 不传 store schema；store 仍保留 sample 仅作 legacy 示例状态 | 目标页微流 tab 否 | 是 | sample 状态残留 | 后续清理 |
| editor key 包含 id/schema/version | `StudioEmbeddedMicroflowEditor.tsx`; `MendixMicroflowEditorEntry.tsx` | key `${microflowId}:${schemaId}:${version}` / `${id}:${schemaId}:${version}` | 是 | 是 | 无 | 保留 |
| 保存 PUT schema | `editor-save-bridge.ts`; `http-resource-adapter.ts`; `MicroflowResourceEditorHost.tsx` | `createMicroflowEditorApiClient` 调 `resourceAdapter.saveMicroflowSchema`，HTTP adapter PUT `/api/microflows/{id}/schema` | 是 | 是 | conflict modal 未做 | Stage 14 |
| 是否可能走 createLocalMicroflowApiClient | `microflow-adapter-factory.ts`; `MicroflowResourceEditorHost.tsx` | 目标真实 microflow tab 强制 `mode === "http"`，且显式传 `apiClient` 给 `MicroflowEditor` | 目标真实路径否 | 是 | env 配成 local/mock 会显示错误而非保存 | 生产配置锁定 |
| A/B useState schema 复用 | `MendixMicroflowEditorEntry.tsx` | `useEffect` reset；key 包含 id/schema/version | 是 | 是 | 切换 guard 无 | 第 4 轮 |
| 保存失败错误 | `mendix-microflow/src/editor/index.tsx` | catch Toast error，validationIssues 打开 Problems | 是 | 是 | 版本冲突 UX 未完整 | 第 5 轮 |
| baseVersion/schema conflict | `MicroflowResourceService.SaveSchemaAsync`; `editor-save-bridge.ts` | 后端校验 baseVersion；前端传 `schemaId || version` | 是 | 是 | conflict 恢复/merge 未实现 | 第 5 轮 |

## 7. Canvas / Toolbox / Property Panel

### Canvas

| 能力 | 源码路径 | 当前实现 | 是否进入 authoring schema | 是否保存刷新恢复 | 缺口 | 建议 |
|---|---|---|---|---|---|---|
| drag node | `FlowGramMicroflowCanvas.tsx`; `editor/index.tsx` | DnD payload -> `onDropRegistryItem` -> add object | 是 | 是，随 schema PUT | 需 E2E | 补目标页 E2E |
| click add node | `node-panel/index.tsx`; `editor/index.tsx` | double click/context add | 是 | 是 | 默认位置简单 | 优化 UX |
| node position | `useFlowGramMicroflowBridge.ts` | `flowGramPositionPatch` -> schema editor position | 是 | 是 | viewport skip dirty | 保留 |
| node delete | `editor/index.tsx` | Delete/Backspace -> `deleteObject` | 是 | 是 | 无确认 | 后续增强 |
| node duplicate | `editor/index.tsx` | `duplicateObject` 已导入并用于交互 | 是 | 是 | 多选 duplicate 未确认 | 补测 |
| multiselect | `FlowGramMicroflowCanvas.tsx`; `useFlowGramMicroflowBridge.ts` | 选择服务仅同步单 selection | 部分 | 部分 | 多选操作未完整发现 | 后续实现 |
| copy/paste | `editor/index.tsx` | 源码中未发现完整 clipboard copy/paste 实现 | 否 | 否 | 缺失 | 第 5 轮 |
| edge create/delete | `useFlowGramMicroflowBridge.ts` | detect new/deleted edge -> add/delete flow | 是 | 是 | 复杂 case 需测试 | 保留 |
| source/target/handle | `flowgram-edge-factory.ts`; `useFlowGramMicroflowBridge.ts` | 通过 ports/handles 创建 flow | 是 | 是 | 依赖 registry | 保留 |
| Decision true/false branch | `registry.ts`; `FlowGramMicroflowCaseEditor` | true/false ports + pending case editor | 是 | 是 | 交互 E2E 缺失 | 补测 |
| viewport persistence | `editor/index.tsx` | `onViewportChange` 写 `schema.editor.viewport` 但 skipDirty | 部分 | 需后续保存触发 | 视口变更本身不 dirty | 定义是否持久 |
| undo/redo | `history` + `editor/index.tsx` | 内部 history manager | 是 | 会随保存持久 schema，不持久 history | per tab guard 缺 | 第 4 轮 |
| auto layout | `layout`; `editor/index.tsx` | `applyAutoLayout` | 是 | 是 | 无后端布局校验 | 保留 |
| minimap/grid | `FlowGramMicroflowCanvas.tsx`; `editor/index.tsx` | `showMiniMap/gridEnabled` 写 schema.editor | 是 | 是 | 需保存 | 保留 |

### Toolbox

| 节点类型 | 注册路径 | 默认配置 | 是否含 mock/demo | 是否可拖拽 | 是否可保存 | 缺口 |
|---|---|---|---|---|---|---|
| Start | `node-registry/registry.ts` | `eventEntry("startEvent")` | 否 | 是 | 是 | 无 |
| End | `registry.ts` | `eventEntry("endEvent")` | 否 | 是 | 是 | 无 |
| Parameter | `registry.ts` | parameter object unknown type | 否 | 是 | 是 | 需真实参数 UX |
| Decision | `registry.ts` | empty expression Boolean | 否 | 是 | 是 | case UX 需测 |
| Merge | `registry.ts` | `strategy:firstArrived` | 否 | 是 | 是 | 无 |
| Create Variable | `action-registry.ts`; `registry.ts` | `createDefaultActionConfig("createVariable")` | 否 | 是 | 是 | 需表达式校验 |
| Change Variable | 同上 | default config | 否 | 是 | 是 | 无 |
| Call Microflow | `action-registry.ts`; `action-activity-form.tsx` | target empty | 否 | 是 | 是 | 依赖真实 metadata |
| Loop | `registry.ts` | forEach + `$currentIndex` | 否 | 是 | 是 | loop body E2E |
| Break | `registry.ts` | `breakEvent` | 否 | 是 | 是 | scope 校验需覆盖 |
| Continue | `registry.ts` | `continueEvent` | 否 | 是 | 是 | scope 校验需覆盖 |
| Create List | `action-registry.ts` | empty entity/list variable | 否 | 是 | 是 | metadata required |
| Change List | 同上 | operation add | 否 | 是 | 是 | 无 |
| Aggregate List | 同上 | count | 否 | 是 | 是 | 无 |
| Create Object | `registry.ts`; `action-registry.ts` | entity empty | 否 | 是 | 是 | metadata required |
| Retrieve Object | 同上 | source empty | 否 | 是 | 是 | metadata required |
| Change Object | 同上 | member changes empty | 否 | 是 | 是 | metadata required |
| Commit Object | 同上 | commit default | 否 | 是 | 是 | runtime 覆盖有限 |
| Delete Object | 同上 | delete default | 否 | 是 | 是 | runtime 覆盖有限 |
| REST Call | `action-registry.ts` | url empty | 否 | 是 | 是 | real HTTP 默认受策略限制 |
| Annotation | `registry.ts` | text default | 否 | 是 | 是 | 无 |

### Property Panel

| 表单 | 源码路径 | 当前支持字段 | 是否写回 schema | 是否 dirty | 是否保存恢复 | 缺口 |
|---|---|---|---|---|---|---|
| document properties | `editor/index.tsx` | schema toolbar/metadata，不是独立文档表单 | 部分 | 部分 | 部分 | 源码中未发现完整 document property form |
| node base properties | `property-panel/forms/object-base-form.tsx`; `object-panel.tsx` | title/description/docs 等 | 是 | 是 | 是 | 需逐字段测 |
| edge properties | `forms/flow-edge-form.tsx` | label/case/error 等 | 是 | 是 | 是 | case UX 需测 |
| Start/End/Parameter | `event-nodes-form.tsx`; `parameter-object-form.tsx` | event/return/parameter | 是 | 是 | 是 | 无 |
| Decision/Merge | `exclusive-split-form.tsx`; `merge-node-form.tsx`; `inheritance-split-form.tsx` | expression/case/strategy | 是 | 是 | 是 | metadata stale 需测 |
| Variable actions | `action-activity-form.tsx`; `generic-action-fields-form.tsx` | create/change variable fields | 是 | 是 | 是 | 表达式编辑器深度有限 |
| Object actions | `action-activity-form.tsx`; selectors | entity/member/object variable | 是 | 是 | 是 | 依赖 metadata |
| List actions | `action-activity-form.tsx`; `generic-action-fields-form.tsx` | list variable/entity/operation | 是 | 是 | 是 | 依赖 metadata |
| Loop/Break/Continue | `loop-node-form.tsx`; `event-nodes-form.tsx` | loop config/event | 是 | 是 | 是 | scope UX |
| Call Microflow | `action-activity-form.tsx`; `MicroflowSelector.tsx` | `targetMicroflowId` 为主，qualifiedName 快照展示 | 是 | 是 | 是 | 真实 metadata loading UX 需补 |
| REST Call | `generic-action-fields-form.tsx`; `action-activity-form.tsx` | URL/method/body/response/error | 是 | 是 | 是 | runtime 支持受限 |
| expression editor | `property-panel/expression` | expression raw/text/references | 是 | 是 | 是 | 非完整 Mendix expression IDE |
| metadata selectors | `selectors/*`; `metadata-provider.tsx` | entity/enumeration/microflow/page/workflow refs | 是 | 是 | 是 | 缺失 adapter 时不 fallback，但错误 UX 简单 |

## 8. Metadata

| Metadata 能力 | 源码路径/API | 当前实现 | 是否 mock | 是否接入目标页面 | 缺口 | 建议 |
|---|---|---|---|---|---|---|
| provider 默认 mock | `mendix-microflow/src/metadata/metadata-provider.tsx` | 缺 adapter 报错，不 fallback mock | 否 | 是 | 需要真实 adapter | 保留 |
| 目标页注入真实 adapter | `StudioEmbeddedMicroflowEditor.tsx`; `microflow-adapter-factory.ts` | http bundle `metadataAdapter` 注入 | 否 | 是 | mode 可配置 local/mock | 生产锁 http |
| target list API | `http-metadata-adapter.ts` | GET `/api/microflow-metadata/microflows` | 否 | 是 | 无 appId | 加 app/module 维度 |
| entities/enumerations API | `http-metadata-adapter.ts`; `MicroflowMetadataController.cs` | GET `/api/microflow-metadata` 和子资源 | 否 | 是 | seed 默认 demo-workspace | 真实域模型接入 |
| Sales.* mock | `mock-metadata.ts` | Sales/Inventory/System mock catalog 存在 | 是 | http 目标页否 | local/mock 模式可能出现 | 禁生产 mock |
| loading/error/empty | `metadata-provider.tsx`; selectors | context 暴露 loading/error；selector 显示错误 | 否 | 是 | empty UX 分散 | 统一状态 |
| cache by workspace/module | `metadata-provider.tsx`; backend metadata repository | request 含 workspace/module；后端 cache by workspace/tenant | 否 | 是 | 前端无显式 stale 策略 | 加失效策略 |
| stale 处理 | `validators/validate-actions.ts` | stale qualifiedName warning | 否 | 是 | cache stale 未自动刷新 | 保存/发布前 refresh |
| Call Microflow 主键 | `action-activity-form.tsx`; `call-microflow-config.ts` | `targetMicroflowId` 为主 | 否 | 是 | 无目标 app 限定 | 保留 ID 主键 |
| qualifiedName 快照 | `action-activity-form.tsx`; `validators/validate-actions.ts` | 展示/校验 stale warning | 否 | 是 | rename 后需刷新 | 引用刷新 |

## 9. Validation / Problems

| 校验能力 | 源码路径/API | 当前实现 | 是否阻止保存/发布 | 是否可定位 | 缺口 | 建议 |
|---|---|---|---|---|---|---|
| 本地 validation | `mendix-microflow/src/validators/*`; `editor/index.tsx` | `validateMicroflowSchema` | 保存前不强阻止；test-run 会 gate | 是，problem click 定位 | save gate 不明确 | 第 5 轮定义 save gate |
| 后端 validate API | `MicroflowResourceController.cs`; `MicroflowValidationService.cs` | POST `/api/microflows/{id}/validate` | publish/test-run 使用 | 返回 issue 字段 | 无 auth | 权限补齐 |
| Problems panel | `editor/index.tsx` | bottom problems tab | 部分阻止 | 是 | 与 studio bottom panel 分离 | 统一 UX |
| issue 字段 | `schema/types.ts`; `MicroflowValidationDtos.cs` | severity/code/message/objectId/flowId/fieldPath 等 | 部分 | 是 | 后端 DTO 直接用 contract issue | 契约固化 |
| 点击 issue 定位 | `editor/index.tsx` | `viewportForProblemIssue` | 是 | 是 | 需 E2E | 补测 |
| save gate | `editor/index.tsx` | save 直接调用 API，失败后显示 issues | 否/后端兜底 | 是 | error 未必阻止保存 | 第 5 轮 |
| publish gate | `PublishMicroflowModal` + backend publish | publish 前 validation/impact | 是 | 部分 | 目标页问题面板联动弱 | 加定位联动 |
| A/B issues 隔离 | `MicroflowEditor` key; local state | component key 隔离 | 是 | 是 | tab 卸载后 issues 丢失 | per tab issues store |
| 覆盖参数/变量/Decision/Call/Loop/List/Object/Reference | `validators/*`; tests | 多类 validator 已存在 | 部分 | 是 | E2E 不足 | 扩大测试 |
| 是否只是 console warning | 多处 | 不是，仅少数缺资源 console.warn | 否 | 是 | console warning 仍存在于缺资源 | 保留但增加 UI |

## 10. References / Publish / Run / Trace

### References

| 能力 | 源码路径/API | 当前实现 | 是否目标页面接入 | 缺口 | 建议 |
|---|---|---|---|---|---|
| callers/callees | `MicroflowResourceController.cs`; `MicroflowReferenceService.cs` | GET references；源码中未发现 GET callees | references 是 | callees API 缺 | 后续补 `/callees` |
| delete protect | `app-explorer.tsx`; `MicroflowResourceService.DeleteAsync` | 前端预查；后端按 referenceCount/active references 保护 | 是 | 需集成测试 | 保留 |
| referenceCount | `MicroflowResourceDto`; `app-explorer.tsx` | 列表展示 ref count | 是 | 更新时机依赖 indexer | 补刷新 |
| rename stability | `validators/validate-actions.ts`; `MicroflowReferencesDrawer` | ID 主键，qualifiedName stale warning | 是 | rename 后引用抽屉刷新有限 | 第 3 轮 |
| duplicate reference refresh | `handleDuplicateMicroflow` | seed++ 刷新 drawer | 是 | 不自动打开 duplicate | 定义 UX |

### Publish

| 能力 | 源码路径/API | 当前实现 | 是否目标页面接入 | 缺口 | 建议 |
|---|---|---|---|---|---|
| validate before publish | `PublishMicroflowModal`; `MicroflowPublishService.cs` | 有 validationAdapter/backend publish | 是 | UI 问题定位弱 | 加联动 |
| version notes | `MicroflowVersionPublishDtos.cs` | `description` | 是 | UX 简单 | 保留 |
| published snapshot | `MicroflowPublishSnapshotEntity` | 有实体/snapshot DTO | 是 | 审计权限弱 | 权限轮 |
| changedAfterPublish | `MicroflowResourceService.SaveSchemaAsync` | 保存 published 后标记 | 是 | 前端标识存在 | 保留 |
| rollback | `MicroflowResourceController.cs` versions rollback | 独立 drawer 接入 | 部分 | 目标页入口在版本 drawer | 补 E2E |
| audit | 源码中未发现 Microflow 专用 audit writer | 未发现 | 否 | 发布审计不足 | 后续补审计 |

### Run / Debug

| 能力 | 源码路径/API | 当前实现 | 是否目标页面接入 | 缺口 | 建议 |
|---|---|---|---|---|---|
| test-run | `http-runtime-adapter.ts`; `MicroflowResourceController.cs` | POST `/api/microflows/{id}/test-run` | 是 | 权限弱 | 补权限 |
| input form | `MicroflowTestRunModal.tsx`; `run-input-model.ts` | 根据参数生成输入 | 是 | E2E 不足 | 补测 |
| runtime response | `MicroflowRuntimeDtos.cs` | session/error/trace/logs/variables | 是 | 与真实运行时仍 mock runner 命名 | 明确支持矩阵 |
| unsupported nodes/errors | `MicroflowMockRuntimeRunner.cs`; `runtime-error-codes.ts` | 返回 unsupported/error | 是 | 覆盖不足 | 补测试 |
| call stack | `MicroflowRuntimeDtos.cs`; `CallMicroflowActionExecutor.cs` | callStack/childRuns | 是 | E2E 缺 | 补测 |

### Trace

| 能力 | 源码路径/API | 当前实现 | 是否目标页面接入 | 缺口 | 建议 |
|---|---|---|---|---|---|
| nodeResults/run history | `MicroflowRunHistoryPanel.tsx`; `MicroflowResourceController.cs` | list/get runs | 是 | UX 与目标 studio 未系统验收 | 补 E2E |
| trace panel | `MicroflowTracePanel.tsx` | bottom debug trace | 是 | 与 Studio bottom panel 分离 | 统一 |
| canvas highlight | `FlowGramMicroflowNodeRenderer.tsx`; `FlowGramMicroflowCanvas.tsx` | runtimeTrace 传入 | 是 | 覆盖不足 | 补测 |
| click locate | `editor/index.tsx` | trace frame 定位 object/viewport | 是 | 需 E2E | 补测 |

## 11. Auth / Tenant / Workspace

| 能力 | 前端源码 | 后端源码 | 当前实现 | 发布风险 | 建议 |
|---|---|---|---|---|---|
| workspaceId 来源 | `mendix-studio-route.tsx`; `workspace-context.tsx` | `HttpMicroflowRequestContextAccessor.cs` | 前端 header/query；后端读 `X-Workspace-Id` | workspace ownership 未发现 | 接入 workspace 权限服务 |
| tenantId/orgId 来源 | `mendix-studio-route.tsx`; `microflow-api-client.ts` | `HttpMicroflowRequestContextAccessor.cs`; repositories | 前端 `workspace.orgId` -> `X-Tenant-Id`; 后端按 TenantId filter | 未校验 JWT tenant match | 接入标准 tenancy |
| currentUser 来源 | `microflow-adapter-config.ts` | `HttpMicroflowRequestContextAccessor.cs` | 类型支持，目标页未传 | `X-User-Id` 缺失 | 从 auth store 注入 |
| Authorization | `microflow-api-client.ts` | Controller `[AllowAnonymous]` | requestHeaders 支持但 route 未传；后端匿名 | 高 | 移除匿名/接入 JWT |
| X-Workspace-Id | `microflow-api-client.ts` | context accessor/production guard | 自动传 | 仅生产 guard 要求 | 开发也可校验 |
| X-Tenant-Id | `microflow-api-client.ts` | repositories tenant filter | 自动传 | header 可伪造 | JWT/tenant 校验 |
| X-User-Id | `microflow-api-client.ts` | context accessor | currentUser 存在才传 | 目标页未传 | 注入 user |
| workspace ownership | 源码中未发现 | 源码中未发现 | 未发现 | 越权读写风险 | 必补 |
| 无权限按钮禁用 | `MendixMicroflowEditorEntry.tsx`; `MicroflowResourceDto` | permissions DTO 固定映射待查 | editor readonly 部分基于 permissions | Explorer 菜单未按权限细分 | 权限态统一 |
| 401/403 区分 | `microflow-api-error.ts`; `CreateMicroflowModal.tsx` | `MicroflowProductionGuardFilter.cs` | 文案区分 | 后端大多匿名 | 接入真实 auth |
| create/save/delete/publish/run 权限 | 前端局部 disabled | Controller `[AllowAnonymous]` | 未发现细粒度校验 | 高 | 发布前必须补 |

## 12. E2E / Tests

| 测试类型 | 文件路径 | 覆盖能力 | 当前状态 | 缺口 | 建议 |
|---|---|---|---|---|---|
| Playwright | `src/frontend/package.json`; `src/frontend/e2e/app/agent-workbench.spec.ts` | 存在 e2e 目录但未发现 Playwright config | 部分 | 未发现 mendix-studio E2E | 补 config/route test |
| mendix-studio route E2E | 源码中未发现 | 无 | 缺失 | 目标页未验收 | 第 2 轮前补 |
| create success/failure | `CreateMicroflowModal.spec.tsx`; `MicroflowCreateHotfixTests.cs` | 前后端单测 | 部分 | 无浏览器真实 route create | 补 E2E |
| App Explorer real list | `src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer.spec.tsx` | loading/empty/error/retry/real nodes/search/click/refresh/sample 隔离 | Stage 02 已补单测 | 浏览器 E2E 未执行 | 补 route E2E/后端联调 |
| save/load | `microflow-interactions.spec.ts`; scripts | 编辑器级/脚本 | 部分 | 目标页 save/load E2E 缺 | 补 |
| call microflow/reference | `call-microflow-config.test.ts`; `microflow-references.test.ts` | 单元测试 | 部分 | 端到端缺 | 补 |
| publish | `scripts/verify-microflow-publish-version-references-testrun-debug-integration.ts` | 脚本 | 部分 | 非标准测试入口 | 纳入 CI |
| test-run/trace | `MicroflowRuntimeEngineTests.cs`; debug tests; scripts | 后端/前端单测和脚本 | 部分 | 目标页 E2E 缺 | 补 |
| API contract tests | `MicroflowBackend.http`; `MicroflowCreateHotfixTests.cs` | http 示例和部分单测 | 部分 | 全契约自动化不足 | 增 contract suite |
| backend integration tests | `tests/Atlas.AppHost.Tests/Microflows/*` | runtime/hotfix/action/transaction | 部分 | controller auth/workspace 集成不足 | 补 WebApplicationFactory |
| migration tests | 源码中未发现 EF migration；SqlSugar code-first 在 `AtlasOrmSchemaCatalog.cs` | code-first 注册 | 缺失 | schema 漂移风险 | 补 code-first smoke |

## 13. Release Blockers

| Blocker | 证据 | 影响 | 修复轮次 | 优先级 |
|---|---|---|---|---|
| App Explorer 完整 module tree 仍是 sample/static | `app-explorer.tsx`; `sample-app`; `store.ts` | Microflows 分组已真实只读，但目标页不是真实 app/module 资产树 | 后续 module tree 轮 | P0 |
| `appId` 未驱动 module/resource 查询 | `mendix-studio-route.tsx`; `app-explorer.tsx` | 多 app 错读错写 | 第 2 轮 | P0 |
| `moduleId` 来自 sample procurement module | `getCurrentExplorerModuleId`; `SAMPLE_PROCUREMENT_APP` | list 按 sample module 查询，不按真实模块 | 后续 module tree 轮 | P0 |
| CRUD 仍未完成 | `app-explorer-tree.tsx` | New/Rename/Duplicate/Delete 均 disabled，占位到 Stage 3 | 第 3 轮 | P0 |
| Workbench tab 已完成 Stage 04 基础生命周期，真实 editor host 未完成 | `app-explorer.tsx`; `workbench-tabs.tsx`; `index.tsx` | 点击真实微流打开 `microflow:{id}` tab 并显示 resource placeholder，不渲染真实 editor | 第 5 轮 | P1 |
| schema load/save 仍未完成本轮验收 | `index.tsx`; `http-resource-adapter.ts`; `editor-save-bridge.ts` | Stage 04 明确禁止目标页调用 GET/PUT schema | 第 5 轮 | P1 |
| Controller `[AllowAnonymous]` 且 workspace ownership 未发现 | `MicroflowResourceController.cs`; `MicroflowMetadataController.cs` | 越权风险 | 权限轮/第 2 前置 | P0 |
| Authorization/currentUser 未在目标 route 注入 | `microflow-adapter-config.ts`; `mendix-studio-route.tsx` | 审计/权限不完整 | 权限轮 | P0 |
| Workbench dirty close/refresh guard 与 schema save 已接通 | `workbench-tabs.tsx`; `store.ts`; `index.tsx`; `MicroflowResourceEditorHost.tsx` | 关闭/刷新有保护，真实保存成功会清 dirty；dirty 切换需确认丢弃 | conflict UX 轮 | P1 |
| 目标页 E2E 缺失 | 搜索结果源码中未发现 | 无发布回归基线 | 第 2 前/随第 2 | P1 |
| save gate/version conflict UX 不完整 | `editor/index.tsx`; `editor-save-bridge.ts`; `MicroflowResourceService.cs` | 409 能展示且 dirty 保持 true，但缺完整 conflict modal | Stage 14 | P1 |
| metadata 真实域模型来源仍不清，seed 默认 demo | `MicroflowMetadataSeedHostedService.cs`; `http-metadata-adapter.ts` | 目标页已注入真实 adapter，但 selector 数据可能非生产 | metadata 专项 | P1 |
| API 无版本前缀 | `MicroflowResourceController.cs`; `MicroflowMetadataController.cs` | 违反仓库 API 强约束 | API 整改轮 | P1 |

## 14. Recommended Next Round

Stage 05 已完成资源感知编辑器宿主，不受第 0 轮 Hotfix 是否完成约束。下一轮建议进入 metadata/toolbox/property 或 conflict UX 专项：继续补 Call Microflow 真实 metadata、Domain Model metadata、版本冲突处理、浏览器 E2E 与真实 app/module tree。publish/run/trace 与执行引擎仍需后续轮次单独验收。
