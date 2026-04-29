# Microflow API Contract Current

本文件为当前仓库源码审计结果（2026-04-29 P0-1 重新生成）。所有路径以仓库实际源码为准。

## Current Envelope / Error Model

| 项 | 当前源码 | 结论 |
|---|---|---|
| 成功 envelope | `src/backend/Atlas.AppHost/Microflows/Controllers/MicroflowApiControllerBase.cs` | `MicroflowOk(data)` 返回 `MicroflowApiResponse<T>.Ok(data, traceId)` 并写 `X-Trace-Id` |
| 异常 envelope | `src/backend/Atlas.AppHost/Microflows/Infrastructure/MicroflowApiExceptionFilter.cs` | 捕获异常后返回 `MicroflowApiResponse<object>.Fail(mapped.Error, traceId)` |
| 错误码映射 | `src/backend/Atlas.Application.Microflows/Exceptions/MicroflowExceptionMapper.cs`; `src/frontend/packages/mendix/mendix-studio-core/src/microflow/adapter/http/microflow-api-error.ts` | 后端映射 `MicroflowApiErrorCode.*`；前端映射 401/403/404/409/422/500 |
| Auth | `MicroflowApiControllerBase.cs` | 基类带 `[Authorize]` + `MicroflowApiExceptionFilter` + `MicroflowProductionGuardFilter` + `MicroflowWorkspaceOwnershipFilter`；所有子控制器默认登录态校验 |
| Header context | `HttpMicroflowRequestContextAccessor.cs`; `microflow-api-client.ts` | 前端写 `X-Workspace-Id`、`X-Tenant-Id`、可选 `X-User-Id`；后端从 header/JWT claim 解析 |
| API version | `MicroflowResourceController.cs`; `MicroflowMetadataController.cs`; `MicroflowFoldersController.cs`; `MicroflowAppAssetsController.cs`; `MicroflowRuntimeMetadataController.cs` | 全部以 `api/v1` 为前缀 |

## API Table

