# Microflow P1 Release Gap

## 1. Executive Summary

当前 P1 真实完成度：后端 Microflow resource/schema/metadata/validate/publish/references/test-run/run-trace API 已有较完整雏形，前端目标页 `/space/:workspaceId/mendix-studio/:appId` 已能创建 HTTP adapter bundle。Release Stage 02 已完成 App Explorer 中 Microflows 分组的真实只读列表：通过 `resourceAdapter.listMicroflows({ workspaceId, moduleId })` 加载，支持 loading/empty/error/retry/search/refresh，并将真实资源写入 `microflowResourcesById` 与 `microflowIdsByModuleId`。Release Stage 04 已完成 microflowId 驱动的 Workbench tab 文档生命周期。Release Stage 05 已把真实 microflow tab 从资源 placeholder 切换为 `MicroflowResourceEditorHost`：按 `microflowId` 加载 resource/schema，复用 `MendixMicroflowEditorEntry` 与 save bridge，保存走 `PUT /api/microflows/{id}/schema`，并更新 store/tab/dirty。Release Stage 06 已强化真实画布核心交互：拖拽创建、移动、删除清线、复制/粘贴、selection、viewport、dirty 与 per-editor undo/redo 均绑定当前 `MicroflowAuthoringSchema`。Release Stage 07 已强化 edge/branch 基础交互：FlowGram edge 与 `MicroflowSequenceFlow` 双向映射、`originObjectId/destinationObjectId` 与 `originConnectionIndex/destinationConnectionIndex` 持久化、Decision true/false `caseValues` 与 branch label 编辑、Edge Property Form、edge delete、节点删除相关 flow 清理及 A/B schema 隔离单测均已补齐。Release Stage 09 已完成属性面板基础节点闭环：no selection 显示 schema-bound document properties，Start/End/Parameter/Annotation/Decision/Merge/Edge 基础属性写回当前 active microflow schema，Parameter 属性同步 schema-level parameters，dirty 与保存刷新恢复沿既有真实 PUT 链路生效。Release Stage 11 已完成 Object Activity 与 List / Collection 节点基础建模能力：Object/List 表单接入真实 Domain Model metadata selector，Create/Retrieve/Change/Commit/Delete/Rollback Object 与 Create/Change/Aggregate/List Operation 配置写回当前 schema，Object/List output 进入当前微流 variable index，并补齐 stale metadata warning 与 A/B/C schema 隔离策略。Release Stage 12 已完成 Call Microflow 设计态互调基础能力：目标选择来自真实 metadata adapter/API，`targetMicroflowId` 稳定写回 schema，参数映射和返回值绑定写回当前微流 schema，保存沿 `PUT /api/microflows/{id}/schema` 触发后端 reference scanner 重建。Release Stage 13 已完成引用关系与影响分析基础能力：References Drawer 同时展示 callers/callees/impact summary，App Explorer 与 Workbench 可打开 View References，删除前执行 callers 预检查，DELETE 409 不移除树节点，target rename 使用 `targetMicroflowId` 稳定解析并提示 stale qualifiedName，source duplicate/delete/save 后刷新受影响 target `referenceCount`。Release Stage 14 已完成保存生命周期发布化基础：manual save、Ctrl/Cmd+S、per microflow dirty/save state、保存队列、autosave 开关与 debounce、last saved 信息、close/beforeunload guard、`baseVersion/schemaId/version` 请求携带、409 Conflict Modal 与 Reload Remote / Keep Local / Force Save 策略。第 0 轮 Create Microflow Hotfix 基本通过：弹窗已 catch `onSubmit` rejection，失败不关闭弹窗，loading 可恢复，并展示 status/code/message/traceId/fieldErrors。

