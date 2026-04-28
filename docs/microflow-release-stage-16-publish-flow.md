# Microflow Release Stage 16 - Publish Flow

## 1. Scope

本轮完成 Publish button、Publish Dialog、Validate before publish、version notes、published snapshot、publish status sync、changedAfterPublish、Version History、Rollback、Impact summary、错误处理与 A/B/C 微流发布状态隔离。

本轮不做 runtime、trace、external deployment、approval workflow、real-time collaboration、full schema diff merge。

依赖缺口：

- 后端 Controller 当前仍为 `[AllowAnonymous]`，未发现完整资源级 workspace/tenant 权限校验。本轮不绕过权限，只记录发布风险。
- 后端未发现独立 audit log 实体/API；当前仅有 `CreatedBy`、`PublishedBy`、时间戳与服务日志。
- `PublishMicroflowApiRequestDto.Force` 已存在但服务未使用；前端默认不展示 force publish。
- `RollbackMicroflowVersionRequestDto.reason` 已存在但服务未持久化。

本轮最小补齐点：

- `MicroflowPublishService.PublishAsync` 改为复用 `IMicroflowValidationService.ValidateAsync(mode=publish)`，有 blockPublish/error 时返回 422 并阻止写入 version/snapshot/resource。
- `PublishMicroflowModal` 增加显式 Validate、Save & Publish、dirty 提示、完整 resource/version/status 字段、version notes、impact summary、sample 阻断与发布前二次校验。
- `MicroflowVersionsDrawer` 在 rollback 失败时展示包含 status/code/traceId 的错误弹窗。

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/backend/Atlas.Application.Microflows/Services/MicroflowVersionPublishServices.cs` | 修改 | Publish 复用真实后端 validation service，validation error 以 422 阻断发布。 |
| `src/backend/Atlas.AppHost/Bosch.http/MicroflowBackend.http` | 修改 | publish 示例字段从 `notes` 对齐为真实 DTO `description`。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/publish/PublishMicroflowModal.tsx` | 修改 | 发布对话框补齐 Validate、Save & Publish、字段展示、错误阻断和状态提示。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/editor/MendixMicroflowEditorEntry.tsx` | 修改 | 注入 dirty/save-before-publish，发布/回滚后同步 save state、dirty、schema。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/resource/resource-types.ts` | 修改 | 补齐 `qualifiedName` 展示字段类型。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/versions/MicroflowVersionsDrawer.tsx` | 修改 | rollback 失败展示 traceId/error code。 |
| `docs/microflow-release-stage-16-publish-flow.md` | 新增 | 本轮发布流程文档。 |
| `docs/microflow-p1-release-gap.md` | 修改 | 记录 Stage 16 状态。 |

## 3. Publish API Contract

| API | Adapter 方法 | Request DTO | Response DTO | Status / Error |
|---|---|---|---|---|
| `POST /api/microflows/{id}/publish` | `publishMicroflow(id, input)` | `PublishMicroflowApiRequestDto`：`version`、`description`、`confirmBreakingChanges`、`force` | `MicroflowPublishResultDto`：`resource`、`version`、`snapshot`、`validationSummary`、`impactAnalysis` | 200；409 version conflict/high impact；422 validation blocked；404 not found；500 traceId |
| `GET /api/microflows/{id}/impact` | `analyzeMicroflowPublishImpact(id, query)` | query `version/includeBreakingChanges/includeReferences` | `MicroflowPublishImpactAnalysisDto` | 200/404/500 |
| `GET /api/microflows/{id}/versions` | `getMicroflowVersions(id)` | 无 | `MicroflowVersionSummaryDto[]` | 200/404/500 |
| `GET /api/microflows/{id}/versions/{versionId}` | `getMicroflowVersionDetail(id, versionId)` | 无 | `MicroflowVersionDetailDto` | 200/404/500 |
| `POST /api/microflows/{id}/versions/{versionId}/rollback` | `rollbackMicroflowVersion(id, versionId, request)` | `RollbackMicroflowVersionRequestDto`：`reason` | `MicroflowResourceDto` | 200/403/404/409/422/500 |
| `POST /api/microflows/{id}/validate` | `validationAdapter.validate` | `ValidateMicroflowRequestDto` | `ValidateMicroflowResponseDto` | 200/422/500 |

能力盘点：

| 能力 | 前端 adapter | 后端 API | Controller | Service | DTO | 当前语义 | 本轮处理 |
|---|---|---|---|---|---|---|---|
| Publish | 已有 | 已有 | `Publish` | `MicroflowPublishService` | `PublishMicroflowApiRequestDto` | 写 version/snapshot/resource | 补完整 validation gate |
| Version notes | `description/releaseNote` | 已有 `description` | 同上 | 存入 version/snapshot description | `Description` | release notes 等价 description | Dialog 文案明确 |
| Validation before publish | 已有 adapter | 已有 validate API | `Validate` | `MicroflowValidationService` | `ValidateMicroflowRequestDto` | publish mode | 后端 publish 强制复用 |
| Latest published version | 已有字段 | 已有 | resource DTO | publish service 更新 | `MicroflowResourceDto` | 成功后更新 | Dialog/Header 展示 |
| current draft version | `resource.version/schemaId` | 已有 | resource/schema | resource service | resource DTO | 当前保存快照 | Dialog 展示 |
| publishStatus | 已有 | 已有 | resource DTO | save/publish/rollback 更新 | resource DTO | `neverPublished/published/changedAfterPublish` | 前端按后端状态展示 |
| Published snapshot | 已有类型 | 已有 | version detail/publish response | publish service | `MicroflowPublishedSnapshotDto` | 后端生成不可变快照 | Dialog 成功使用 response |
| Version history | 已有 | 已有 | `ListVersions/GetVersionDetail` | `MicroflowVersionService` | version DTO | 真实 API | Drawer 继续使用真实数据 |
| Rollback | 已有 | 已有 | `RollbackVersion` | `RollbackAsync` | rollback DTO | 复制历史 schema 为当前 draft | 失败 traceId 可见 |
| Audit log | 无独立 adapter | 未发现 | 无 | 无独立审计服务 | publishedBy/createdBy 字段 | 仅发布元数据 | 文档记录缺口 |

## 4. Publish State Model

- `publishing`、`publishError`、`validation`、`impact` 保持在 `PublishMicroflowModal` 局部，按当前 `resource.id` 隔离。
- `publishStatus`、`latestPublishedVersion`、`status`、`updatedAt` 使用后端返回的 `MicroflowResource`。
- `changedAfterPublish` 由后端 save/rename/rollback/publish 逻辑维护；前端 dirty 只作为未保存提示，不写入后端。
- `versionHistory` 使用 `GET /versions` 真实 API，不使用假数据。
- `rollbackState` 使用 `MicroflowVersionsDrawer` 调用真实 rollback API，失败不更新 resource。

## 5. Publish Dialog Strategy

Dialog 展示 microflow name/displayName、qualifiedName、microflowId、module、status、publishStatus、draft version、schemaId、latest published version、dirty 状态、validation summary、impact summary 与 version notes。

按钮包括 Validate、Publish、Save & Publish、Cancel。dirty=true 时默认走 Save & Publish：先保存当前 schema，再重新 validation，再 publish。

## 6. Validate Before Publish Strategy

打开 Dialog 自动执行 publish-mode validation，并同时加载 versions 与 impact。用户点击 Validate 可手动重跑。点击 Publish 前会再次校验当前 schema；存在 `blockPublish` 或 error issue 时不调用 publish，并触发 Problems 入口。

后端 publish 也强制调用 `IMicroflowValidationService.ValidateAsync(mode=publish)`，即使前端绕过也不会生成发布快照。

## 7. Save & Publish Strategy

dirty=false 时允许 Publish 当前保存版本。dirty=true 时按钮显示 Save & Publish，流程为保存 schema、保存成功后重新 validation、validation 通过后调用真实 publish API。保存失败或 conflict 时不 publish，保持 Dialog 可见并展示错误。

本轮不提供 “Publish last saved version” 选项，避免用户误发布旧 schema。

## 8. Publish Success Sync

发布成功后使用 response.resource 更新 editor local resource/schema，并通过 `onResourceUpdated` 同步 `microflowResourcesById`、App Explorer 节点、Workbench tab、Editor header。Save state 与 dirty 被置为 saved/false。

如果 publish 失败，不更新 `latestPublishedVersion`，不关闭 Dialog，不清 dirty。

## 9. Version History / Rollback Strategy

Version History 使用真实 `GET /versions` 与 `GET /versions/{versionId}`。详情展示 version、publishedAt/createdAt、publishedBy/createdBy、description、schemaSnapshotId、validation summary 与 published snapshot。

Rollback 使用真实 `POST /versions/{versionId}/rollback`。成功后刷新 resource/schema/tab/tree；失败展示错误 code/status/traceId。后端支持 rollback，但 reason 当前未持久化，记录为依赖缺口。

## 10. changedAfterPublish Strategy

- 从未发布：后端返回 `neverPublished`。
- 发布成功：后端返回 `published`，`latestPublishedVersion` 更新。
- 发布后编辑并保存：后端 `SaveSchemaAsync` 将已发布资源置为 `changedAfterPublish`。
- 本地未保存：前端显示 dirty，不写后端 publishStatus。
- 再次发布：后端回到 `published`。
- rollback：后端返回 draft + `changedAfterPublish` 或 `neverPublished`。

## 11. Impact Summary

Impact summary 复用真实 `GET /impact`，展示 referenceCount、breakingChangeCount、高/中/低影响数量与引用预览。若 impact API 失败，Dialog 展示错误并禁用 Publish，不伪造 impact。

## 12. Error Mapping

| status/code | UI 行为 | 是否关闭 Dialog | 是否更新 resource |
|---|---|---|---|
| network | 显示“微流服务不可用，请检查后端服务或网络” | 否 | 否 |
| 401 | 显示登录失效 | 否 | 否 |
| 403 | 显示无权限 | 否 | 否 |
| 404 | 显示微流或版本不存在 | 否 | 否 |
| 409 | 显示版本冲突/发布冲突 | 否 | 否 |
| 422 | 显示发布校验失败并打开 Problems | 否 | 否 |
| 500 | 显示服务异常和 traceId | 否 | 否 |
| unknown | 显示原始 message 和 traceId | 否 | 否 |

## 13. Permission / Audit

前端按 `resource.permissions.canPublish`、archived/readonly 禁用发布；401/403 分流展示。后端当前控制器 `[AllowAnonymous]` 且资源级 workspace/tenant 校验不足，作为本轮发布风险记录。

Audit 方面当前没有独立 audit log API；发布记录保留 `PublishedBy/PublishedAt/CreatedBy/CreatedAt`，前端在版本历史中展示这些字段，不做本地假审计。

## 14. Verification

自动验证：

- 待执行前端 TypeScript/build 检查。
- 待执行后端 build 检查。

手工验收清单覆盖：打开目标页、打开真实微流 A、Validate、Publish、Version History、dirty 后 Save & Publish、validation error 阻断、422/409/403 错误展示、rollback 如支持、A/B/C 快速切换、确认不使用 localStorage/local adapter、不展示 sampleOrderProcessingMicroflow。
