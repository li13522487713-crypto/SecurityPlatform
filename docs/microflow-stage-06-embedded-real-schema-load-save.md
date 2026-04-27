# Microflow Stage 06 - Embedded Real Schema Load & Save

## 1. Scope

本轮完成：

- `activeMicroflowId` / `activeWorkbenchTabId` 驱动真实 resource/schema 加载。
- 在 Mendix Studio Workbench 内嵌真实 `MicroflowEditor`。
- 复用 `MendixMicroflowEditorEntry` 与 `createMicroflowEditorApiClient` / `editor-save-bridge`。
- 保存通过 `resourceAdapter.saveMicroflowSchema` 调用 `PUT /api/microflows/{id}/schema`。
- 以 `microflowId` 为粒度隔离画布状态。
- 刷新页面后再次打开微流，会从后端重新加载已保存内容。

本轮不做：

- 新节点类型。
- 节点属性深度增强。
- Call Microflow metadata。
- runtime / trace / debug。
- execution engine。

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-studio-core/src/index.tsx` | 修改 | 将 Stage 05 placeholder 替换为嵌入式真实微流编辑器。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/store.ts` | 修改 | 新增 `updateStudioMicroflowFromResource` 与 `markWorkbenchTabDirty`。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/studio/StudioEmbeddedMicroflowEditor.tsx` | 新增 | 加载真实 resource/schema，处理 loading/error/retry，并嵌入 `MendixMicroflowEditorEntry`。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/editor/MendixMicroflowEditorEntry.tsx` | 修改 | 增加 dirty 回调，并给 `MicroflowEditor` 设置 `microflowId:schemaId:version` key。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/editor/editor-save-bridge.ts` | 修改 | 保存仍走真实 `resourceAdapter.saveMicroflowSchema`，移除未注入 runtime 时的 local client fallback。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/components/workbench-toolbar.tsx` | 修改 | 微流 tab 下说明画布工具栏由内嵌编辑器提供。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/studio.css` | 修改 | 增加嵌入式微流加载态样式。 |
| `docs/microflow-stage-01-gap-matrix.md` | 修改 | 更新 Stage 06 schema 加载、保存、节点/连线持久化状态。 |

## 3. Load Data Flow

App Explorer microflow node
-> `openMicroflowWorkbenchTab(microflowId)`
-> `activeWorkbenchTab`
-> `StudioEmbeddedMicroflowEditor`
-> `adapterBundle.resourceAdapter.getMicroflow(microflowId)`
-> `adapterBundle.runtimeAdapter.loadMicroflow(microflowId)`
-> `MicroflowAuthoringSchema`
-> `MendixMicroflowEditorEntry`
-> `MicroflowEditor key={microflowId:schemaId:version}`

实际 API：

- `GET /api/microflows/{id}`：加载 resource metadata。
- `GET /api/microflows/{id}/schema`：加载 draft authoring schema。

## 4. Save Data Flow

MicroflowEditor Save
-> `createMicroflowEditorApiClient`
-> `editor-save-bridge`
-> `resourceAdapter.saveMicroflowSchema(resource.id, schema, { baseVersion, saveReason })`
-> `PUT /api/microflows/{id}/schema`
-> `SaveMicroflowSchemaResponse`
-> `adapter.getMicroflow(id)` 刷新 resource metadata
-> `updateStudioMicroflowFromResource(resource)`
-> `markWorkbenchTabDirty(tabId, false)`

保存失败时 editor 保持 dirty，不更新 resource metadata，不假装成功。

## 5. Isolation Strategy

- Workbench tab id 固定为 `microflow:{microflowId}`。
- 嵌入式 editor 只渲染 active tab。
- `StudioEmbeddedMicroflowEditor` 在 `microflowId` 变化时重新加载 resource/schema。
- `MendixMicroflowEditorEntry` 与内部 `MicroflowEditor` 都使用 `key={microflowId:schemaId:version}`，防止复用上一个微流的内部 `useState(() => props.schema)`。
- 请求乱序通过 `requestSeqRef` 忽略旧请求结果，避免 A 请求慢返回覆盖 B。
- 本轮未实现切换前 dirty guard；切换 tab 后会重新从后端加载 active 微流。未保存修改可能丢失，后续可增加切换确认。

## 6. Error Handling

- loading：显示 `Loading microflow schema...`。
- workspaceId / adapterBundle / microflowId 缺失：显示错误空态和 Retry。
- local/mock adapter：显示错误，不作为真实保存 fallback。
- resource 404：显示 `Microflow no longer exists`。
- schema missing / invalid：显示 `Microflow schema not found or invalid`，不回退 sample。
- save failed：由 `MicroflowEditor` 展示错误 Toast，dirty 不清除。
- resource deleted：重新加载时显示不存在，不继续保存已删除资源。
- 快速切换：旧请求返回被忽略。

## 7. Verification

手工验收步骤：

1. 打开 `/space/:workspaceId/mendix-studio/:appId`。
2. 展开 `Procurement`。
3. 展开 `Microflows`。
4. 新建或选择 `MF_ValidatePurchaseRequest`。
5. 点击 `MF_ValidatePurchaseRequest`。
6. 确认打开真实嵌入式编辑器。
7. 确认没有显示 `sampleOrderProcessingMicroflow`。
8. 拖拽 Start 节点。
9. 拖拽 End 节点。
10. 连线 Start -> End。
11. 保存。
12. 确认 Network 中调用 `PUT /api/microflows/{id}/schema`。
13. 刷新页面。
14. 再次打开 `MF_ValidatePurchaseRequest`。
15. 确认 Start / End / 连线恢复。
16. 新建或选择 `MF_CalculateApprovalLevel`。
17. 打开 `MF_CalculateApprovalLevel`。
18. 确认它不是 `MF_ValidatePurchaseRequest` 的画布。
19. 在 `MF_CalculateApprovalLevel` 中拖拽自己的节点并保存。
20. 刷新页面后分别打开两个微流，确认数据隔离。
21. 模拟保存失败，确认错误提示。
22. 快速点击两个微流，确认不会串画布。
23. 删除已打开且无引用微流，确认 tab/editor 不再保存该资源。

类型检查 / 构建命令：

```bash
cd src/frontend
pnpm --filter atlas-app-web run build
```
