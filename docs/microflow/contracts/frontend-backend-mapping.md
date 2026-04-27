# 前端 Adapter → HTTP API 映射（冻结）

> **说明**：`MicroflowResourceAdapter` / `MicroflowRuntimeAdapter` / `MicroflowMetadataAdapter` 的**方法签名**以 TypeScript 接口为准，返回**业务 DTO**；`MicroflowApiResponse` 由 **HTTP 客户端层** 解析 Envelope 后剥除。

第 31 轮起，HTTP 映射统一由 `mendix-studio-core` 的 `createMicroflowAdapterBundle({ mode: "http", apiBaseUrl, workspaceId, tenantId, currentUser })` 创建；`app-web` 只传配置，不直接实现 fetch、mock、localStorage、metadata、validation 或 runtime 逻辑。

第 32 轮起，生产 runtime policy 固定为 `defaultMode=http` 且禁止 `mock/local/enableMockFallback/local validation`。后端不可用时 HTTP adapter 抛统一错误，UI 展示服务未连接或 API 错误，不静默展示 mock/local 数据。

第 33 轮起，HTTP 错误统一抛 `MicroflowApiException`：401/403 会触发宿主回调，404/409/422/5xx/network 会映射到 `MicroflowApiErrorCode`。`error.validationIssues` 由编辑器合并进 ProblemPanel；Publish blocked 留在 PublishModal 内展示；Runtime API 错误进入 DebugPanel 服务错误态。

第 34 轮起，Contract Mock 使用 MSW 拦截上述 HTTP 请求，返回与后端契约一致的 `MicroflowApiResponse<T>`。该模式不是 `mock/local` adapter：`app-web` 仍传 `mode=http` 与 `apiBaseUrl`，不读取 mock store、不 import handler。

## ResourceAdapter

| 方法 | HTTP | 请求 | 响应 data |
|------|------|------|-----------|
| `listMicroflows` | `GET /api/microflows` | `ListMicroflowsRequest` as query | `MicroflowApiPageResult<MicroflowResource>`（客户端常映射为 `MicroflowResourceListResult`） |
| `getMicroflow` | `GET /api/microflows/{id}` | — | `MicroflowResource` |
| `createMicroflow` | `POST /api/microflows` | `CreateMicroflowRequest` | `MicroflowResource` |
| `updateMicroflow` | `PATCH /api/microflows/{id}` | `UpdateMicroflowResourceRequest` | `MicroflowResource` |
| `saveMicroflowSchema` | `PUT /api/microflows/{id}/schema` | `SaveMicroflowSchemaRequest` + 第三参 `SaveMicroflowSchemaOptions` 对齐 | `SaveMicroflowSchemaResponse`（可再合并入 `getMicroflow` 的缓存策略，由产品决定） |
| `duplicateMicroflow` | `POST /api/microflows/{id}/duplicate` | `DuplicateMicroflowRequest` | `MicroflowResource` |
| `renameMicroflow` | `POST /api/microflows/{id}/rename` | `RenameMicroflowRequest` | `MicroflowResource` |
| `toggleFavorite` | `POST /api/microflows/{id}/favorite` | `ToggleFavoriteMicroflowRequest` | `MicroflowResource` |
| `archiveMicroflow` | `POST /api/microflows/{id}/archive` | — | `MicroflowResource` |
| `restoreMicroflow` | `POST /api/microflows/{id}/restore` | — | `MicroflowResource` |
| `deleteMicroflow` | `DELETE /api/microflows/{id}` | — | `{ id }` |
| `publishMicroflow` | `POST /api/microflows/{id}/publish` | `PublishMicroflowApiRequest` | `MicroflowPublishResult` |
| `getMicroflowReferences` | `GET /api/microflows/{id}/references` | `GetMicroflowReferencesRequest` 作为 query | `MicroflowReference[]` |
| `getMicroflowVersions` | `GET /api/microflows/{id}/versions` | — | `MicroflowVersionSummary[]` |
| `getMicroflowVersionDetail` | `GET /api/microflows/{id}/versions/{versionId}` | — | `MicroflowVersionDetail` |
| `rollbackMicroflowVersion` | `POST /api/.../rollback` | `RollbackMicroflowVersionRequest` | `MicroflowResource` |
| `duplicateMicroflowVersion` | `POST /api/.../duplicate` | `DuplicateMicroflowVersionRequest` | `MicroflowResource` |
| `compareMicroflowVersion` | `GET /api/.../compare-current` | — | `MicroflowVersionDiff` |
| `analyzeMicroflowPublishImpact` | `GET /api/microflows/{id}/impact` | `AnalyzeMicroflowImpactRequest` 或对齐 `MicroflowPublishInput.version` 等 | `MicroflowPublishImpactAnalysis` |

