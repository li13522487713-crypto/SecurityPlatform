# Microflow API Contract Current

本文件为当前仓库源码审计结果，不代表目标发布契约。用户列出的 `apps/...`、`packages/...` 路径在当前仓库实际落点为 `src/frontend/apps/...` 与 `src/frontend/packages/...`。

## Current Envelope / Error Model

| 项 | 当前源码 | 结论 |
|---|---|---|
| 成功 envelope | `src/backend/Atlas.AppHost/Microflows/Controllers/MicroflowApiControllerBase.cs` | `MicroflowOk(data)` 返回 `MicroflowApiResponse<T>.Ok(data, traceId)` 并写 `X-Trace-Id` |
| 异常 envelope | `src/backend/Atlas.AppHost/Microflows/Infrastructure/MicroflowApiExceptionFilter.cs` | 捕获异常后返回 `MicroflowApiResponse<object>.Fail(mapped.Error, traceId)` |
| 错误码映射 | `src/backend/Atlas.Application.Microflows/Exceptions/MicroflowExceptionMapper.cs`; `src/frontend/packages/mendix/mendix-studio-core/src/microflow/adapter/http/microflow-api-error.ts` | 后端映射 `MicroflowApiErrorCode.*`；前端映射 401/403/404/409/422/500 |
| Auth | `MicroflowResourceController.cs`; `MicroflowMetadataController.cs` | 两个 Controller 当前均 `[AllowAnonymous]` |
| Header context | `HttpMicroflowRequestContextAccessor.cs`; `microflow-api-client.ts` | 前端自动写 `X-Workspace-Id`、`X-Tenant-Id`、可选 `X-User-Id`；后端从 header/claim 读取 |
| API version | `MicroflowResourceController.cs`; `MicroflowMetadataController.cs` | 当前路由为 `/api/microflows`、`/api/microflow-metadata`，无 `api/v1` 前缀 |

## API Table