| API | Controller | Service | Request DTO | Response DTO | Status Codes | Error Codes | Auth/Tenant/Workspace | 前端调用方 | 当前问题 |
|---|---|---|---|---|---|---|---|---|---|
| GET `/api/v1/microflows?workspaceId=&moduleId=` | `MicroflowResourceController.GetPaged` | `MicroflowResourceService.ListAsync` | `ListMicroflowsRequestDto` | `MicroflowApiPageResult<MicroflowResourceDto>` | 200 | exception filter 映射 | `[Authorize]` + Workspace ownership filter；context tenant；repository filter 按 WorkspaceId/TenantId/ModuleId | `http-resource-adapter.ts` `listMicroflows`; App Explorer | 列表 `TenantId` 列允许 NULL；权限码缺失（仅工作区成员校验） |
| POST `/api/v1/microflows` | `Create` | `MicroflowResourceService.CreateAsync` | `CreateMicroflowRequestDto` | `MicroflowResourceDto` | 200/409/422/500 | `MICROFLOW_NAME_DUPLICATED`; `MICROFLOW_VALIDATION_FAILED`; `MICROFLOW_STORAGE_ERROR` | 同上 | `http-resource-adapter.ts`; `CreateMicroflowModal.tsx` | 缺细粒度 RBAC（write）；缺 audit |
| GET `/api/v1/microflows/{id}` | `GetById` | `MicroflowResourceService.GetAsync` | path id | `MicroflowResourceDto` | 200/404 | `MICROFLOW_NOT_FOUND` | `[Authorize]`；ownership filter 通过 `id` 反查资源所属 workspace 校验 | `MicroflowResourceEditorHost`; `editor-save-bridge` | 服务层 `LoadResourceAsync` 未显式 `EnsureScoped`，依赖 filter（P0-8 修复） |
| PATCH `/api/v1/microflows/{id}` | `Update` | `MicroflowResourceService.UpdateAsync` | `UpdateMicroflowResourceRequestDto` | `MicroflowResourceDto` | 200/404/409/422 | name/validation/storage | 同上 | `http-resource-adapter.ts` | 缺 RBAC；缺 audit |
| POST `/api/v1/microflows/{id}/rename` | `Rename` | `RenameAsync` | `RenameMicroflowRequestDto` | `MicroflowResourceDto` | 200/404/409/422 | name conflict/validation | 同上 | `http-resource-adapter.ts`; App Explorer | rename 引用稳定按 targetMicroflowId；缺 audit |
| POST `/api/v1/microflows/{id}/duplicate` | `Duplicate` | `DuplicateAsync` | `DuplicateMicroflowRequestDto` | `MicroflowResourceDto` | 200/404/409/422 | name conflict/validation | 同上 | `http-resource-adapter.ts` | 缺 audit |
| DELETE `/api/v1/microflows/{id}` | `Delete` | `DeleteAsync`; reference repo | path id | `DeleteMicroflowResponseDto` | 200/404/409 | `MICROFLOW_REFERENCE_BLOCKED`; not found | 同上 | `http-resource-adapter.ts`; App Explorer | reference 阻断已存在；缺 audit |
| GET `/api/v1/microflows/{id}/schema` | `GetSchema` | `GetSchemaAsync` | path id | `GetMicroflowSchemaResponseDto` | 200/404/400 | not found/schema invalid | 同上 | `http-runtime-adapter.ts` `loadMicroflow` | 缺 audit |
| PUT `/api/v1/microflows/{id}/schema` | `SaveSchema` | `SaveSchemaAsync` | `SaveMicroflowSchemaRequestDto` | `SaveMicroflowSchemaResponseDto` | 200/400/404/409 | `MICROFLOW_SCHEMA_INVALID`; version conflict | 同上 | `http-resource-adapter.ts`; `editor-save-bridge.ts` | 缺 audit |
| POST `/api/v1/microflows/{id}/validate` | `Validate` | `MicroflowValidationService.ValidateAsync` | `ValidateMicroflowRequestDto` | `ValidateMicroflowResponseDto` | 200/400/422 | validation/schema errors | 同上 | `microflow-validation-adapter.ts`; `editor/index.tsx` | save gate 由前端 dirty/validate flow 控制；errors 字段定位仍待统一（P1-5） |
| POST `/api/v1/microflows/{id}/publish` | `Publish` | `MicroflowPublishService.PublishAsync` | `PublishMicroflowApiRequestDto` | `MicroflowPublishResultDto` | 200/400/409/422 | publish blocked/validation/conflict | 同上 | `http-runtime-adapter.ts`; `PublishMicroflowModal` | 缺 unpublish；缺 audit |
| GET `/api/v1/microflows/{id}/impact` | `AnalyzeImpact` | `MicroflowPublishService.AnalyzeImpactAsync` | query | `MicroflowPublishImpactAnalysisDto` | 200/404 | not found | 同上 | `PublishMicroflowModal` | mock 文案残留前端待替换（P1-4） |
| GET `/api/v1/microflows/{id}/references` | `GetReferences` | `MicroflowReferenceService.GetReferencesAsync` | query | `IReadOnlyList<MicroflowReferenceDto>` | 200/404 | not found | 同上 | `http-resource-adapter.ts`; `MicroflowReferencesDrawer` | callees / callers 专用 API 缺失（P0-7） |
| POST `/api/v1/microflows/{id}/references/rebuild` | `RebuildReferences` | `MicroflowReferenceService.RebuildAsync` | path id | `MicroflowReferenceRebuildResultDto` | 200/404 | not found | 同上 | 后台/调试工具 | 仅 admin |
| GET `/api/v1/microflow-metadata/microflows?workspaceId=&moduleId=` | `MicroflowMetadataController.GetMicroflowRefs` | `MicroflowMetadataService.GetMicroflowRefsAsync` | `GetMicroflowRefsRequestDto` | `IReadOnlyList<MetadataMicroflowRefDto>` | 200 | metadata load | 同上 | `http-metadata-adapter.ts`; `MicroflowSelector.tsx` | 未按 appId（产品需要时增强） |
| GET `/api/v1/microflow-metadata?workspaceId=&moduleId=` | `GetCatalog` | `MicroflowMetadataService.GetCatalogAsync` | `GetMicroflowMetadataRequestDto` | `MicroflowMetadataCatalogDto` | 200 | metadata load | 同上 | `http-metadata-adapter.ts`; `MicroflowMetadataProvider` | 真实域模型来源需发布化 |
| POST `/api/v1/microflows/{id}/test-run` | `TestRun` | `MicroflowTestRunService.TestRunAsync` → `IMicroflowRuntimeEngine.RunAsync` | `TestRunMicroflowApiRequest` | `TestRunMicroflowApiResponse` | 200/400/404/422 | runtime/validation errors | 同上 | `http-runtime-adapter.ts`; `editor/index.tsx`; `MicroflowTestRunModal.tsx` | RunSession 表 ownership 列待加（P0-3） |
| GET `/api/v1/microflows/runs/{runId}` | `GetRun` | `GetRunSessionAsync(runId)` | path runId | `MicroflowRunSessionDto` | 200/404 | not found | 仅按 runId 查询，**未校验 ownership**（P0-3 修复） | `http-runtime-adapter.ts` `getMicroflowRunSession` | IDOR 风险 |
| GET `/api/v1/microflows/runs/{runId}/trace` | `GetTrace` | `GetRunTraceAsync(runId)` | path runId | `GetMicroflowRunTraceResponse` | 200/404 | not found | 仅按 runId 查询，**未校验 ownership**（P0-3 修复） | `http-runtime-adapter.ts`; `MicroflowTracePanel` | IDOR 风险 |
| POST `/api/v1/microflows/runs/{runId}/cancel` | `Cancel` | `CancelAsync(runId)` | path runId | `CancelMicroflowRunResponse` | 200/404 | not found | 仅按 runId，**未校验 ownership** + 仅改 DB 状态不中断引擎（P0-3 + P0-6 修复） | `http-runtime-adapter.ts` | IDOR + 取消语义弱 |
| GET `/api/v1/microflows/{id}/runs` | `ListRuns` | `ListRunsAsync(resourceId, …)` | query | `MicroflowApiPageResult<MicroflowRunSessionListItemDto>` | 200/404 | not found | 同上 | `http-runtime-adapter.ts` | OK |