Release Stage 16 已完成发布流程基础能力：目标页 active microflow tab 可打开 Publish Dialog，发布前调用真实 validate API，并在后端 publish 中再次执行 publish-mode validation；存在 blockPublish/error 时阻止发布并打开 Problems 入口，warning 仅提示不阻断。Publish Dialog 已展示 name/qualifiedName/status/publishStatus/draft version/latest published version/schemaId/dirty/version notes/impact summary，dirty=true 默认 Save & Publish。发布调用真实 `POST /api/microflows/{id}/publish`，成功后通过后端返回 resource 同步 Editor Header、Workbench tab、App Explorer resource index 与 `latestPublishedVersion`；保存 draft 后由后端状态转为 `changedAfterPublish`，再次 publish 回到 `published`。Version History 与 Rollback 使用真实 versions/rollback API，失败显示 status/code/traceId，不使用 fake history/localStorage/local adapter。Audit log 仍未发现独立后端能力，仅展示 `PublishedBy/PublishedAt`；runtime、run/trace、外部部署仍待后续轮次。

目标 `mendix-studio` 页面仍未达到完整发布化：Microflows 分组已真实化为只读列表，但 App Explorer 其他分组仍以 `SAMPLE_PROCUREMENT_APP` 静态数据为主，`moduleId` 来源仍是 sample procurement module；`appId` 没有驱动真实应用/模块资产树；Workbench Tabs 已具备 `microflow:{id}` 多文档 tab、`activeWorkbenchTabId`/`activeMicroflowId` 同步、per-tab dirty/save status、close guard 和 beforeunload guard；Stage 14/16 已将 schema save 与 publish lifecycle 推进到发布级基础能力，包含 manual save、autosave 策略、version conflict 基础能力、validate before publish、version notes、published snapshot、changedAfterPublish、version history 与 rollback。Stage 11 已完成 Domain Model metadata 基础接入和 Object/List selector/variable index；Stage 12 已完成 Call Microflow target selector、parameterMappings、return binding、reference rebuild 契约和 target rename 稳定性策略，但后端无 metadata cache 时仍可能返回 seed catalog，真实 Domain Model 编辑器、Call Microflow runtime executor、runtime full cycle detection / run/trace 仍待后续轮次。权限仍主要依赖 header/context 和生产 guard，Controller 标注 `[AllowAnonymous]`，未发现 workspace ownership 校验。

最大 blocker：目标页仍不是真实 app/module 资产树，`appId` 未进入 module tree 查询维度；真实 Domain Model/Call Microflow 元数据来源仍依赖 metadata cache/seed，Call Microflow 运行时执行器、publish/run/trace、执行引擎仍未完成，且后端 API 无版本前缀、权限/租户隔离不足。Stage 0 Hotfix 若未来发现回归，作为单独 Release Blocker 记录，但不阻塞 Stage 12 Call Microflow 设计态互调。

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

### Stage 14 Save Lifecycle Update

| 能力 | 当前状态 | 源码路径 | 说明 | 后续缺口 |
|---|---|---|---|---|
| manual save | 已进入发布级生命周期基础 | `MendixMicroflowEditorEntry.tsx`; `editor-save-bridge.ts`; `http-resource-adapter.ts` | Save/Ctrl+S 只保存当前 active microflowId，走真实 `PUT /api/microflows/{id}/schema` | 仍需 E2E 网络断言 |
| autosave | 已完成可配置策略 | `MendixMicroflowEditorEntry.tsx` | 默认关闭，toolbar 开关，4s debounce，失败可见，conflict 停止 | 后续可接用户偏好持久化 |
| conflict handling | 已完成基础能力 | `MendixMicroflowEditorEntry.tsx`; `MicroflowResourceService.cs` | 409 打开 Conflict Modal，支持 Reload Remote / Keep Local / Force Save | 后端 remote updatedBy/updatedAt payload 与 full diff 待补 |
| dirty/guard | 已强化 | `store.ts`; `workbench-tabs.tsx`; `index.tsx` | per microflow dirty/save state，close guard Save/Discard/Cancel，beforeunload 覆盖 dirty/saving/queued | 跨 tab draft cache 未做，dirty 切换选择阻止 |
| schema load/save | 已进入发布级生命周期 | `MicroflowResourceEditorHost.tsx`; `MendixMicroflowEditorEntry.tsx` | 保存成功同步 resource/tab/tree，失败 rollback，delete 清理 save state | publish 仍待后续轮次 |
| publish | 未纳入本轮 | `PublishMicroflowModal.tsx` | 本轮没有实现 publish 发布化 | 后续轮次处理 |