| API | Controller | Service | Request DTO | Response DTO | Status Codes | Error Codes | Auth/Tenant/Workspace | 前端调用方 | 当前问题 |
|---|---|---|---|---|---|---|---|---|---|
| GET `/api/microflows?workspaceId=&moduleId=` | `MicroflowResourceController.GetPaged` | `IMicroflowResourceService.ListAsync`; `MicroflowResourceService.ListAsync` | `ListMicroflowsRequestDto` | `MicroflowApiPageResult<MicroflowResourceDto>` | 200; filter 兜底异常 | exception filter 映射 | `[AllowAnonymous]`; workspace query/header；tenant 来自 context；repository 按 WorkspaceId/TenantId/ModuleId filter | `http-resource-adapter.ts` `listMicroflows`; `app-explorer.tsx` `loadMicroflows` | 无版本前缀；workspace ownership 源码中未发现；目标页 moduleId 仍 sample |
| POST `/api/microflows` | `MicroflowResourceController.Create` | `IMicroflowResourceService.CreateAsync`; `MicroflowResourceService.CreateAsync` | `CreateMicroflowRequestDto` 包含 `MicroflowCreateInputDto` | `MicroflowResourceDto` | 200; 409; 422; 500 | `MICROFLOW_NAME_DUPLICATED`; `MICROFLOW_VALIDATION_FAILED`; `MICROFLOW_STORAGE_ERROR` | `[AllowAnonymous]`; body/header workspace；tenant/user 来自 context | `http-resource-adapter.ts` `createMicroflow`; `CreateMicroflowModal.tsx` via App Explorer | `moduleId` 由目标页 sample module 提供；权限缺 |
| GET `/api/microflows/{id}` | `MicroflowResourceController.GetById` | `MicroflowResourceService.GetAsync` | path `id` | `MicroflowResourceDto` | 200; 404 | `MICROFLOW_NOT_FOUND` | `[AllowAnonymous]`; repository `GetByIdAsync` 未按 workspace/tenant 限定 | `StudioEmbeddedMicroflowEditor.tsx`; `MendixMicroflowEditorPage.tsx`; `editor-save-bridge.ts` | ID 读取未见 workspace ownership 校验 |
| PATCH `/api/microflows/{id}` | `MicroflowResourceController.Update` | `MicroflowResourceService.UpdateAsync` | `UpdateMicroflowResourceRequestDto` / `MicroflowResourcePatchDto` | `MicroflowResourceDto` | 200; 404; 409; 422 | validation/name conflict/storage | `[AllowAnonymous]`; context only for audit fields | `http-resource-adapter.ts` `updateMicroflow` | 权限/ownership 缺 |
| POST `/api/microflows/{id}/rename` | `MicroflowResourceController.Rename` | `MicroflowResourceService.RenameAsync` | `RenameMicroflowRequestDto` | `MicroflowResourceDto` | 200; 404; 409; 422 | name duplicated/validation | `[AllowAnonymous]` | `http-resource-adapter.ts` `renameMicroflow`; `app-explorer.tsx` | rename 后 references 稳定依赖 targetMicroflowId；权限缺 |
| POST `/api/microflows/{id}/duplicate` | `MicroflowResourceController.Duplicate` | `MicroflowResourceService.DuplicateAsync` | `DuplicateMicroflowRequestDto` | `MicroflowResourceDto` | 200; 404; 409; 422 | name duplicated/validation | `[AllowAnonymous]` | `http-resource-adapter.ts`; `app-explorer.tsx` | 目标 moduleId 可来自 sample/输入；权限缺 |
| DELETE `/api/microflows/{id}` | `MicroflowResourceController.Delete` | `MicroflowResourceService.DeleteAsync`; reference repository | path `id` | `DeleteMicroflowResponseDto` | 200; 404; 409 | likely `MICROFLOW_REFERENCE_BLOCKED`; not found | `[AllowAnonymous]` | `http-resource-adapter.ts`; `app-explorer.tsx` | 前端有引用预检查；后端 ownership/权限缺 |
| GET `/api/microflows/{id}/schema` | `MicroflowResourceController.GetSchema` | `MicroflowResourceService.GetSchemaAsync` | path `id` | `GetMicroflowSchemaResponseDto` | 200; 404; 400 | not found/schema invalid | `[AllowAnonymous]` | `http-runtime-adapter.ts` `loadMicroflow`; `StudioEmbeddedMicroflowEditor.tsx` | workspace/tenant ownership 未按 id 校验 |
| PUT `/api/microflows/{id}/schema` | `MicroflowResourceController.SaveSchema` | `MicroflowResourceService.SaveSchemaAsync` | `SaveMicroflowSchemaRequestDto` | `SaveMicroflowSchemaResponseDto` | 200; 400; 404; 409 | schema invalid/version conflict/not found | `[AllowAnonymous]` | `http-resource-adapter.ts`; `editor-save-bridge.ts`; `http-runtime-adapter.ts` | 前端 conflict UX 不完整；权限缺 |
| POST `/api/microflows/{id}/validate` | `MicroflowResourceController.Validate` | `IMicroflowValidationService.ValidateAsync`; `MicroflowValidationService.cs` | `ValidateMicroflowRequestDto` | `ValidateMicroflowResponseDto` | 200; 400/422 | validation/schema errors | `[AllowAnonymous]` | `microflow-validation-adapter.ts`; `http-runtime-adapter.ts`; `editor/index.tsx` | save gate 未统一阻止 |
| POST `/api/microflows/{id}/publish` | `MicroflowResourceController.Publish` | `IMicroflowPublishService.PublishAsync`; `MicroflowPublishService.cs` | `PublishMicroflowApiRequestDto` | `MicroflowPublishResultDto` | 200; 400/409/422 | publish blocked/validation/conflict | `[AllowAnonymous]` | `http-resource-adapter.ts`; `http-runtime-adapter.ts`; `PublishMicroflowModal` | 审计/权限缺；version notes 为 description |
| GET `/api/microflows/{id}/references` | `MicroflowResourceController.GetReferences` | `IMicroflowReferenceService.GetReferencesAsync`; `MicroflowReferenceService.cs` | `GetMicroflowReferencesRequestDto` query | `IReadOnlyList<MicroflowReferenceDto>` | 200; 404 | not found | `[AllowAnonymous]` | `http-resource-adapter.ts`; `MicroflowReferencesDrawer`; `app-explorer.tsx` | callers 可查；callees 单独 API 未发现 |
| GET `/api/microflows/{id}/callees` | 源码中未发现 | 源码中未发现 | 源码中未发现 | 源码中未发现 | 源码中未发现 | 源码中未发现 | 源码中未发现 | 源码中未发现 | 必须标缺失 |
| GET `/api/microflow-metadata/microflows?workspaceId=&moduleId=` | `MicroflowMetadataController.GetMicroflowRefs` | `IMicroflowMetadataService.GetMicroflowRefsAsync`; `MicroflowMetadataService.cs` | `GetMicroflowRefsRequestDto` | `IReadOnlyList<MetadataMicroflowRefDto>` | 200 | metadata load/not found | `[AllowAnonymous]`; workspace query/header；tenant context | `http-metadata-adapter.ts` `getMicroflowRefs`; `MicroflowSelector.tsx` | 未按 appId；metadata seed 默认 demo workspace |
| GET `/api/microflow-metadata?workspaceId=&moduleId=` | `MicroflowMetadataController.GetCatalog` | `IMicroflowMetadataService.GetCatalogAsync` | `GetMicroflowMetadataRequestDto` | `MicroflowMetadataCatalogDto` | 200 | metadata load/not found | `[AllowAnonymous]` | `http-metadata-adapter.ts`; `MicroflowMetadataProvider` | 真实域模型来源需发布化 |
| POST `/api/microflows/{id}/test-run` | `MicroflowResourceController.TestRun` | `IMicroflowTestRunService.TestRunAsync`; `MicroflowTestRunService.cs` | `TestRunMicroflowApiRequest` | `TestRunMicroflowApiResponse` | 200; 400/404/422 | runtime/validation errors | `[AllowAnonymous]` | `http-runtime-adapter.ts`; `editor/index.tsx`; `MicroflowTestRunModal.tsx` | 权限缺；目标页 E2E 缺 |
| GET `/api/microflows/runs/{runId}` | `MicroflowResourceController.GetRun` | `IMicroflowTestRunService.GetRunSessionAsync` | path `runId` | `MicroflowRunSessionDto` | 200; 404 | not found | `[AllowAnonymous]` | `http-runtime-adapter.ts` `getMicroflowRunSession` | runId 未见 tenant/workspace ownership 校验 |
| GET `/api/microflows/runs/{runId}/trace` | `MicroflowResourceController.GetTrace` | `IMicroflowTestRunService.GetRunTraceAsync` | path `runId` | `GetMicroflowRunTraceResponse` | 200; 404 | not found | `[AllowAnonymous]` | `http-runtime-adapter.ts` `getMicroflowRunTrace`; `MicroflowTracePanel` | run trace 权限缺 |

