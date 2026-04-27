# Microflow Stage 16 - Microflow Reference Relationship

## 1. Scope

本轮完成微流引用关系专项治理：

- callers 查询与展示：`GET /api/microflows/{id}/references` 语义确认为“谁引用当前 target microflow”。
- callees 展示：后端暂无独立 callees API，本轮从当前微流 authoring schema 解析 `callMicroflow` action。
- references drawer：展示 Callers / Callees、loading / empty / error / retry、stale / inactive 状态与打开来源/目标微流入口。
- 删除保护 UI：App Explorer 删除前查询 callers；active callers 阻止删除并打开引用抽屉；DELETE 409 保留树节点并刷新引用。
- referenceCount：App Explorer 展示后端返回的 `referenceCount` badge；保存、重命名、复制、删除后通过重新拉取资源列表刷新，不伪造计数。
- 重命名稳定性：`targetMicroflowId` 作为权威引用，`targetMicroflowQualifiedName` 仅作显示快照；资源索引刷新后 UI 使用新名称。
- 复制引用刷新：后端 duplicate 后重建新 source 的 outgoing MicroflowReference；前端复制后刷新资源列表与引用抽屉。
- A/B 微流引用隔离：引用抽屉按 `resource.id` 和当前 schema 重新计算，切换资源时清空旧 callers，callees 只解析当前 source schema。

本轮不做：

- Call Microflow 执行器。
- 完整循环调用检测。
- trace/debug。
- 表达式执行引擎。
- Domain Model metadata。
- 历史 schema migration。
- 新增 mock API 或孤立 demo 页面。

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/references/microflow-reference-types.ts` | 修改 | 新增 `StudioMicroflowCalleeView`。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/references/microflow-reference-utils.ts` | 修改 | 新增 callers 删除判断、display 解析、`parseMicroflowCallees` 与 stale warning helper。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/references/MicroflowReferencesDrawer.tsx` | 修改 | 扩展为 Callers / Callees 双区域，支持刷新、错误、空状态、stale/inactive 提示与打开微流。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer.tsx` | 修改 | 右键新增 View References；referenceCount badge；删除前 active callers 阻止；预检查失败不放行；DELETE 409 打开引用抽屉。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/editor/MendixMicroflowEditorEntry.tsx` | 修改 | Workbench 引用入口传入当前编辑器 schema、资源索引和打开微流回调。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/studio/StudioEmbeddedMicroflowEditor.tsx` | 修改 | 透传资源索引、打开微流与刷新资源列表能力。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/index.tsx` | 修改 | 保存/发布后触发 App Explorer 资源列表刷新，保证 referenceCount 来自后端最新列表。 |
| `src/backend/Atlas.Application.Microflows/Services/MicroflowResourceService.cs` | 修改 | duplicate 后调用 reference indexer 重建新 source outgoing references。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/studio/__tests__/microflow-references.test.ts` | 新增 | 覆盖 callees 解析、缺失 target、rename 稳定展示、删除判断与 A/B 隔离。 |
| `docs/microflow-stage-16-reference-relationship.md` | 新增 | Stage 16 契约、策略与验收记录。 |
| `docs/microflow-stage-01-gap-matrix.md` | 修改 | 更新 Stage 16 相关 gap 状态。 |

## 3. Reference API Contract

| 语义 | API | Adapter 方法 | DTO | 备注 |
|---|---|---|---|---|
| callers / incoming references | `GET /api/microflows/{id}/references` | `resourceAdapter.getMicroflowReferences(id, query)` | `MicroflowReferenceDto` / `MicroflowReference` | `id` 是 `targetMicroflowId`，返回谁引用当前微流。 |
| rebuild outgoing references | `POST /api/microflows/{id}/references/rebuild` | 暂无前端 adapter 暴露 | `MicroflowReferenceDto[]` | 后端保存 schema 后自动调用；手动 API 用于恢复索引。 |
| callees / outgoing calls | 暂无独立 API | 暂无 | `StudioMicroflowCalleeView` | 本轮从当前 source schema 的 `objectCollection.objects` 解析 `action.kind === "callMicroflow"`。 |

API 盘点：

| 能力 | 前端 adapter | 后端 API | DTO | 当前语义 | 本轮处理 |
|---|---|---|---|---|---|
| callers 查询 | `getMicroflowReferences` | `GET /api/microflows/{id}/references` | `MicroflowReference` | target references / incoming callers | 作为删除保护与 Callers 展示权威数据。 |
| callees 查询 | 无 | 无 | `StudioMicroflowCalleeView` | 无后端契约 | 从当前 schema 解析，不伪造 reference 表。 |
| schema 保存重建引用 | `saveMicroflowSchema` | `PUT /api/microflows/{id}/schema` | `SaveMicroflowSchemaResponse` | 保存后扫描 source schema outgoing refs | 沿用后端扫描 `targetMicroflowId` / `targetMicroflowQualifiedName`。 |
| duplicate 引用重建 | `duplicateMicroflow` | `POST /api/microflows/{id}/duplicate` | `MicroflowResourceDto` | 原先只复制 schema，不重建 refs | 本轮 duplicate 后重建新 source outgoing refs。 |
| 删除保护 | `deleteMicroflow` + `getMicroflowReferences` | `DELETE /api/microflows/{id}` | 409 `MICROFLOW_REFERENCE_BLOCKED` | 后端最终保护 active target refs | 前端预检查 + 409 后打开 references drawer。 |