### Stage 17 Run / Debug Update

| 能力 | 当前状态 | 源码路径 | 说明 | 后续缺口 |
|---|---|---|---|---|
| Run / Test Run | 已完成基础闭环 | `mendix-microflow/src/editor/index.tsx`; `debug/MicroflowTestRunModal.tsx`; `mendix-studio-core/src/microflow/adapter/http/http-runtime-adapter.ts` | 当前 active microflow tab 可打开 Run Panel，dirty 时 Save & Run，调用真实 `POST /api/microflows/{id}/test-run` | 浏览器 E2E 仍需补齐 |
| 参数输入模型 | 已完成基础能力 | `debug/run-input-model.ts`; `MicroflowTestRunModal.tsx` | 从 schema-level parameters 生成输入控件，支持 required/default/type conversion，按 microflowId 隔离输入 | Object/List 仅 JSON 输入，不做真实对象选择器 |
| Runtime MVP | 已完成本轮范围 | `Atlas.Application.Microflows/Runtime/MicroflowRuntimeEngine.cs`; `MicroflowTestRunService.cs` | 支持 Start/End/Parameter binding/Create Variable/Change Variable/Decision/Merge/Unsupported，按 sequence flow 执行 | 不是完整 Mendix Runtime |
| Call Microflow runtime | 已完成基础能力 | `MicroflowRuntimeEngine.cs` | 加载 target schema、参数映射、child context、返回绑定、callStack、self-call/A-B-A/max depth guard | 仅支持 sync call，复杂事务/权限 runtime 后续 |
| Trace Panel / nodeResults | 已完成基础能力 | `debug/MicroflowTracePanel.tsx`; `trace-history-utils.ts` | 展示 runId/status/duration/output/error/nodeResults/logs/callStack，childRuns 按 callDepth 展示 | Open child microflow 深度 UX 后续增强 |
| Canvas highlight | 已完成基础能力 | `flowgram/adapters/authoring-to-flowgram.ts`; `flowgram/styles/flowgram-microflow-node.css`; `FlowGramMicroflowCanvas.tsx` | trace 仅作为运行态投影，不写 schema；支持 success/failed/unsupported/skipped/running 高亮，Clear Trace 清空 | 边高亮依赖 trace flowId |
| Unsupported node | 已完成真实失败 | `MicroflowRuntimeEngine.cs`; `MicroflowRuntimeDtos.cs` | 未支持节点返回 `RUNTIME_UNSUPPORTED_ACTION`，前后端 status 归一为 unsupported/failed，不显示假成功 | Loop/List/Object/REST runtime 后续轮次 |
| Run history | 已有后端持久化最小能力 | `MicroflowTestRunService.cs`; `MicroflowRunRepository` | 支持 list/detail/trace，Trace Panel 可加载 history | 未做完整生产级 run history 页面 |

## 7. Canvas / Toolbox / Property Panel

### Canvas