## Additional APIs Found

| API | Controller | 当前前端调用 | 当前问题 |
|---|---|---|---|
| GET `/api/microflows/{id}/versions` | `MicroflowResourceController.ListVersions` | `http-resource-adapter.ts`; `MicroflowVersionsDrawer` | 权限缺 |
| GET `/api/microflows/{id}/versions/{versionId}` | `GetVersionDetail` | `MicroflowVersionsDrawer` | 权限缺 |
| POST `/api/microflows/{id}/versions/{versionId}/rollback` | `RollbackVersion` | `http-resource-adapter.ts` | 权限缺 |
| POST `/api/microflows/{id}/versions/{versionId}/duplicate` | `DuplicateVersion` | `http-resource-adapter.ts` | 权限缺 |
| GET `/api/microflows/{id}/impact` | `AnalyzeImpact` | publish/reference flows | 权限缺 |
| POST `/api/microflows/{id}/favorite` | `ToggleFavorite` | resource tab/runtime adapter | 权限缺 |
| POST `/api/microflows/{id}/archive` | `Archive` | resource tab/runtime adapter | 权限缺 |
| POST `/api/microflows/{id}/restore` | `Restore` | resource tab | 权限缺 |
| GET `/api/microflows/{id}/runs` | `ListRuns` | `http-runtime-adapter.ts` | 权限缺 |
| GET `/api/microflows/{id}/runs/{runId}` | `GetRunByMicroflow` | `http-runtime-adapter.ts` | 权限缺 |
| POST `/api/microflows/runs/{runId}/cancel` | `Cancel` | `http-runtime-adapter.ts` | 权限缺 |
| GET `/api/microflow-metadata/entities/{qualifiedName}` | `MicroflowMetadataController.GetEntity` | `http-metadata-adapter.ts` | 权限缺 |
| GET `/api/microflow-metadata/enumerations/{qualifiedName}` | `GetEnumeration` | `http-metadata-adapter.ts` | 权限缺 |
| GET `/api/microflow-metadata/pages` | `GetPageRefs` | `http-metadata-adapter.ts` | appId 缺 |
| GET `/api/microflow-metadata/workflows` | `GetWorkflowRefs` | `http-metadata-adapter.ts` | appId 缺 |