第 37 轮后端已实现 ResourceAdapter 的资源 CRUD 与 Schema 保存/加载真实 DB 路径。第 38 轮已实现 publish / versions / rollback / duplicate version / compare-current / impact 的真实后端路径；references 仍是基础表读取，完整引用分析留第 41 轮。Schema 保存通过 `PUT /api/microflows/{id}/schema` 新增快照，已发布资源保存后 `publishStatus=changedAfterPublish`，PublishModal 可通过 impact API 决定是否要求 `confirmBreakingChanges`。

## RuntimeAdapter

| 方法 | HTTP | 说明 |
|------|------|------|
| `validateMicroflow` | `POST /api/microflows/{id}/validate` | 与 REST `ValidateMicroflowRequest` 对齐；`@atlas/microflow` 内旧 client 的 `ValidateMicroflowRequest` 仅 schema，接入网关时需包一层 `mode`。 |
| `testRunMicroflow` | `POST /api/microflows/{id}/test-run` | `TestRunMicroflowApiRequest`；响应取 `session`。 |
| `cancelMicroflowRun` | `POST /api/microflows/runs/{runId}/cancel` | — |
| `getMicroflowRunTrace` | `GET /api/microflows/runs/{runId}/trace` | 返回 `trace[]` 或从 `GetMicroflowRunTraceResponse` 拆 `trace`（若客户端已返回合并结构）。 |
| `toRuntimeDto` | 本地/边缘 | 无需 HTTP；`MicroflowRuntimeDto` 不持久化为 FlowGram。 |

## MicroflowMetadataAdapter

前端**生产路径**通过 Adapter / `MicroflowMetadataProvider` 获取 `MicroflowMetadataCatalog`；不得在生产组件、校验器、表达式与变量模块中直接 `import` mock catalog 或 `mockEntities`。同步桥接仅使用 `getDefaultMockMetadataCatalog()`（测试与过渡工具），新代码应 `await adapter.getMetadataCatalog()`。

| 方法 | HTTP | 说明 |
|------|------|------|
| `getMetadataCatalog` / `refreshMetadataCatalog` | `GET /api/microflow-metadata` | 响应为 catalog + 必填 `updatedAt`。 |
| `getEntity` | `GET /api/microflow-metadata/entities/{qualifiedName}` | — |
| `getEnumeration` | `GET /api/microflow-metadata/enumerations/{qualifiedName}` | — |
| `getMicroflowRefs` | `GET /api/microflow-metadata/microflows` 或 本地过滤 catalog | — |

第 39 轮后端已实现真实 HTTP MetadataAdapter 所需路径。`MicroflowRef` 由 `MicroflowResource` 表动态生成；实体、关联、枚举、connectors 来自 `MicroflowMetadataCache` 或后端 seed catalog；pages / workflows 第一版可返回空数组。生产路径仍不得 fallback 到前端 mock metadata。

## 错误码

见 `api-error-code-contract.md`；`MICROFLOW_*` 与 `MicroflowApiError` 一致。

## 本地 adapter

- `local-microflow-resource-adapter` 直接返回 DTO，**不**包 `MicroflowApiResponse`；`listMicroflows` 在提供 `pageIndex`+`pageSize` 时给出 `hasMore`；`tags` 为 OR 语义；`getMicroflowReferences` 支持 query 过滤。
- Contract Mock 位于 `mendix-studio-core/src/microflow/contracts/mock-api`，只服务 development/test/contract；生产路径不启动 MSW。