## Additional APIs Found

| API | Controller | 前端调用 | 当前状态 |
|---|---|---|---|
| GET `/api/v1/microflows/{id}/versions` | `ListVersions` | `MicroflowVersionsDrawer` | OK |
| GET `/api/v1/microflows/{id}/versions/{versionId}` | `GetVersionDetail` | `MicroflowVersionsDrawer` | OK |
| POST `/api/v1/microflows/{id}/versions/{versionId}/rollback` | `RollbackVersion` | `http-resource-adapter.ts` | OK；rollback 生成新 draft snapshot |
| POST `/api/v1/microflows/{id}/versions/{versionId}/duplicate` | `DuplicateVersion` | `http-resource-adapter.ts` | OK |
| GET `/api/v1/microflows/{id}/versions/{versionId}/compare-current` | `CompareCurrent` | `MicroflowVersionsDrawer` | OK |
| POST `/api/v1/microflows/{id}/favorite` | `ToggleFavorite` | resource tab/runtime adapter | OK |
| POST `/api/v1/microflows/{id}/archive` | `Archive` | resource tab/runtime adapter | OK |
| POST `/api/v1/microflows/{id}/restore` | `Restore` | resource tab | OK |
| GET `/api/v1/microflows/health` | `GetHealth` | readiness | OK |
| GET `/api/v1/microflows/runtime/health` | `GetRuntimeHealth` | readiness | OK |
| GET `/api/v1/microflows/storage/health` | `GetStorageHealth` | readiness | OK |
| GET `/api/v1/microflow-metadata/health` | `MicroflowMetadataController.GetHealth` | readiness | OK |
| POST `/api/v1/microflows/runtime/navigate` | `MicroflowResourceController.Navigate` / `IMicroflowFlowNavigator` | 编辑器导航工具 | OK |
| POST `/api/v1/microflows/runtime/plan` | `LoadExecutionPlan` | 调试 | OK |
| POST `/api/v1/microflows/runtime/metadata/resolve` | `MicroflowRuntimeMetadataController.Resolve` | 编辑器属性面板 | 接受 body 覆盖 securityContext，需收紧（P0-8 处理） |
| GET `/api/v1/microflow-metadata/entities/{qualifiedName}` | `GetEntity` | `http-metadata-adapter.ts` | OK |
| GET `/api/v1/microflow-metadata/enumerations/{qualifiedName}` | `GetEnumeration` | `http-metadata-adapter.ts` | OK |
| GET `/api/v1/microflow-metadata/pages` | `GetPageRefs` | `http-metadata-adapter.ts` | OK |
| GET `/api/v1/microflow-metadata/workflows` | `GetWorkflowRefs` | `http-metadata-adapter.ts` | OK |
| GET `/api/v1/microflow-folders?...` | `MicroflowFoldersController.List` 等全套 | App Explorer | OK |
| GET `/api/v1/microflow-apps/{appId}` | `MicroflowAppAssetsController.GetApp` | App Explorer | OK |

## Currently Missing (P0-7 即将补齐)

