# Microflow Release Stage 02 - App Explorer Asset Tree

## 1. Scope

本轮完成：

- App Explorer 拆为 `AppExplorerContainer`、`AppExplorerTree`、`MicroflowTreeSection`。
- Microflows 分组通过 `adapterBundle.resourceAdapter.listMicroflows` 加载真实资源。
- Microflows 分组支持 loading、empty、error、retry、success。
- App Explorer 支持 200ms debounce 的本地搜索过滤。
- Microflows 分组支持 Refresh。
- 真实微流节点写入 `microflowId`、`resourceId`、`moduleId`、`qualifiedName`、`status`、`publishStatus`、`referenceCount`。
- 点击真实微流只设置选中态和 `activeMicroflowId`，不打开 sample editor。
- Context menu 框架保留 Refresh / Open / Properties 占位，CRUD 项禁用并标注 Stage 3。

本轮不做：

- Create/Rename/Duplicate/Delete。
- schema load/save。
- real editor host。
- Call Microflow metadata。
- publish/run/trace。

## 2. Stage 0 Hotfix Status

第 0 轮 Hotfix 按当前审计文档和源码为“基本通过”，不是本轮前置约束。本轮不依赖 Create Microflow 成功路径，也没有修改 `CreateMicroflowModal`。

| Hotfix 检查项 | 当前状态 | 是否阻塞本轮 | 后续处理 |
|---|---|---|---|
| `CreateMicroflowModal` catch `onSubmit` rejection | 已 catch | 否 | 保持现状 |
| Console uncaught promise | 目标弹窗路径未发现 | 否 | 后续继续监控独立旧路径 |
| 409 / 422 / 401 / 403 / 500 / network error 区分展示 | 已有基础区分 | 否 | 后续统一 action 文案 |
| `moduleId` 是否默认 `sales` | 目标页不默认 `sales`，仍来自 sample Procurement module | 否 | 真实 module tree 后续处理 |
| name 前端校验是否与后端一致 | 基本一致 | 否 | 后续补端到端覆盖 |

## 3. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer.tsx` | 重构 | 改为 container，负责真实 list、refresh、store 写入和选择行为 |
| `src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer-tree.tsx` | 新增 | 负责 header/search/tree view/context menu 框架 |
| `src/frontend/packages/mendix/mendix-studio-core/src/components/microflow-tree-section.tsx` | 新增 | 负责 Microflows 状态节点和真实微流节点映射 |
| `src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer.spec.tsx` | 新增 | 覆盖真实 list、状态、搜索、点击、刷新和 sample 隔离 |
| `docs/microflow-p1-release-gap.md` | 更新 | 记录 Stage 02 完成只读列表，保留后续 blocker |
| `docs/microflow-release-stage-02-app-explorer-assets.md` | 新增 | 本轮发布说明与验收记录 |

## 4. Data Flow

`mendix-studio` route
-> `MendixStudioApp`
-> `AppExplorerContainer`
-> `adapterBundle.resourceAdapter.listMicroflows`
-> `GET /api/microflows?workspaceId&moduleId`
-> `MicroflowResource`
-> `mapMicroflowResourceToStudioDefinitionView`
-> `store.microflowResourcesById / microflowIdsByModuleId`
-> App Explorer Microflows children

## 5. ModuleId Source

当前 moduleId 由 `getCurrentExplorerModuleId()` 统一封装，优先取当前 module node 的 `moduleId`，当前实际值仍来自 `SAMPLE_PROCUREMENT_APP.modules[0].moduleId`，即 `mod_procurement`。

当前 Microflows 分组已真实化，但 module tree 本身仍来自 sample/static 数据，完整 module tree 真实化不是本轮范围。

## 6. Tree Node Contract

真实微流节点结构：

- `id/key = microflow:{resource.id}`
- `kind = microflow`
- `label = resource.displayName || resource.name`
- `microflowId = resource.id`
- `resourceId = resource.id`
- `moduleId = resource.moduleId`
- `name`
- `displayName`
- `qualifiedName`
- `status`
- `publishStatus`
- `referenceCount`

## 7. UI States

- loading：显示 `Loading microflows...`，不显示旧硬编码数据。
- empty：显示 `No microflows`。
- error：显示 `Load failed`、错误 message/status/code/traceId 和 `Retry`。
- retry：点击 Retry 只重新调用当前 module 的 `listMicroflows`。
- success：显示真实微流节点、status、referenceCount badge。
- search result：按 module/folder label 与 microflow `label/name/displayName/qualifiedName` 本地过滤，保留父级上下文，清空搜索恢复完整树。

## 8. Context Menu

本轮可用：

- Microflows 分组：`Refresh`。
- 微流节点：`Open / Select`、`Refresh`。

本轮禁用或占位：

- `New Microflow`：disabled，Stage 3 接入。
- `Rename`：disabled，Stage 3 接入。
- `Duplicate`：disabled，Stage 3 接入。
- `Delete`：disabled，Stage 3 接入。
- `View Properties` / `View References`：占位或后续轮次接入。

## 9. Sample Isolation

`Domain Model`、`Pages`、`Workflows`、`Security`、`Navigation`、`Constants`、`Theme` 仍保留 sample/static 数据。

Microflows 分组不再使用 `TREE_DATA` 的 sample child，不再固定显示 `MF_SubmitPurchaseRequest`。API 返回空时显示 `No microflows`，请求失败时显示 `Load failed` 和 `Retry`，不会 fallback 到 sample microflow。

点击真实微流节点只设置 `selectedExplorerNodeId`、`selectedKind=microflow`、`activeModuleId`、`activeMicroflowId`；本轮不打开真实 editor，也不会展示 `sampleOrderProcessingMicroflow`。

## 10. Verification

自动验证：

- `pnpm exec vitest run packages/mendix/mendix-studio-core/src/components/app-explorer.spec.tsx`：7 passed。
- `pnpm run build:app-web`：通过。
- `ReadLints`：本轮修改文件无新增 linter errors。

手工验收步骤：

1. 启动后端 `dotnet run --project src/backend/Atlas.AppHost`。
2. 启动前端 `pnpm run dev:app-web`。
3. 打开 `/space/:workspaceId/mendix-studio/:appId`。
4. 展开 Procurement。
5. 展开 Microflows。
6. 确认触发 `GET /api/microflows?workspaceId=&moduleId=`。
7. 后端返回空时显示 `No microflows`。
8. 通过后端种子数据、API/Postman、独立资源页或现有数据库准备一个真实微流。
9. 回到 `mendix-studio` 点击 Refresh。
10. 确认树中显示真实微流。
11. 再准备第二个真实微流。
12. Refresh 后确认两个微流都显示。
13. 搜索其中一个 `displayName`。
14. 确认只显示匹配微流。
15. 清空搜索。
16. 确认完整列表恢复。
17. 模拟 API 失败。
18. 确认显示 error + retry。
19. 点击 retry 后能重新加载。
20. 点击真实微流节点。
21. 确认 `activeMicroflowId` 设置为真实 id。
22. 确认没有展示 `sampleOrderProcessingMicroflow`。
23. 确认 Microflows 分组不再固定显示硬编码 `MF_SubmitPurchaseRequest`。
24. 确认 Domain Model / Pages / Workflows 等 sample 分组仍能正常显示，不白屏。
25. 即使第 0 轮 Hotfix 未完成，本轮真实只读列表仍能验收。

本轮未执行浏览器手工验收；以上步骤作为发布前手工验收清单保留。