后端 rebuild 扫描字段：

- action kind：`callMicroflow`。
- target 字段：优先 `targetMicroflowId`，缺失时使用 `targetMicroflowQualifiedName`。
- source object：当前 microflow resource id，source type 为 `microflow`。

## 4. Callers Strategy

Callers 输入为 `targetMicroflowId`。引用抽屉调用 `resourceAdapter.getMicroflowReferences(targetMicroflowId, { includeInactive })`，展示 `sourceName`、`sourceType`、`sourcePath`、`referenceKind`、`active`、`sourceId`、版本字段与描述。

展示按 source type 分组：Microflows、Pages、Workflows、Other 等沿用现有类型标签。`sourceType === "microflow"` 且 `sourceId` 可导航时，点击“打开来源微流”会打开对应 Workbench tab。loading 显示 `Loading references...`，empty 显示 `No callers`，error 显示可重试错误状态。

## 5. Callees Strategy

后端暂无 `GET /api/microflows/{id}/callees`，本轮不新增 API，原因是当前前端可直接访问当前编辑器 schema，且本轮不要求跨 source 图遍历。

`parseMicroflowCallees(schema, sourceMicroflowId, resourceIndex)` 遍历 `objectCollection.objects` 与 loop nested `objectCollection`，识别 `action.kind === "callMicroflow"`：

- `targetMicroflowId` 是权威引用。
- `targetMicroflowQualifiedName` 是显示快照。
- 若资源索引中存在 target id，则显示最新 `displayName` / `qualifiedName`。
- 缺失 `targetMicroflowId` 标记 `missingTargetId`。
- 找不到 target 标记 `targetNotFound`。
- `sourceMicroflowId === targetMicroflowId` 标记 direct self call。

Workbench 打开引用抽屉时，callees 使用当前 editor state，因此可包含未保存 Call Microflow；callers 始终来自后端已保存引用索引。

## 6. Delete Protection Strategy

完整流程：

1. 用户在 App Explorer 微流节点点击 Delete。
2. 前端调用 `getMicroflowReferences(id, { includeInactive: false })`。
3. 如果 active callers 大于 0，打开 references drawer，禁止 DELETE。
4. 如果预检查失败，提示无法验证引用关系，不放行删除。
5. 如果 callers 为空，展示删除确认。
6. 用户确认后调用 `DELETE /api/microflows/{id}`。
7. DELETE 成功后从 store/tree 移除资源并关闭对应 Workbench tab。
8. DELETE 409 / `MICROFLOW_REFERENCE_BLOCKED` 时不移除树节点，打开 references drawer，展示后端错误并重新拉取 references。
9. DELETE 其他失败时展示错误并保留前端节点。

## 7. Rename Stability Strategy

Call Microflow 配置中 `targetMicroflowId` 是权威字段，目标重命名不改变 id。`targetMicroflowQualifiedName` 只作为历史显示快照。

目标微流重命名后，App Explorer 与 Workbench 触发资源列表刷新；引用抽屉解析 callee 时用 `targetMicroflowId` 从 `resourceIndex` 取最新名称/qualifiedName。如果 stored qualifiedName 过期，不清空 target，也不批量改写 source schema；只有用户后续保存 source schema 时才会保存当前编辑器状态。

## 8. Reference Refresh Strategy

- schema 保存成功后：`MendixStudioApp` 增加 refresh token，App Explorer 重新 list microflows；打开的引用抽屉可手动 Refresh。
- duplicate 后：前端刷新列表；后端 duplicate 后重建新 source outgoing references。
- rename 后：前端刷新列表和打开的引用目标；target id 保持不变。
- delete source 后：前端刷新列表；后端删除 source references；目标 callers 需要刷新抽屉或资源列表重新加载。
- referenceCount：只展示后端返回值，不前端计算或伪造。

## 9. Verification

自动测试：

- `parseMicroflowCallees` 能解析 `callMicroflow.targetMicroflowId`。
- 缺失 `targetMicroflowId` 标记 stale。
- target rename 后基于 `targetMicroflowId` 解析最新 display name / qualifiedName。
- active callers 阻止删除判断。
- A/B source schema callee 解析互不污染。
- source display name 优先从 resource index 解析。

手工验收建议按 Stage 16 清单执行：在 `/space/:workspaceId/mendix-studio/:appId` 中创建 `MF_SubmitPurchaseRequest -> MF_ValidatePurchaseRequest` 调用，保存后查看 target callers、source callees、删除保护、409 保留节点、重命名 target 后 id 稳定、复制 source 后 target callers 增加、切换到其他微流后抽屉不显示旧数据、网络失败时显示 error + retry。