| 能力 | 源码路径 | 当前实现 | 是否进入 authoring schema | 是否保存刷新恢复 | 缺口 | 建议 |
|---|---|---|---|---|---|---|
| drag node | `FlowGramMicroflowCanvas.tsx`; `editor/index.tsx`; `node-registry/drag-drop.ts` | DnD payload 含 registry key/type/actionKind -> registry 反查 -> factory -> `addMicroflowObjectFromDragPayload` -> 当前 active schema objectCollection | 是 | 是，随 schema PUT | 需浏览器 E2E | Stage 08 已补 schema/helper 单测 |
| click add node | `node-panel/index.tsx`; `editor/index.tsx` | 节点卡片单击/context add，使用当前 viewport 安全中心点并偏移 | 是 | 是 | 需浏览器 E2E | Stage 08 发布化完成 |
| node position | `useFlowGramMicroflowBridge.ts`; `flowgram-to-authoring-patch.ts` | `flowGramPositionPatch` -> `moveObject` -> `relativeMiddlePoint` | 是 | 是 | 快速移动需手工验收 | Stage 06 已补单测 |
| node delete | `editor/index.tsx`; `authoring-operations.ts`; `useFlowGramMicroflowBridge.ts` | Delete/Backspace / 属性面板 / FlowGram 原生节点删除 -> `deleteObject`，同步清理 root/nested flows 与 selection | 是 | 是 | 无确认；E2E 待补 | Stage 07 已补 FlowGram node delete 同步与清线测试 |
| node duplicate | `editor/index.tsx`; `authoring-operations.ts` | `duplicateObject` 生成新 id、偏移位置、Copy caption，不复制 flows | 是 | 是 | 多选 duplicate 未确认 | Stage 06 已补单测 |
| multiselect | `FlowGramMicroflowCanvas.tsx`; `useFlowGramMicroflowBridge.ts` | 选择服务仅同步单 selection | 部分 | 部分 | 多选操作未完整发现 | 后续实现 |
| copy/paste | `editor/index.tsx`; `editor/shortcuts/useMicroflowShortcuts.ts` | Ctrl/Cmd+C 记录当前微流单节点；Ctrl/Cmd+V 在同一微流调用 `duplicateObject`，跨微流粘贴禁用 | 是 | 是 | 多选/跨微流后续定义 | Stage 06 已完成基础能力 |
| edge create/delete | `useFlowGramMicroflowBridge.ts`; `flowgram-edge-factory.ts`; `flowgram-edge-mapping.ts`; `authoring-operations.ts` | detect new/deleted edge -> add/delete flow，写入真实 `schema.flows`/nested `objectCollection.flows`，新 flow id 当前 schema 内唯一 | 是 | 是 | 浏览器 E2E 待补 | Stage 07 已补 FlowGram/schema 映射 helper 与相关单测 |
| source/target/handle | `flowgram-edge-mapping.ts`; `flowgram-edge-factory.ts`; `port-utils.ts` | `sourcePortID/targetPortID` 映射为 `originConnectionIndex/destinationConnectionIndex`，schema 保存 `originObjectId/destinationObjectId` | 是 | 是 | handle 本身是投影字段，不独立落库 | Stage 07 已补 handle/index 映射测试 |
| Decision true/false branch | `registry.ts`; `FlowGramMicroflowCaseEditor`; `flow-edge-form.tsx`; `flowgram-case-options.ts` | true/false ports + `caseValues` 持久化；branch label 写 `editor.label`；重复 case 阻止/校验 | 是 | 是 | 交互 E2E 缺失；无独立 `conditionKey` 字段 | Stage 07 已补 true/false/case 基础持久化测试 |
| viewport persistence | `editor/index.tsx` | `onViewportChange` 写 `schema.editor.viewport/zoom` 并标 dirty | 是 | 是，保存后恢复 | pan 事件覆盖需手工验收 | Stage 06 已定义持久化 |
| undo/redo | `history` + `editor/index.tsx` | 每个 editor instance 持有独立 `MicroflowHistoryManager`，key 含 microflowId/schemaId/version | 是 | schema 恢复；history 不持久 | Workbench 顶部按钮仍未接 editor state | Stage 06 已确认隔离 |
| auto layout | `layout`; `editor/index.tsx` | `applyAutoLayout` | 是 | 是 | 无后端布局校验 | 保留 |
| minimap/grid | `FlowGramMicroflowCanvas.tsx`; `editor/index.tsx` | `showMiniMap/gridEnabled` 写 schema.editor | 是 | 是 | 需保存 | 保留 |

### Toolbox

Stage 08 已完成 Toolbox 发布化：分类稳定为 Events / Inputs / Flow Control / Loops / Variables / Objects / Lists / Integration / Documentation；搜索支持 label/description/category/keywords/tags/type/actionKind 且 200ms debounce；drag/click add 均写入当前 active microflow authoring schema；生产默认配置已清理 Sales/MF_ValidateOrder/Order/Customer/Product/ProcessOrder/CheckInventory/NotifyUser/localhost 等 mock/demo 值。