| API | 说明 |
|---|---|
| POST `/api/v1/microflows/{id}/unpublish` | 取消发布；将资源 `publishStatus` 置 `unpublished`，published snapshot 标记 inactive，写 audit |
| GET `/api/v1/microflows/{id}/callees` | 列出该微流调用了哪些其他微流（基于 reference index source 维度） |
| GET `/api/v1/microflows/{id}/callers` | 列出哪些微流/页面/工作流调用了该微流（基于 reference index target 维度） |
| POST `/api/v1/microflows/{id}/move` | 将微流迁移到指定 module/folder（同名校验、引用一致性） |

## DTO / Entity / Repository / DI Evidence

| 类型 | 源码路径 | 证据 | 当前问题 |
|---|---|---|---|
| Resource DTO | `src/backend/Atlas.Application.Microflows/Models/MicroflowResourceDto.cs` | `MicroflowResourceDto` 含 `id/schemaId/workspaceId/moduleId/name/status/referenceCount/permissions/schema` | permissions 实际计算来自 `MicroflowResourceService` 内部（前端可信展示） |
| Resource API DTO | `MicroflowResourceApiDtos.cs` | list/create/patch/schema/duplicate/rename/delete DTO | `MicroflowCreateInputDto.Parameters/ReturnType/Security` 仍 `JsonElement?`，待强类型化 |
| Validation DTO | `MicroflowValidationDtos.cs` | `ValidateMicroflowRequestDto/ResponseDto` | `MicroflowValidationIssueDto` 字段需扩展 fieldPath（P1-5） |
| Publish/reference DTO | `MicroflowVersionPublishDtos.cs` | publish result、version、snapshot、reference、impact | publish 缺 unpublish；audit 字段不足 |
| Runtime DTO | `MicroflowRuntimeDtos.cs` | run session/trace/log/error/callStack；`RuntimeErrorCode` 集中错误码 | RunSession ownership 字段待补 |
| Entity | `Atlas.Domain.Microflows/Entities/*.cs` | Resource/Snapshot/Version/PublishSnapshot/Reference/Metadata/Run 等 SqlSugar entity | 不继承 `TenantEntity`；`MicroflowRunSession`/`MicroflowRunTraceFrame` 缺 WorkspaceId/TenantId（P0-3 补） |
| Repository | `Atlas.Infrastructure/Repositories/Microflows/*` | resource repository filter WorkspaceId/TenantId/ModuleId | `GetByIdAsync` 仅按 id；缺 `EnsureScoped` 工具（P0-8 修复） |
| DI | `Atlas.AppHost/Microflows/DependencyInjection/MicroflowBackendServiceCollectionExtensions.cs`; `Atlas.Application.Microflows/DependencyInjection/MicroflowApplicationServiceCollectionExtensions.cs` | 注册 Microflow app services、context accessor、filters；DI `IMicroflowRuntimeEngine -> MicroflowRuntimeEngine` | OK |
| Code-first 注册 | `Atlas.Infrastructure/Services/AtlasOrmSchemaCatalog.cs` | Microflow entities 全量注册 | OK |

## Contract Blockers

| Blocker | 证据 | 影响 | 计划 |
|---|---|---|---|
| RunSession/Trace 仅按 runId 查询 | `MicroflowTestRunService.GetRunTraceAsync/GetRunSessionAsync/CancelAsync` | IDOR：跨 workspace 用户知 runId 即可读 trace、cancel | P0-3 修复 |
| 引擎与控制面节点覆盖不一致 | `MicroflowRuntimeEngine.ExecuteNodeAsync` 仅 6 种 kind；validation 接受更多 kind | unsupported 节点静默/失败行为不清 | P0-4 修复 |
| `ShouldEnterErrorHandler` 标志未消费 | `RestCallActionExecutor`/`ThrowExceptionActionExecutor` 等设置；`MicroflowRuntimeEngine` 主循环未消费 | error handler 分支不被触发 | P0-4 修复 |
| `PendingClientCommand` 在引擎中走 success | `ExecuteActionViaRegistryAsync` 仅判 Failed/Unsupported/ConnectorRequired | server-side 假成功 | P0-4 修复 |
| CallMicroflow 双路径 | 引擎内联 `ExecuteCallMicroflowAsync` + `CallMicroflowActionExecutor` | 行为分叉、维护成本 | P0-5 修复 |
| RunTimeoutSeconds 引擎未 enforce + cancel 不中断 | `RuntimeContext.TryStep` 只控 MaxSteps；`CancelAsync` 仅改 DB | 长跑/失控风险 | P0-6 修复 |
| 缺 unpublish/callees/callers/move 路由 | Controller 中无对应 action | 前端能力受限 | P0-7 修复 |
| Audit 缺失 | `Atlas.Application.Microflows` 无 `IAuditWriter` 调用 | 等保合规 | P0-9 修复 |
