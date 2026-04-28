# Microflow Release Stage 05 - Resource Aware Editor Host

## 1. Scope

本轮完成 `/space/:workspaceId/mendix-studio/:appId` 中真实 microflow tab 的资源感知编辑器宿主：`microflow:{id}` tab 会按当前 `microflowId` 加载 resource 与 `MicroflowAuthoringSchema`，渲染 `MendixMicroflowEditorEntry`，并通过真实 HTTP adapter 保存到后端 schema API。

已完成范围：
- microflow tab 渲染真实 editor host。
- resource/schema load。
- real apiClient/save bridge。
- metadataAdapter/validationAdapter 注入。
- `PUT /api/microflows/{id}/schema` 保存。
- editor key remount。
- dirty state 联动。
- save success resource update。
- A/B schema isolation。
- sample schema isolation。

本轮不做：
- toolbox 发布化。
- property panel 深化。
- Call Microflow metadata 专项。
- publish/run/trace。
- execution engine。

## 2. Stage 0 Hotfix Status

第 0 轮 Hotfix 不阻塞本轮。按当前源码与 `docs/microflow-p1-release-gap.md` 记录，Create Microflow Hotfix 基本通过；`moduleId` 仍来自 sample Procurement module，属于真实 app/module tree 后续 blocker，不作为本轮启动阻塞。

| Hotfix 检查项 | 当前状态 | 是否阻塞本轮 | 后续处理 |
|---|---|---|---|
| 是否仍有 uncaught promise | 目标弹窗路径未发现；本轮另外修复 editor save 后刷新 promise catch | 否 | 继续用 E2E 观察 Console |
| 是否仍所有错误显示服务不可用 | 已按 401/403/409/422/500/network 区分 | 否 | 后续统一 action-aware 文案 |
| 是否仍默认 moduleId=sales | 目标页不是 sales，但仍来自 sample Procurement module | 否，记录 Release Blocker | 真实 app/module tree 轮次处理 |

## 3. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-studio-core/src/index.tsx` | 修改 | microflow tab 从 placeholder 切换为 `MicroflowResourceEditorHost`，保存后更新 store/tab |
| `src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer.tsx` | 修改 | 点击真实 microflow 节点打开 `microflow:{id}` tab |
| `src/frontend/packages/mendix/mendix-studio-core/src/components/workbench-tabs.tsx` | 修改 | dirty tab 切换前确认，避免静默丢失未保存草稿 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/adapter/microflow-resource-adapter.ts` | 修改 | 增加 `getMicroflowSchema(id)` 契约 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/adapter/http/http-resource-adapter.ts` | 修改 | 通过 `GET /api/microflows/{id}/schema` 加载 authoring schema |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/adapter/local-microflow-resource-adapter.ts` | 修改 | 补齐接口，仅供 local/mock 开发路径 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/editor/MendixMicroflowEditorEntry.tsx` | 修改 | 保存成功后的 resource refresh 增加 catch 与 unmount/race guard |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/resource/MicroflowResourceTab.tsx` | 修改 | failing adapter 补齐 schema method |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/studio/index.ts` | 修改 | 导出资源感知宿主 |
| `src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer.spec.tsx` | 修改 | 更新点击真实 microflow 后打开 workbench tab 的断言 |

新增文件：

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/studio/MicroflowResourceEditorHost.tsx` | 新增 | Stage 05 资源感知编辑器宿主 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/studio/__tests__/MicroflowResourceEditorHost.spec.tsx` | 新增 | 覆盖 resource/schema load、dirty/save 回调、404 不回退 sample |
| `docs/microflow-release-stage-05-resource-aware-editor-host.md` | 新增 | 本轮实现与验收记录 |

## 4. Editor Host Architecture

`MicroflowResourceEditorHost` 位于 `src/frontend/packages/mendix/mendix-studio-core/src/microflow/studio/MicroflowResourceEditorHost.tsx`。

宿主职责是校验 `microflowId`、校验 HTTP adapter bundle、并发加载 resource 与 schema、处理 loading/error/missing UI、渲染 `MendixMicroflowEditorEntry`、同步 dirty 和保存后的 resource 更新。它复用 `MendixMicroflowEditorEntry` 与 `editor-save-bridge.ts`，不复制平行编辑器。独立 `/microflow/:microflowId/editor` 路由仍保留 `MendixMicroflowEditorPage`，未被删除或改路由。