| 节点类型 | 注册路径 | category | 默认配置 | 是否含 mock/demo | 是否可拖拽 | 是否可点击添加 | 缺口 |
|---|---|---|---|---|---|---|---|
| Start | `node-registry/registry.ts` | Events | `eventEntry("startEvent")` | 否 | 是 | 是 | 无 |
| End | `registry.ts` | Events | void return / empty return expression | 否 | 是 | 是 | 无 |
| Parameter | `registry.ts` | Inputs | registry name 空；添加时生成安全 `parameter` | 否 | 是 | 是 | 参数 UX 后续 |
| Decision / If | `registry.ts` | Flow Control | empty expression Boolean | 否 | 是 | 是 | case UX E2E |
| Merge | `registry.ts` | Flow Control | `strategy:firstArrived` | 否 | 是 | 是 | 无 |
| Loop | `registry.ts` | Loops | empty list/iterator, forEach shell | 否 | 是 | 是 | loop body E2E |
| Break / Continue | `registry.ts` | Loops | no fake targetLoopId | 否 | 是，warning | 是，warning | 合法性留 validation |
| Create/Change Variable | `action-registry.ts` | Variables | empty expression, safe variable default | 否 | 是 | 是 | 表达式专项 |
| Object actions | `action-registry.ts` | Objects | entity/object/list target empty | 否 | 是 | 是 | Stage 11 已接真实 Domain Model selector 与基础表单 |
| List actions | `action-registry.ts` | Lists / Collections | entity/list/source/target empty | 否 | 是 | 是 | Stage 11 已接真实 Domain Model selector 与基础表单 |
| Call Microflow | `action-registry.ts` | Integration | target id/name/qualifiedName empty | 否 | 是，真实 metadata selector | 是，真实 metadata selector | runtime executor 后续 |
| REST Call | `action-registry.ts` | Integration | method GET, url empty | 否 | 是 | 是 | runtime/policy 后续 |
| Annotation | `registry.ts` | Documentation | text empty | 否 | 是 | 是 | 无 |

Registry metadata 状态：每项具备 `id/key/type/kind/actionKind/category/label/description/iconKey/keywords/createDefaultConfig/metadataRequirements/featureStatus`；unsupported/preview 节点不伪造后端不支持的 actionKind。

Disabled/warning 状态：无 active microflow、schema loading、readonly、unsupported 均显示 disabled reason；Object/List/Call Microflow metadata 缺失显示 warning 不阻止添加；Break/Continue 显示 Loop context warning。

仍待后续轮次：Variable actions 深度表单、Loop/Break/Continue 深度表单、Call Microflow runtime executor、完整 Domain Model 编辑器、Validation/Problems 专项、publish/run/trace 与执行引擎。Object/List 基础建模与 Domain Model metadata selector 已在 Stage 11 完成，Call Microflow 设计态互调已在 Stage 12 完成。详见 `docs/microflow-release-stage-08-toolbox-productionization.md`、`docs/microflow-release-stage-09-property-panel-basic-nodes.md`、`docs/microflow-release-stage-11-object-list-nodes.md` 与 `docs/microflow-release-stage-12-call-microflow-design-time.md`。

### Property Panel

