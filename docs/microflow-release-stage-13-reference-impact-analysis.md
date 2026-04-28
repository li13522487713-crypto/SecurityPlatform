# Microflow Release Stage 13 - Reference & Impact Analysis

## 1. Scope

本轮完成 `/space/:workspaceId/mendix-studio/:appId` 中 Microflows 的引用关系与影响分析基础能力：

- callers query：通过后端 `GET /api/microflows/{id}/references` 查询当前微流作为 target 时的入向引用。
- callees query/schema parse：仓库未发现公开 `GET /api/microflows/{id}/callees`，本轮从当前 source microflow schema 解析 `callMicroflow` action。
- References Panel / Drawer：复用并强化 `MicroflowReferencesDrawer`，展示 Callers、Callees、Impact Summary、loading/empty/error/retry、stale/incomplete warning。
- App Explorer View References：真实 microflow 节点右键菜单可打开 References Drawer，保留 referenceCount badge 仅显示 DTO 值。
- Workbench References entry：Workbench toolbar 与 editor toolbar 均可打开 References。
- delete precheck：删除前调用 callers API；precheck 失败或 active callers 存在时阻断。
- DELETE 409 handling：后端引用保护返回 409 时前端不移除树节点，打开 references 并刷新资源列表。
- referenceCount display/refresh：只使用 `MicroflowResourceDto.referenceCount`；schema save/duplicate/delete source 后由后端 indexer 更新受影响 target 的 count，前端通过 list refresh 获取。
- target rename stability：以 `targetMicroflowId` 为权威，`targetMicroflowQualifiedName` 只做显示快照；重命名后显示最新资源名并提示 stale qualifiedName。
- duplicate/delete source refresh：duplicate 后重建新 source 出向 references，delete source 后删除出向 references 并刷新 target count。
- stale reference warning：覆盖 missing target id、target not found、self call、stale qualifiedName、inactive caller。
- A/B/C reference state isolation：Drawer 按 `microflowId` 请求序号清空和写入数据，callee 解析只使用当前 schema。

本轮不做：Call Microflow runtime executor、full cycle runtime detection、publish/run/trace、full Problems panel、execution engine。

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/backend/Atlas.Application.Microflows/Models/MicroflowVersionPublishDtos.cs` | 修改 | `MicroflowReferenceDto` 增加 `createdAt`、`updatedAt`。 |
| `src/backend/Atlas.Application.Microflows/Services/MicroflowReferenceServices.cs` | 修改 | 引用 scanner 不再用 qualifiedName 伪造 target id；DTO 输出时间字段；重建后刷新 target `referenceCount`。 |
| `src/backend/Atlas.Application.Microflows/Services/MicroflowResourceService.cs` | 修改 | 删除 source 后刷新被调用 target 的 `referenceCount`。 |
| `src/backend/Atlas.Application.Microflows/Repositories/IMicroflowRepositories.cs` | 修改 | 增加批量 reference count 查询与 resource count 更新接口。 |
| `src/backend/Atlas.Infrastructure/Repositories/Microflows/MicroflowRepositories.cs` | 修改 | 实现按 target ids 聚合 active references count。 |
| `src/backend/Atlas.Infrastructure/Repositories/Microflows/MicroflowResourceRepository.cs` | 修改 | 实现 resource `ReferenceCount` 更新。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/index.tsx` | 修改 | 接入 Studio 全局 References Drawer。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer.tsx` | 修改 | App Explorer View References 与 delete callers precheck / 409 handling。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer-tree.tsx` | 修改 | 微流节点右键菜单启用 View References / Delete。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/components/workbench-toolbar.tsx` | 修改 | Workbench toolbar 增加 References 入口。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/editor/MendixMicroflowEditorEntry.tsx` | 修改 | Editor toolbar 引用入口显示 DTO referenceCount。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/resource/MicroflowResourceTab.tsx` | 修改 | 资源库删除前 callers precheck 与 409 保护展示。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/references/MicroflowReferencesDrawer.tsx` | 修改 | 强化 Callers/Callees/Impact Summary/stale 展示。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/references/microflow-reference-types.ts` | 修改 | 补充 reference 时间字段和 callee stale/incomplete view model。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/references/microflow-reference-utils.ts` | 修改 | 增加 callee parser、stale warning、group/delete helper、target id 提取。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/adapter/http/microflow-api-error.ts` | 修改 | 增加引用保护错误用户文案。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/studio/__tests__/microflow-references.test.ts` | 修改 | 补 target rename stale qualifiedName 与不按 qualifiedName 猜 id 测试。 |
| `docs/microflow-p1-release-gap.md` | 修改 | 更新 Stage 13 references 状态。 |

