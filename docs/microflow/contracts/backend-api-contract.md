# 微流 REST API 契约（冻结，第 21 轮）

- **类型源**：`src/frontend/packages/mendix/mendix-studio-core/src/microflow/contracts/api/*.ts`。
- **统一响应**：`MicroflowApiResponse<T>`。成功时 `success: true` 且 `data` 非空；失败时 `success: false` 且 `error` 非空。
- **不存储 FlowGram JSON**：仅 `MicroflowAuthoringSchema` / Runtime DTO / Trace 等，见 `storage-model-contract.md`。
- **分页**：`pageIndex` **1-based**；`MicroflowResourceQuery` 与 `ListMicroflowsRequest` 对齐；多选 `status` / `publishStatus` / `tags` 均为 **OR** 语义；`sortBy` 推荐：`name`、`updatedAt`、`createdAt`、`version`、`referenceCount`。
- **前端 HTTP 客户端**：`MicroflowApiClient` 统一附加 `X-Workspace-Id`、`X-Tenant-Id`、`X-User-Id`，解析 `MicroflowApiResponse<T>` 后再交给 Resource / Metadata / Runtime / Validation Adapter。

## 资源

| 方法 | 路径 | 请求 DTO | 响应 `data` |
|------|------|-----------|------------|
| GET | `/api/microflows` | `ListMicroflowsRequest`（query） | `MicroflowApiPageResult<MicroflowResource>` |
| POST | `/api/microflows` | `CreateMicroflowRequest` | `MicroflowResource` |
| GET | `/api/microflows/{id}` | — | `MicroflowResource` |
| PATCH | `/api/microflows/{id}` | `UpdateMicroflowResourceRequest` | `MicroflowResource` |
| POST | `/api/microflows/{id}/duplicate` | `DuplicateMicroflowRequest` | `MicroflowResource` |
| POST | `/api/microflows/{id}/rename` | `RenameMicroflowRequest` | `MicroflowResource` |
| POST | `/api/microflows/{id}/favorite` | `ToggleFavoriteMicroflowRequest` | `MicroflowResource` |
| POST | `/api/microflows/{id}/archive` | — | `MicroflowResource` |
| POST | `/api/microflows/{id}/restore` | — | `MicroflowResource` |
| DELETE | `/api/microflows/{id}` | — | `{ id: string }` |

## Schema

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/microflows/{id}/schema` | 返回 `GetMicroflowSchemaResponse`（`schema` 为 Authoring；可含 `migrationVersion`） |
| PUT | `/api/microflows/{id}/schema` | `SaveMicroflowSchemaRequest`（`baseVersion` 乐观锁） |
| POST | `/api/microflows/{id}/schema/migrate` | `MigrateMicroflowSchemaRequest` → `MigrateMicroflowSchemaResponse` |

## 校验

- `POST /api/microflows/{id}/validate` — `ValidateMicroflowRequest`（`mode`：`edit` | `save` | `publish` | `testRun`）→ `ValidateMicroflowResponse`。
- `publish` 模式规则更严；`testRun` 要求无 error 方可运行。

## 运行 / Trace

- `POST /api/microflows/{id}/test-run` — 可选 `schema`（草稿）；否则用已保存版本（P0 可先只支持带 `schema`）。
- `POST /api/microflows/runs/{runId}/cancel` — `{ runId, status: "cancelled" }`。
- `GET /api/microflows/runs/{runId}` — `MicroflowRunSession`。
- `GET /api/microflows/runs/{runId}/trace` — `{ runId, trace, logs }`；帧须含 `objectId` 与可解析的 `actionId` 等，见 `MicroflowTraceFrame`。

## 发布

- `POST /api/microflows/{id}/publish` — `PublishMicroflowApiRequest` → `MicroflowPublishResult`。
- 流程：加载资源 → 校验（有 error 则 `MICROFLOW_PUBLISH_BLOCKED`）→ 影响分析（高影响且未 `confirmBreakingChanges` 时阻塞）→ 创建**不可变**发布快照（含 `MicroflowAuthoringSchema`）→ 更新 `MicroflowVersionSummary` 与 `MicroflowResource` 的 `status` / `publishStatus` / `latestPublishedVersion`。

## 版本

- `GET /api/microflows/{id}/versions` — `MicroflowVersionSummary[]`。
- `GET /api/microflows/{id}/versions/{versionId}` — `MicroflowVersionDetail`（含 `MicroflowPublishedSnapshot`）。
- `POST .../rollback` — `RollbackMicroflowVersionRequest`；从快照恢复 Authoring；资源为 draft 或 `changedAfterPublish` 策略与实现约定（建议 rollback 后 `changedAfterPublish` 若曾发布过）。
- `POST .../duplicate` — 新建**草稿**资源。
- `GET .../compare-current` — `MicroflowVersionDiff`。

## 引用与影响

- `GET /api/microflows/{id}/references` — query：`GetMicroflowReferencesRequest`（`includeInactive`、`sourceType[]`、`impactLevel[]`）。
- `GET /api/microflows/{id}/impact` — `AnalyzeMicroflowImpactRequest` → `MicroflowPublishImpactAnalysis`。
- 来源类型：`microflow` | `workflow` | `page` | `form` | `button` | `schedule` | `api`（可扩展，与 `MicroflowReference` 一致）。

## 元数据

- `GET /api/microflow-metadata` — `GetMicroflowMetadataRequest`；响应为 `MicroflowMetadataCatalog` + 必填 `updatedAt`（及可选 `catalogVersion` / `version`），便于缓存。
- `GET /api/microflow-metadata/entities/{qualifiedName}` — `MetadataEntity`。
- `GET /api/microflow-metadata/enumerations/{qualifiedName}` — `MetadataEnumeration`。
- `GET /api/microflow-metadata/microflows` — `MetadataMicroflowRef[]`。

前端使用 `createHttpMicroflowMetadataAdapter({ apiBaseUrl })` 请求 `GET /api/microflow-metadata`（及可选子资源）；响应须为 `MicroflowApiResponse`，`data` 与 `MicroflowMetadataCatalog` 类型对齐。生产 UI 不直接使用 mock catalog，仅通过 Adapter / Provider 消费上述 DTO。

## 与 Adapter 的边界

- **UI / ResourceAdapter**：直接消费业务 DTO，不经 `MicroflowApiResponse`。
- **HTTP 客户端**：解析 Envelope 后返回 DTO 或抛业务异常。
- **生产配置**：`mode=http` 必须配置 `apiBaseUrl`；服务不可用时前端显示服务未连接或 API 错误，不 fallback 到 mock。
- **生产禁用 mock/local**：`MicroflowAdapterRuntimePolicy.production` 禁止 mock resource、mock metadata、mock runner、localStorage resource 与 local validation 作为主路径。

## OpenAPI

- 见同目录 `openapi-draft.yaml`。