| 表单 | 源码路径 | 当前支持字段 | 是否写回 schema | 是否 dirty | 是否保存恢复 | 缺口 |
|---|---|---|---|---|---|---|
| document properties | `property-panel/index.tsx`; `forms/microflow-document-properties-form.tsx` | no selection 显示 id/name/displayName/qualifiedName/schemaVersion/returnType/description/documentation/parameters/audit | 是，documentation | 是 | 是 | resource-only `referenceCount/publishStatus/schemaId` 未注入 property panel，本轮只读说明 |
| node base properties | `property-panel/forms/object-base-form.tsx`; `object-panel.tsx` | id/kind/caption/description/position/disabled | 是 | 是 | 是 | 空 caption 仅 warning，不自动修复 |
| edge properties | `forms/flow-edge-form.tsx` | flow id、origin/destination、connection indexes、label、description、case/error、Delete edge 等 | 是 | 是 | 是 | 深度 routing 与完整 Problems panel 后续 |
| Start | `event-nodes-form.tsx`; `object-base-form.tsx` | caption/description/trigger/outgoing summary | 是 | 是 | 是 | 仅基础 warning |
| End | `event-nodes-form.tsx`; `schema/utils/microflow-signature.ts` | caption/description/returnType/returnValueExpression/incoming summary | 是 | 是 | 是 | 不执行表达式 |
| Parameter | `parameter-object-form.tsx`; `property-panel/utils/schema-patch.ts`; `schema/utils/microflow-signature.ts` | name/type/required/defaultValue/exampleValue/description | 是，且同步 `schema.parameters` 与 Parameter object | 是 | 是 | 历史损坏 schema repair/migration 不做 |
| Annotation | `annotation-object-form.tsx` | text/colorToken/pinned/exportToDocumentation + base fields | 是 | 是 | 是 | 完整样式系统后续 |
| Decision/Merge | `exclusive-split-form.tsx`; `merge-node-form.tsx`; `decision-merge.ts` | expression/decisionType/resultType/branch summary；merge strategy/in-out summary | 是 | 是 | 是 | metadata stale 与 expression engine 后续 |
| Variable actions | `action-activity-form.tsx`; `generic-action-fields-form.tsx` | create/change variable fields | 是 | 是 | 是 | 本轮不做深度专项，后续处理 |
| Object actions | `action-activity-form.tsx`; selectors | entity/member/object variable | 是 | 是 | 是 | Stage 11 已接真实 Domain Model metadata；runtime 执行器后续 |
| List actions | `action-activity-form.tsx`; `generic-action-fields-form.tsx` | list variable/entity/operation | 是 | 是 | 是 | Stage 11 已接真实 Domain Model metadata；集合 runtime 后续 |
| Loop/Break/Continue | `loop-node-form.tsx`; `event-nodes-form.tsx` | loop config/event | 是 | 是 | 是 | 本轮不做深度专项 |
| Call Microflow | `action-activity-form.tsx`; `MicroflowSelector.tsx`; `call-microflow-config.ts` | 真实 metadata target selector、`targetMicroflowId` 稳定保存、display/qualifiedName 快照、parameterMappings、return binding、self-call warning | 是 | 是 | 是 | runtime executor、完整 cycle detection 后续 |
| REST Call | `generic-action-fields-form.tsx`; `action-activity-form.tsx` | URL/method/body/response/error | 是 | 是 | 是 | runtime 支持受限 |
| expression editor | `property-panel/expression` | expression raw/text/references | 是 | 是 | 是 | 非完整 Mendix expression IDE |
| metadata selectors | `selectors/*`; `metadata-provider.tsx` | entity/enumeration/microflow/page/workflow refs | 是 | 是 | 是 | 缺失 adapter 时不 fallback，但错误 UX 简单 |

### Release Stage 11 Object / List Nodes