## 3. Reference API Contract

| 语义 | API | Adapter 方法 | DTO | 说明 |
|---|---|---|---|---|
| callers / 入向引用 | `GET /api/microflows/{id}/references` | `getMicroflowReferences(id, query)` | `MicroflowReferenceDto` / `MicroflowReference` | 当前微流作为 target，被哪些 source 引用。 |
| rebuild references | `POST /api/microflows/{id}/references/rebuild` | 未新增前端入口 | `MicroflowReferenceDto[]` | 后端已有手动 rebuild API，本轮 UI 不新增操作入口。 |
| callees / 出向调用 | 源码未发现公开 API | 未新增 API adapter | `StudioMicroflowCalleeView` | 本轮从当前 source schema 的 `objectCollection.objects` 递归解析。 |
| referenceCount | `MicroflowResourceDto.referenceCount` | `listMicroflows/getMicroflow` | `MicroflowResource.referenceCount` | 前端只展示后端 DTO 字段，不自行扫描累加。 |

引用 API / DTO 盘点：

| 能力 | 前端 adapter | 后端 API | Controller | Service | DTO | 当前语义 | 本轮处理 |
|---|---|---|---|---|---|---|---|
| callers 查询 | `getMicroflowReferences` | `GET /api/microflows/{id}/references` | `GetReferences` | `GetReferencesAsync` | `MicroflowReferenceDto` | target 被哪些 source 引用 | 直接复用并接入 UI/delete precheck。 |
| callees 查询 | 无 API adapter | 未发现 `/callees` | 无 | 仓储有 `ListBySourceAsync` 但未公开 | `StudioMicroflowCalleeView` | source 调用了哪些 target | 本轮 schema parse，不补后端 API。 |
| DTO 时间字段 | `MicroflowReference.createdAt/updatedAt` | references response | `GetReferences` | `ToDto` | `createdAt/updatedAt` | 引用创建/更新时间 | 最小补齐输出。 |
| referenceCount | `MicroflowResource.referenceCount` | list/get resource | `GetPaged/GetById` | `ListAsync/GetAsync` | `MicroflowResourceDto.referenceCount` | 后端资源字段 | 保存/复制/删除 source 后刷新受影响 target count。 |
| 删除保护 | `deleteMicroflow` + precheck | `DELETE /api/microflows/{id}` | `Delete` | `EnsureNoActiveTargetReferencesAsync` | error envelope | active callers 阻止删除 | 前端新增 precheck，409 不移除节点。 |
| 保存重建 | `saveMicroflowSchema` | `PUT /schema` | `SaveSchema` | `TryRebuildOutgoingReferencesAsync` | resource DTO | source 保存后重建出向引用 | 修正 scanner 不用 qualifiedName 伪造 id。 |
| duplicate 重建 | `duplicateMicroflow` | `POST /duplicate` | `Duplicate` | `DuplicateAsync` | resource DTO | 新 source 复制 schema 后重建 | 保留并刷新 target count。 |
| rename 稳定 | `renameMicroflow` | `POST /rename` | `Rename` | `RenameAsync` | resource DTO | id 不变，qualifiedName 变化 | UI 用 id 查最新资源名并标 stale snapshot。 |
| workspace/tenant 过滤 | header/query | references 按 target id 查 | `GetReferences` | repository target id filter | references | 过滤证据不足 | 文档记录依赖缺口；本轮不大改权限/租户。 |

依赖缺口与本轮最小补齐点：

- 缺口：没有公开 `callees` API。本轮最小补齐：前端只解析当前 source schema，不新增 mock/fake API。
- 缺口：references 查询未发现 workspace/tenant 强过滤。本轮最小补齐：不改大范围权限模型，文档记录为后续安全缺口。
- 缺口：旧 scanner 会用 `targetMicroflowQualifiedName` 作为 target key。本轮最小补齐：只在 `targetMicroflowId` 存在时写 reference，禁止 qualifiedName 伪造 id。

## 4. Callers Strategy

Callers 通过 `resourceAdapter.getMicroflowReferences(targetMicroflowId, { includeInactive })` 加载，组件不裸 fetch。Drawer 展示 `sourceName`、`sourceType`、`sourcePath`、`referenceKind`、`active`、`sourceId`、`targetMicroflowId`、`updatedAt`。加载失败显示 error + retry，不回退假空列表。`sourceType === "microflow"` 且 `sourceId/canNavigate` 可用时允许打开 source tab。