## DTO / Entity / Repository / DI Evidence

| 类型 | 源码路径 | 证据 | 当前问题 |
|---|---|---|---|
| Resource DTO | `src/backend/Atlas.Application.Microflows/Models/MicroflowResourceDto.cs` | `MicroflowResourceDto` 含 `id/schemaId/workspaceId/moduleId/name/status/referenceCount/permissions/schema` | permissions 是否真实按用户计算需继续审 |
| Resource API DTO | `MicroflowResourceApiDtos.cs` | list/create/patch/schema/duplicate/rename/delete DTO | `MicroflowCreateInputDto.Parameters/ReturnType/Security` 为 `JsonElement?`，强类型不足 |
| Validation DTO | `MicroflowValidationDtos.cs` | `ValidateMicroflowRequestDto/ResponseDto` | issue contract 依赖 shared `MicroflowValidationIssueDto` |
| Publish/reference DTO | `MicroflowVersionPublishDtos.cs` | publish result、version、snapshot、reference、impact | audit 字段不足 |
| Runtime DTO | `MicroflowRuntimeDtos.cs` | run session/trace/log/error/callStack | run ownership 未见 |
| Entity | `src/backend/Atlas.Domain.Microflows/Entities/MicroflowEntities.cs`; `MicroflowOperationalEntities.cs` | Resource/Snapshot/Version/PublishSnapshot/Reference/Metadata/Run 等 SqlSugar entity | 不继承统一 TenantEntity，手写 TenantId |
| Repository | `src/backend/Atlas.Infrastructure/Repositories/Microflows/*` | resource repository filter WorkspaceId/TenantId/ModuleId | `GetByIdAsync` 按 id 直取，ownership 需 service 兜底但源码未发现 |
| DI | `src/backend/Atlas.AppHost/Microflows/DependencyInjection/MicroflowBackendServiceCollectionExtensions.cs`; `src/backend/Atlas.Application.Microflows/DependencyInjection/MicroflowApplicationServiceCollectionExtensions.cs` | 注册 Microflow app services、context accessor、filters | Controller 仍 `[AllowAnonymous]` |
| Code-first 注册 | `src/backend/Atlas.Infrastructure/Services/AtlasOrmSchemaCatalog.cs` | rg 发现 Microflow entities 被 schema catalog 引用 | 未发现 migration 测试 |

## Contract Blockers

| Blocker | 证据 | 影响 |
|---|---|---|
| 无 API 版本前缀 | Controller `[Route("api/microflows")]` / `[Route("api/microflow-metadata")]` | 违反仓库 API 强约束 |
| Controller 匿名 | `MicroflowResourceController.cs`; `MicroflowMetadataController.cs` `[AllowAnonymous]` | 发布权限风险 |
| workspace ownership 未发现 | repository `GetByIdAsync(id)` 直取；服务读取 id 后未见 workspace compare | 越权读写风险 |
| `GET /api/microflows/{id}/callees` 缺失 | 源码中未发现 | Call Microflow 影响分析不完整 |
| 目标页 appId 未进 API | 前端 adapter query 只有 workspaceId/moduleId | 多 app 数据隔离不足 |