| 能力 | 源码路径 | 当前状态 | 仍待后续 |
|---|---|---|---|
| Domain Model metadata adapter | `mendix-studio-core/src/microflow/metadata/http-metadata-adapter.ts`; `mendix-microflow/src/metadata/metadata-provider.tsx` | 目标页通过 `adapterBundle.metadataAdapter` 调 `GET /api/microflow-metadata`，传 `workspaceId/moduleId`，失败显示 error/retry，不 fallback mock | metadata cache 为空时后端 seed 来源仍需后续接真实 Domain Model 编辑器 |
| Entity selector | `property-panel/selectors/EntitySelector.tsx` | 支持真实 catalog entity 搜索、loading/error/empty/retry，不显示 mock catalog | module tree 真实化后再补跨模块 UX |
| Attribute/member selector | `AttributeSelector.tsx`; `action-activity-form.tsx` | Create/Change Object memberChanges 使用当前 entity attributes，保存到 schema，stale member warning | 复杂类型匹配留后续 Problems |
| Association selector | `AssociationSelector.tsx` | Retrieve association 与 member association 修改使用 catalog associations | 后端无 association 单查接口 |
| Enumeration selector | `EnumerationSelector.tsx` | enum attribute value 从 catalog enumeration values 选择，不生成 fake values | enum stale value 目前以保留表达式为主 |
| Object actions | `action-activity-form.tsx`; `variables/object-activity-foundation.ts`; `variables/variable-index.ts` | Create/Retrieve/Change/Commit/Delete/Rollback 基础字段、selector、output variable index、stale warning 已接入 | 不做真实 CRUD runtime |
| List / Collection actions | `action-activity-form.tsx`; `variables/list-collection-foundation.ts`; `variables/variable-index.ts` | Create/Change/Aggregate/List Operation 基础字段、selector、result/output index 已接入；List Operation 支持 right list 与 take/skip | 不做表达式执行与复杂集合 runtime |
| Object/List variable index | `variables/variable-index.ts` | Parameters、Create Variable、Create Object、Retrieve、Create List、Aggregate、List Operation、Loop 等输出按当前 schema 重建，A/B/C 不共享全局数组 | Workbench dirty save UX 仍可继续增强 |
| Runtime / Problems / publish/run/trace | 多处 | 本轮未实现 Object/List executor、真实数据库 CRUD、publish/run/trace | 后续轮次 |

Stage 09 状态：Property Panel 基础节点已达到发布版本基础闭环。Start/End/Parameter/Annotation/Decision/Merge 的基础属性修改均写回当前 active `MicroflowAuthoringSchema`，通过 `commitSchema` 触发当前 tab dirty，并随 `PUT /api/microflows/{id}/schema` 保存恢复。Parameter 与 schema-level parameters 已做最小同步；Object/List metadata selector 与基础表单已由 Stage 11 补齐；Variable/Loop/CallMicroflow 深度表单、完整 Problems、publish/run/trace 仍待后续轮次。

## 8. Metadata

| Metadata 能力 | 源码路径/API | 当前实现 | 是否 mock | 是否接入目标页面 | 缺口 | 建议 |
|---|---|---|---|---|---|---|
| provider 默认 mock | `mendix-microflow/src/metadata/metadata-provider.tsx` | 缺 adapter 报错，不 fallback mock | 否 | 是 | 需要真实 adapter | 保留 |
| 目标页注入真实 adapter | `StudioEmbeddedMicroflowEditor.tsx`; `microflow-adapter-factory.ts` | http bundle `metadataAdapter` 注入 | 否 | 是 | mode 可配置 local/mock | 生产锁 http |
| target list API | `http-metadata-adapter.ts` | GET `/api/microflow-metadata/microflows`，传 `workspaceId/moduleId/keyword/status` | 否 | 是 | 无 appId | 加 app/module 维度 |
| entities/enumerations API | `http-metadata-adapter.ts`; `MicroflowMetadataController.cs` | GET `/api/microflow-metadata` 和子资源 | 否 | 是 | seed 默认 demo-workspace | 真实域模型接入 |
| Sales.* mock | `mock-metadata.ts` | Sales/Inventory/System mock catalog 存在 | 是 | http 目标页否 | local/mock 模式可能出现 | 禁生产 mock |
| loading/error/empty | `metadata-provider.tsx`; selectors | context 暴露 loading/error；selector 显示错误 | 否 | 是 | empty UX 分散 | 统一状态 |
| cache by workspace/module | `metadata-provider.tsx`; backend metadata repository | request 含 workspace/module；Provider 和 selector 已有请求序号，避免乱序污染当前 form | 否 | 是 | 前端无持久化 stale TTL | 加失效策略 |
| stale 处理 | `validators/validate-actions.ts` | stale qualifiedName warning | 否 | 是 | cache stale 未自动刷新 | 保存/发布前 refresh |
| Call Microflow 主键 | `action-activity-form.tsx`; `call-microflow-config.ts` | `targetMicroflowId` 为主，qualifiedName/displayName 只作快照 | 否 | 是 | 无目标 app 限定 | 保留 ID 主键 |
| qualifiedName 快照 | `action-activity-form.tsx`; `validators/validate-actions.ts` | 展示/校验 stale warning；保存可刷新快照，不清空 target | 否 | 是 | rename 后仍需手工验收 | 引用刷新 |

