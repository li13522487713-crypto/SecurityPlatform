# Microflow Stage 04 - App Explorer Microflow CRUD

## 1. Scope

本轮完成：

- New Microflow
- Rename Microflow
- Duplicate Microflow
- Delete Microflow
- Refresh Microflows
- Delete references precheck

本轮不做：

- schema load
- schema save
- canvas isolation
- node drag save
- Call Microflow metadata
- runtime/trace

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer.tsx` | 修改 | 在 App Explorer Microflows 分组和真实微流节点接入右键 CRUD 菜单、真实 Resource API 调用、列表刷新、删除引用预检查和真实微流点击空态入口。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/store.ts` | 修改 | 删除微流时同步清理 `microflowResourcesById`、`microflowIdsByModuleId`、`activeMicroflowId` 和选中树节点。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/resource/CreateMicroflowModal.tsx` | 修改 | 允许调用方传入当前 module，并在 App Explorer 场景锁定 moduleId，避免新建时写死模块。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/resource/RenameMicroflowModal.tsx` | 修改 | 放宽资源输入类型并展示提交错误，支持 App Explorer 复用。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/resource/DuplicateMicroflowModal.tsx` | 修改 | 支持 `_Copy` 默认名称、可编辑 displayName，并展示提交错误，支持 App Explorer 复用。 |
| `docs/microflow-stage-01-gap-matrix.md` | 修改 | 将 Stage 04 覆盖的 P0/P1 gap 更新为已完成。 |
| `docs/microflow-stage-04-app-explorer-crud.md` | 新增 | 记录本轮范围、数据流、API 契约、引用保护、UI 状态和验收步骤。 |

## 3. CRUD Data Flow

New:
App Explorer Microflows folder
-> Create dialog
-> `resourceAdapter.createMicroflow`
-> `POST /api/microflows`
-> `MicroflowResource`
-> `mapMicroflowResourceToStudioDefinitionView`
-> store
-> refresh tree

Rename:
Microflow node
-> Rename dialog
-> `resourceAdapter.renameMicroflow`
-> `POST /api/microflows/{id}/rename`
-> store update
-> refresh tree

Duplicate:
Microflow node
-> Duplicate dialog
-> `resourceAdapter.duplicateMicroflow`
-> `POST /api/microflows/{id}/duplicate`
-> store update
-> refresh tree

Delete:
Microflow node
-> references precheck
-> `DELETE /api/microflows/{id}`
-> store remove
-> refresh tree

## 4. API Contract Used

实际使用的 adapter 方法：

- `resourceAdapter.listMicroflows(query)`
- `resourceAdapter.createMicroflow(input)`
- `resourceAdapter.renameMicroflow(id, name, displayName)`
- `resourceAdapter.duplicateMicroflow(id, input)`
- `resourceAdapter.getMicroflowReferences(id, { includeInactive: false })`
- `resourceAdapter.deleteMicroflow(id)`

实际使用的后端 API 路径：

- `GET /api/microflows`
- `POST /api/microflows`
- `POST /api/microflows/{id}/rename`
- `POST /api/microflows/{id}/duplicate`
- `GET /api/microflows/{id}/references`
- `DELETE /api/microflows/{id}`

## 5. Delete Reference Protection

删除入口先调用 `resourceAdapter.getMicroflowReferences(id, { includeInactive: false })` 查询入向 active references。若存在 active references，确认按钮禁用，并展示引用来源，提示“该微流正在被其他对象引用，不能直接删除。”

如果引用预检查失败，前端允许继续确认，但明确提示最终以后端校验为准。执行 `resourceAdapter.deleteMicroflow(id)` 时，如果后端返回 409 或 `MICROFLOW_REFERENCE_BLOCKED`，前端展示后端错误，不从列表和 store 中移除该微流。

删除成功后调用 `removeStudioMicroflow(id)`，并刷新当前 module 的真实微流列表。如果删除的是 `activeMicroflowId`，store 会清空 `activeMicroflowId`，App Explorer 会切回 `pageBuilder`，避免继续展示 sample 微流画布。

## 6. UI States

- creating：新建弹窗提交中展示确认按钮 loading，成功后刷新列表。
- renaming：重命名弹窗提交中展示确认按钮 loading，成功后保持同一 resource id 并刷新列表。
- duplicating：复制弹窗提交中展示确认按钮 loading，成功后写入新 resource id 并刷新列表。
- deleting：删除确认弹窗展示 references 检查状态和删除提交 loading。
- validation error：name 为空、trim 后为空、含空格或同 module 重名时阻止提交并提示。
- server error：adapter 抛出的服务端错误通过 `getMicroflowErrorUserMessage` 友好展示。
- duplicate name：同一 module 下大小写不敏感重名会在前端阻止，服务端 duplicate/validation error 仍会展示服务端错误。
- reference blocked：references 预检查命中时禁用删除；后端 409 时不移除节点并展示错误。

## 7. Verification

手工验收步骤：

1. 打开 `/space/:workspaceId/mendix-studio/:appId`。
2. 展开 `Procurement`。
3. 展开 `Microflows`。
4. 确认显示真实后端微流列表。
5. 在 `Microflows` 分组上新建 `MF_ValidatePurchaseRequest`。
6. 确认树中出现 `MF_ValidatePurchaseRequest`。
7. 刷新页面。
8. 确认 `MF_ValidatePurchaseRequest` 仍存在。
9. 重命名为 `MF_ValidatePurchaseRequest_V2`。
10. 确认树节点更新，resource id 不变。
11. 复制 `MF_ValidatePurchaseRequest_V2` 为 `MF_ValidatePurchaseRequest_Copy`。
12. 确认树中出现新资源，resource id 不同。
13. 删除 `MF_ValidatePurchaseRequest_Copy`。
14. 确认树中移除。
15. 如果某个微流被引用，尝试删除时必须提示不能删除。
16. 点击任意真实微流，确认只设置 `activeMicroflowId` 并展示 Stage 05/06 空态，不展示 `sampleOrderProcessingMicroflow` 作为真实画布。