## 5. Load Flow

`Workbench tab microflow:{id}` -> `MicroflowResourceEditorHost` -> `resourceAdapter.getMicroflow(id)` -> `resourceAdapter.getMicroflowSchema(id)` -> `MicroflowAuthoringSchema` -> `MendixMicroflowEditorEntry` -> `MicroflowEditor key={microflowId:schemaId:version}`。

加载失败不会 fallback `sampleOrderProcessingMicroflow`。404 展示 `Microflow no longer exists`，401/403/409/422/500/network 通过 `MicroflowErrorState` 展示 code/status/traceId/retry。

## 6. Save Flow

`MicroflowEditor Save` -> `createMicroflowEditorApiClient` -> `resourceAdapter.saveMicroflowSchema(resource.id, schema, { baseVersion, saveReason })` -> `PUT /api/microflows/{id}/schema` -> save response -> `adapter.getMicroflow(id)` refresh -> `mapMicroflowResourceToStudioDefinitionView` -> `upsertStudioMicroflow` -> `updateMicroflowWorkbenchTabFromResource` -> `dirty=false`。

保存失败由 `MicroflowEditor` catch 并展示错误；`onSaveComplete` 不触发，因此 Workbench dirty 保持 true。保存后 refresh 失败也不会产生 uncaught promise，会把 dirty 标回 true 并显示错误。

## 7. Adapter Injection

- `apiClient`：由 `MendixMicroflowEditorEntry` 调用 `createMicroflowEditorApiClient(adapterBundle.resourceAdapter, resource, adapterBundle.runtimeAdapter)` 创建。
- `resourceAdapter`：来自目标页 HTTP `adapterBundle.resourceAdapter`。
- `metadataAdapter`：目标页显式传入 `adapterBundle.metadataAdapter`；缺失时宿主报错，不回退 mock metadata。
- `validationAdapter`：目标页显式传入 `adapterBundle.validationAdapter`；缺失时展示 warning，编辑器保留本地校验。
- local/mock 禁用：目标真实 microflow tab 要求 `adapterBundle.mode === "http"`，否则显示错误，不渲染真实保存路径。

## 8. Isolation Strategy

- editor key：`${microflowId}:${resource.schemaId}:${resource.version}`。
- request race protection：`requestSeqRef` + `mountedRef`，旧请求或 unmount 后返回不会覆盖当前 host。
- dirty per tab：宿主用当前 `activeMicroflowTabId` 调 `markWorkbenchTabDirty(tabId, dirty)`。
- A/B schema 不共享：切换 tab 会 remount host/editor，按 tab 的 `microflowId` 重新加载 schema。
- dirty 切换保护：当前 active tab dirty 时，切换前弹确认；确认丢弃才清 dirty 并切换。
- sample schema 隔离：目标 tab 不读取 `store.microflowSchema`，不传 `sampleOrderProcessingMicroflow`。

## 9. Error Handling

- load 404：显示 `Microflow no longer exists`，可重试或关闭 tab。
- load 401/403：显示登录失效/无权限。
- save 409：`MICROFLOW_VERSION_CONFLICT` 通过编辑器 toast/problems 呈现，dirty 保持 true。
- save 422：validation issues 写入 Problems，dirty 保持 true。
- save 500：显示服务异常，traceId 由 `MicroflowErrorState`/API error tag 呈现。
- network：显示服务不可用，可重试。

## 10. Verification

自动测试：
- `MicroflowResourceEditorHost` loads resource by `microflowId`。
- `MicroflowResourceEditorHost` loads schema by `microflowId`。
- host save callback marks dirty false and reports resource update。
- 404 missing state does not fallback sample schema。
- `AppExplorer` click real microflow opens `microflow:{id}` workbench tab。

手工验收建议：
- 打开目标页面，展开 Procurement / Microflows。
- 点击 `MF_ValidatePurchaseRequest`，确认 tab id 为 `microflow:{id}`。
- Network 观察 `GET /api/microflows/{id}` 与 `GET /api/microflows/{id}/schema`。
- 拖 Start/End、连线、保存，确认 `PUT /api/microflows/{id}/schema`。
- 刷新后重新打开同一微流，确认节点/连线恢复。
- 打开第二个微流，确认画布不显示第一个微流数据。
- 模拟 404/409/500，确认页面不白屏、不 fallback sample，并展示错误/traceId。