## 9. Validation / Problems

| 校验能力 | 源码路径/API | 当前实现 | 是否阻止保存/发布 | 是否可定位 | 缺口 | 建议 |
|---|---|---|---|---|---|---|
| 本地 validation | `mendix-microflow/src/validators/*`; `editor/index.tsx` | `validateMicroflowSchema` | 保存前不强阻止；test-run 会 gate | 是，problem click 定位 | save gate 不明确 | 第 5 轮定义 save gate |
| 后端 validate API | `MicroflowResourceController.cs`; `MicroflowValidationService.cs` | POST `/api/microflows/{id}/validate` | publish/test-run 使用 | 返回 issue 字段 | 无 auth | 权限补齐 |
| Problems panel | `editor/index.tsx` | bottom problems tab；edge/case validation issue 可展示定位 | 部分阻止 | 是 | 与 studio bottom panel 分离；不是完整 Problems panel | 后续轮次统一 UX，Stage 07 不实现 full Problems panel |
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
| callers/callees | `MicroflowResourceController.cs`; `MicroflowReferenceService.cs`; `MicroflowReferencesDrawer.tsx`; `microflow-reference-utils.ts` | GET references 为 callers 语义；源码中未发现 GET callees，本轮从当前 source schema 解析 Call Microflow | 是 | callees API 仍缺；schema parse 只覆盖设计态 | 后续可补 `/callees` API，但不得 mock |
| delete protect | `app-explorer.tsx`; `MicroflowResourceTab.tsx`; `MicroflowResourceService.DeleteAsync` | 前端删除前查 callers，失败/active callers 阻断；后端 409 是最终保护 | 是 | 需集成/E2E 测试 | 保留 |
| referenceCount | `MicroflowResourceDto`; `app-explorer-tree.tsx`; `MicroflowReferenceIndexer`; `MicroflowResourceService.DeleteAsync` | 只展示 DTO 值；save/duplicate/delete source 后刷新受影响 target count | 是 | 历史旧数据需 rebuild；不做前端伪造 | 手动 rebuild 或后续后台修复 |
| rename stability | `action-activity-form.tsx`; `MicroflowReferencesDrawer`; `microflow-reference-utils.ts` | ID 主键，qualifiedName 显示快照；rename 后显示最新 target 名称或 stale qualifiedName warning | 是 | 需浏览器手工验收 | 保留 |
| duplicate/delete source reference refresh | `MicroflowResourceService.DuplicateAsync`; `MicroflowResourceService.DeleteAsync`; `MicroflowReferenceIndexer` | duplicate 重建新 source 出向 references；delete source 删除出向 references 并刷新 target callers/count | 是 | 需集成测试 | 保留 |

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
| call microflow/reference | `call-microflow-config.test.ts`; `microflow-references.test.ts` | target 写入/清空、同名 mapping 保留、return binding、reference scanner 单元覆盖 | 部分 | 浏览器端到端缺 | 补真实路线 E2E |
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
| CRUD 仍未完整完成 | `app-explorer-tree.tsx`; `app-explorer.tsx` | Delete 已接 callers precheck 与后端 409 保护；New/Rename/Duplicate 在 App Explorer 仍未完整接入 | 后续 CRUD 轮 | P0 |
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

Stage 11 已完成 Object/List 基础建模，不受第 0 轮 Hotfix 是否完成约束。下一轮建议进入真实 app/module tree、Call Microflow 真实 metadata、版本冲突处理、浏览器 E2E 或 runtime/Problems 专项。publish/run/trace 与执行引擎仍需后续轮次单独验收。
