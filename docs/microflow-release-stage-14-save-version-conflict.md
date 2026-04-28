# Microflow Release Stage 14 - Save, Autosave & Version Conflict

## 1. Scope

本轮完成 `/space/:workspaceId/mendix-studio/:appId` 目标页的 Microflow 保存生命周期发布化基础能力：

- manual save、Ctrl/Cmd+S、save status machine、per microflow dirty/save state。
- per microflow save queue、防重复提交、autosave 配置开关与 4s debounce。
- `baseVersion` / `schemaId` / `version` 保存请求携带，后端最小支持 `force` 覆盖。
- 409 conflict 识别、Conflict Modal、Reload Remote / Keep Local / Force Save。
- 保存成功同步 resource / Workbench tab / App Explorer store，失败保留 dirty。
- lastSavedAt / lastSavedBy / lastSaveDurationMs、close / beforeunload guard、optimistic update rollback、A/B/C 保存隔离。

本轮不做：publish 流程、完整 Problems 面板专项、run/trace、实时协作、完整 schema diff merge、执行引擎。

依赖缺口：后端没有完整 remote conflict payload、没有完整 diff/merge、目标页仍未真实化 app/module 资产树。  
本轮最小补齐点：Save DTO 增加 `schemaId` / `version` / `clientRequestId` / `force`，保存成功更新 `ConcurrencyStamp`，前端只展示可用的最小冲突信息。

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-studio-core/src/store.ts` | 修改 | 新增 per microflow save state、dirty/saving/error/conflict map 与清理动作 |
| `src/frontend/packages/mendix/mendix-studio-core/src/index.tsx` | 修改 | beforeunload 覆盖 dirty/saving/queued，dirty 写入 microflow save state |
| `src/frontend/packages/mendix/mendix-studio-core/src/components/workbench-tabs.tsx` | 修改 | dirty tab 关闭 Save/Discard/Cancel，切换 dirty tab 不再丢草稿 |
| `src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer.tsx` | 修改 | 删除 dirty 微流前提示本地更改会丢失 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/editor/MendixMicroflowEditorEntry.tsx` | 修改 | 保存队列、autosave、Conflict Modal、last saved UI、保存事件桥接 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/editor/editor-save-bridge.ts` | 修改 | 支持宿主注入真实保存队列，默认 PUT 携带版本字段 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/adapter/microflow-resource-adapter.ts` | 修改 | Save options 增加 `schemaId` / `version` / `clientRequestId` / `force` |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/contracts/api/microflow-schema-api-contract.ts` | 修改 | 前端 Save DTO 契约补齐 |
| `src/frontend/packages/mendix/mendix-studio-core/src/workbench-tabs-lifecycle.spec.ts` | 修改 | 增加保存状态隔离、删除清理、rename 保留测试 |
| `src/frontend/packages/mendix/mendix-microflow/src/editor/index.tsx` | 修改 | Save disabled/no-op、保存竞态 dirty 修正 |
| `src/backend/Atlas.Application.Microflows/Models/MicroflowResourceApiDtos.cs` | 修改 | Save request 增加版本、clientRequestId、force 字段 |
| `src/backend/Atlas.Application.Microflows/Services/MicroflowResourceService.cs` | 修改 | force 跳过冲突校验，保存更新并发令牌 |
| `src/backend/Atlas.AppHost/Microflows/Controllers/MicroflowResourceController.cs` | 修改 | SaveSchema 标注 409/422 response |
| `docs/microflow-p1-release-gap.md` | 修改 | 标记 Stage 14 保存生命周期进展 |

## 3. Current Save Chain Audit

| 环节 | 源码路径 | 函数/组件 | 当前实现 | 是否真实后端 | 是否携带版本 | 是否处理错误 | 缺口 | 本轮处理 |
|---|---|---|---|---|---|---|---|---|
| MicroflowEditor Save 按钮 | `mendix-microflow/src/editor/index.tsx` | `handleSave` / `saveCurrentSchema` | 调 `apiClient.saveMicroflow` | 是，经宿主注入 | 是，经 bridge/queue | 是 | dirty=false 仍可点 | 已禁用且 no-op |
| Editor keyboard shortcut | `useMicroflowShortcuts.ts` | Ctrl/Cmd+S | preventDefault 后 `onSave` | 是 | 是 | 是 | 依赖 active editor | 保留当前 active editor |
| onSchemaChange | `MendixMicroflowEditorEntry.tsx` | `onSchemaChange` | 写本地 schema + dirty | 不请求 | N/A | N/A | 保存中编辑竞态 | 标记 queued |
| dirty state | `store.ts` | `markMicroflowDirty` | per microflow/tab | N/A | N/A | N/A | 原仅 tab map | 新增 save state |
| editor-save-bridge | `editor-save-bridge.ts` | `saveMicroflow` | 调 adapter 保存 | 是 | 是 | 抛给调用方 | 无队列 | 支持宿主队列 |
| resourceAdapter.saveMicroflowSchema | `http-resource-adapter.ts` | `saveMicroflowSchema` | PUT `/api/microflows/{id}/schema` | 是 | 是 | HTTP client 统一异常 | 无 force 字段 | DTO 扩展 |
| HTTP PUT | `microflow-api-client.ts` | `put` | fetch envelope | 是 | body 携带 | 是 | 无幂等持久层 | 仅 clientRequestId 透传 |
| Save DTO | `MicroflowResourceApiDtos.cs` | `SaveMicroflowSchemaRequestDto` | schema/baseVersion/saveReason | 是 | 部分 | N/A | 无 schemaId/version/force | 已补 |
| Save response DTO | 同上 | `SaveMicroflowSchemaResponseDto` | resource/schemaVersion/updatedAt | 是 | resource 内有 | N/A | remote conflict payload 不完整 | 文档记录 |
| baseVersion/schemaId/version | 前后端 DTO | 多处 | baseVersion 已有 | 是 | 部分 | 409 | 无完整 source token | schemaId/version 最小补齐 |
| conflict 409 | `MicroflowResourceService.cs` | `SaveSchemaAsync` | baseVersion 不匹配 409 | 是 | 是 | 是 | 无弹窗 | 已接 Modal |
| error mapping | `microflow-api-error.ts` | `getMicroflowApiError` | 区分 401/403/404/409/422/5xx | N/A | N/A | 是 | 409 普通 toast | 改为 conflict 状态 |
| Workbench dirty | `store.ts` | `dirtyByWorkbenchTabId` | per tab | N/A | N/A | N/A | 与 save status 分离 | 双写 |
| App Explorer update | `index.tsx` | `onResourceUpdated` | upsert resource + tab | N/A | N/A | N/A | 保存后不刷新列表 | 成功触发 refresh token |
| close guard | `workbench-tabs.tsx` | Modal | Save disabled | N/A | N/A | 部分 | 不能 Save | 已接保存事件 |
| beforeunload guard | `index.tsx` | effect | dirty 时提示 | N/A | N/A | N/A | saving/queued 未覆盖 | 已覆盖 |
| local adapter fallback risk | `MicroflowResourceEditorHost.tsx` | mode guard | 非 http 报错 | 否，不保存 | N/A | 是 | 独立页仍有风险 | 文档记录 |
| sampleOrderProcessingMicroflow risk | `MicroflowResourceEditorHost.tsx` | load guard | 不回退 sample | 否 | N/A | 是 | store 仍有 sample app | 目标页保存不使用 |

## 4. Save State Model

`MicroflowSaveStatus` 包含 `idle`、`dirty`、`saving`、`saved`、`error`、`conflict`、`autosaving`、`queued`。`MicroflowSaveState` 按 `microflowId` 存储 `dirty`、`saving`、`queued`、`lastSavedAt`、`lastSavedBy`、`lastSaveDurationMs`、`lastError`、`conflict`、`schemaId`、`baseVersion`、`localVersion`、`remoteVersion`。

Store 同时维护 `saveStateByMicroflowId`、`savingByMicroflowId`、`saveErrorByMicroflowId`、`saveConflictByMicroflowId` 与 `dirtyByWorkbenchTabId`。删除微流清理状态；rename 因 id 不变保留；duplicate 因新 id 不复制状态。

## 5. Manual Save Flow

User Save / Ctrl+S -> active `MicroflowEditor` -> `apiClient.saveMicroflow({ schema })` -> `MendixMicroflowEditorEntry.saveLatestSchema("manual")` -> per microflow queue -> `resourceAdapter.saveMicroflowSchema(microflowId, schema, options)` -> `PUT /api/microflows/{id}/schema` -> response resource -> update Entry resource/schema -> `onResourceUpdated` upsert store/tab/tree -> dirty=false。

保存失败时不更新 `schemaId/version`，本地 schema 保留，dirty=true，UI 显示错误或冲突弹窗。

## 6. Autosave Strategy

默认关闭，编辑器 toolbar 提供 `Autosave` 开关。开启后 schema 变更 debounce 4000ms，仅 dirty 且非 readonly 时触发。保存中继续编辑会标记 queued；conflict 时暂停等待用户处理。自动保存失败保留 dirty 并显示非阻塞错误，不使用 localStorage，不会每个 keypress 请求后端。

## 7. Version / Conflict Strategy

`baseVersion` 来源优先为当前 resource `schemaId`，回退 `version`。保存请求同时携带 `schemaId`、`version`、`saveReason`、`clientRequestId`，force save 时带 `force=true` 并不传 baseVersion。后端已有 baseVersion 409，本轮最小补齐 force 与 `ConcurrencyStamp` 更新。

409 或 `MICROFLOW_VERSION_CONFLICT` 设置 `status=conflict` 并打开 Conflict Modal。Reload Remote 重新 GET resource/schema 并替换本地；Keep Local 清 conflict 但保持 dirty；Force Save 二次确认后带 `force=true` 覆盖远端；Cancel 保持 conflict。

## 8. Error Mapping

| status/code | UI 行为 | dirty | resource update |
|---|---|---|---|
| network/timeout/5xx | 显示“微流服务不可用”与 traceId | true | 不更新 |
| 401 | 显示登录失效 | true | 不更新 |
| 403 | 显示无权限 | true | 不更新 |
| 404 | 显示微流不存在，可关闭/刷新 | true | 不 resurrect |
| 409 | Conflict Modal | true | 不更新 |
| 422/400 | 显示校验失败，编辑器 Problems 接收 validation issues | true | 不更新 |
| unknown | 显示原始 message/traceId | true | 不更新 |

## 9. Guard Strategy

Close dirty/saving tab 打开 guard：Save 派发当前微流保存事件，成功后 force close；Discard 丢弃本地状态关闭；Cancel 保留。Switch dirty tab 因当前没有跨 tab draft cache，阻止切换并提示先保存/关闭处理，避免卸载编辑器导致静默丢稿。任意 dirty/saving/queued 存在时注册 beforeunload。删除 dirty resource 时提示本地更改会丢失，删除成功清理 save state。

## 10. Optimistic Update / Rollback

编辑 schema 是本地 optimistic；保存中仅显示 saving/autosaving，不提前更新 version/schemaId。保存成功才更新 resource/tab/tree、last saved、dirty=false。保存失败保留本地 schema、dirty=true、status=error/conflict，不更新 resource version/schemaId。保存请求返回时若 tab 已关闭或资源被删除，忽略响应，避免 resurrect。

## 11. Isolation Strategy

A/B/C dirty 和 save state 均以 `microflowId` 为 key。每个 microflow 同时最多一个 in-flight PUT；保存中编辑设置 queued，当前请求完成后保存最新 schema。旧响应只应用到同 id 且 tab 仍打开的 resource。B 保存失败只写 B 的 error，不影响 A/C。

## 12. Verification

自动测试：

- `workbench-tabs-lifecycle.spec.ts` 增加 per microflow dirty/save state 隔离。
- 覆盖 delete 清理 save state、rename 保留 save state。

手工验收建议：

1. 启动 AppHost 与 AppWeb。
2. 打开 `/space/:workspaceId/mendix-studio/:appId`。
3. 打开 A=`MF_ValidatePurchaseRequest`，修改节点属性，确认 A dirty。
4. 点击 Save，确认请求 `PUT /api/microflows/{A.id}/schema`，body 含 `schema/baseVersion/schemaId/version/saveReason/clientRequestId`。
5. 确认 saving -> saved，dirty=false，lastSavedAt 更新。
6. 打开 B=`MF_CalculateApprovalLevel`，修改 B，确认 B dirty 且 A 不 dirty。
7. 快速点击 Save 多次，确认同一 microflow 同时最多一个 PUT。
8. 开启 Autosave 后连续编辑，确认 4s debounce 只保存一次；关闭 Autosave 时不自动请求。
9. 模拟 500/422，确认 dirty 保持 true 且错误可见。
10. 模拟 409，确认 Conflict Modal 出现；Keep Local 保留 dirty；Reload Remote 替换远端；Force Save 二次确认。
11. 关闭 dirty tab，确认 Save/Discard/Cancel guard；刷新页面确认 beforeunload。
12. 删除 dirty 微流，确认提示且删除后状态清理。
13. 检查没有调用 local adapter/localStorage 作为真实保存，没有保存 sampleOrderProcessingMicroflow，没有保存 FlowGram JSON。