删除流程中 callers precheck 是强制步骤：查询失败默认阻止删除；active callers > 0 时打开 References Drawer 并阻止删除。

## 5. Callees Strategy

仓库未发现公开 `GET /api/microflows/{sourceId}/callees`。本轮通过 `parseMicroflowCallees(schema, sourceMicroflowId, resourcesById)` 递归遍历当前 schema 的 `objectCollection.objects`，识别 `action.kind/actionKind === "callMicroflow"`。

`targetMicroflowId` 是唯一权威；`targetMicroflowQualifiedName` 仅为显示快照。缺失 `targetMicroflowId` 时即使有 qualifiedName，也标记 `incomplete/missingTargetId`，不猜 id。target id 找不到标记 `targetNotFound`；stored qualifiedName 与最新 resource qualifiedName 不一致标记 `staleQualifiedName`。

## 6. References Panel Strategy

Drawer Header 展示 displayName、qualifiedName、microflowId、referenceCount、Refresh。主体包含 Callers、Callees、Impact Summary。请求按 `requestSeq` 隔离，切换 A/B/C 时先清空旧数据，旧请求不会覆盖当前 panel。

Callers 有 loading / No callers / error + retry；Callees 有 No callees、stale/incomplete badge、open target action。Impact Summary 展示 active callers、inactive callers、callees、stale、missing target、delete blocked 文案，以及 rename/duplicate/delete impact 说明。

## 7. Delete Protection Strategy

完整流程：

1. 用户点击 Delete。
2. 前端调用 `getMicroflowReferences(id, { includeInactive: true })`。
3. 查询失败：阻止删除，Toast 提示无法验证，并打开 References。
4. active callers > 0：阻止删除，Toast 展示调用方，打开 References。
5. 无 active callers：显示确认框。
6. 确认后调用 `DELETE /api/microflows/{id}`。
7. DELETE 成功：移除 store/tree/tab，刷新列表。
8. DELETE 409：不移除树节点，打开 References，展示后端错误并刷新列表。
9. 其他错误：不移除树节点，显示错误。

## 8. Rename Stability Strategy

`targetMicroflowId` 是稳定主键，rename target 不改变 id。UI 通过 `resourcesById[targetMicroflowId]` 显示最新 displayName/qualifiedName；当 source schema 存储的 `targetMicroflowQualifiedName` 与最新 qualifiedName 不一致时展示 stale warning。不会批量改写 source schema，下一次保存 source 时可刷新显示快照。

## 9. Reference Refresh Strategy

保存 source schema 后，后端 `SaveSchemaAsync` 重建 source 出向 references，并刷新受影响 target 的 `ReferenceCount`。duplicate source 后同样重建新 source 出向 references。delete source 后删除 source 出向 references，并刷新受影响 target count。前端保存后刷新当前 resource；Drawer Refresh 和 App Explorer reload 从后端 DTO 重新读取 count。referenceCount 不可用时不伪造。

## 10. Impact Summary Strategy

Impact Summary 显示 callers count、active/inactive callers、callees count、stale/missing target count、delete blocked 状态，并明确 rename 因 `targetMicroflowId` 稳定而安全，duplicate source 会新增 caller，delete source 会在 reference rebuild 后移除 caller。

## 11. Verification

自动测试：

- 更新 `microflow-references.test.ts`，覆盖 parse callers/callees 基础、active callers 阻断、A/B parsing 隔离、target rename stale qualifiedName、不按 qualifiedName 猜 id。

手工验收建议：

1. 启动 AppHost 与 AppWeb。
2. 打开 `/space/:workspaceId/mendix-studio/:appId`。
3. 打开 A，拖入 Call Microflow，选择 B，保存 A。
4. 打开 B References，确认 callers 出现 A。
5. 打开 A References，确认 callees 出现 B。
6. App Explorer 右键 B，View References 可打开。
7. 删除 B 前会查 callers；active callers 存在时阻止删除。
8. 模拟 DELETE 409，确认 B 不从树移除并打开 References。
9. 重命名 B 后，A 的 targetMicroflowId 不变，UI 显示新名称或 stale qualifiedName warning。
10. duplicate A 后刷新 B callers；delete A 后刷新 B callers 减少。
11. 打开 C，确认 References 不显示 A/B 数据。
12. 模拟 references API 失败，确认 error + retry，不显示假空。
13. 检查 referenceCount badge 只在 DTO 提供且大于 0 时展示。
14. 检查未使用 `sampleOrderProcessingMicroflow`、localStorage/local adapter 作为真实 references 数据。
