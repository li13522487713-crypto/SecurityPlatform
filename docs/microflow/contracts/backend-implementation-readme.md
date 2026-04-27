# 后端实现指引与 Skeleton 现状

供后端团队按**冻结契约**分阶段实现；不替代 OpenAPI/TS 类型。

## 第 35 轮 Skeleton

本仓库已新增核心微流应用模块 `src/backend/Atlas.Application.Microflows`，并由 `src/backend/Atlas.AppHost` 引用后暴露最小 API。当前只提供后端骨架、统一 Envelope、错误结构、请求上下文和联调占位服务，不包含数据库、ORM 实体、真实资源 CRUD、真实 Metadata、真实 Validation 或 Runtime。

前端联调配置：

- `adapterMode=http`
- `apiBaseUrl=/api`
- AppWeb 仍通过统一 Microflow HTTP Adapter 访问，不在页面内直接 `fetch`。

当前可用接口：

- `GET /api/microflows/health`：返回 `MicroflowApiResponse<MicroflowHealthDto>`。
- `GET /api/microflows`：返回空 `MicroflowApiPageResult<MicroflowResourceDto>`。
- `GET /api/microflows/{id}`：当前无数据库，统一返回 `404 MICROFLOW_NOT_FOUND`。
- `GET /api/microflow-metadata`：返回空 catalog，`version=backend-skeleton`。
- `POST /api/microflows/{id}/validate`：返回空 issues 和 0 计数 summary。
- `POST /api/microflows/{id}/test-run`：返回 `503 MICROFLOW_SERVICE_UNAVAILABLE`。
- `GET /api/microflows/storage/health`：返回微流存储表存在性诊断。

第 36 轮已补充 DB / Repository 基础：

- 新增 `Atlas.Domain.Microflows`，实体通过 `AtlasOrmSchemaCatalog.EnsureRuntimeSchema(db)` 参与现有 SqlSugar CodeFirst 初始化。
- 新增 Repository 接口与 SqlSugar 实现，Controller 仍不直接操作 ORM。
- `GET /api/microflows` 与 `GET /api/microflows/{id}` 已从 Repository 读取 DB 数据。
- `GET /api/microflow-metadata` 优先读取 `MicroflowMetadataCache` 最新行，缺失时返回空 catalog。
- 开发 seed 通过 `Microflows:SeedData:Enabled=true` 开启，且只在 Development 环境插入 `mf-seed-blank`。

第 37 轮已补充 Resource CRUD + Schema Save/Load：

- `GET /api/microflows`：真实 DB 列表查询，支持分页、搜索、状态、发布状态、收藏、owner、module、tags、updatedAt 范围与排序。
- `POST /api/microflows`：创建资源与首个 `MicroflowSchemaSnapshot`。
- `GET /api/microflows/{id}`：读取资源详情，并附带当前 AuthoringSchema。
- `PATCH /api/microflows/{id}`：更新资源元数据，不覆盖历史 SchemaSnapshot。
- `GET /api/microflows/{id}/schema`：读取当前 SchemaSnapshot。
- `PUT /api/microflows/{id}/schema`：保存 AuthoringSchema，并新增 SchemaSnapshot；已发布资源保存后 `publishStatus=changedAfterPublish`。
- `POST /duplicate`、`/rename`、`/favorite`、`/archive`、`/restore` 与 `DELETE` 已实现真实持久化。

第 38 轮已补充 Version / Publish Snapshot：

- `POST /api/microflows/{id}/publish`：执行基础版本号校验、AuthoringSchema 校验、impact analysis，占用 high breaking changes 时要求 `confirmBreakingChanges=true`。
- 发布会在事务中新增 `MicroflowSchemaSnapshot`、不可变 `MicroflowPublishSnapshot` 与 `MicroflowVersion`，并更新资源 `status=published`、`publishStatus=published`、`version`、`latestPublishedVersion`。
- `GET /api/microflows/{id}/versions` 与 `GET /api/microflows/{id}/versions/{versionId}` 已从 DB 读取版本列表、发布快照和当前 diff。
- `POST /rollback` 从历史版本 snapshot 创建新的 current schema snapshot，不修改历史快照；资源回到 `draft`，已发布过则 `publishStatus=changedAfterPublish`。
- `POST /duplicate` 从历史版本复制为新草稿资源，不复制运行记录、trace、log 或 references。
- `GET /compare-current` 与 `GET /impact` 使用基础 JSON diff：参数删除/类型变更、返回类型变更、暴露 URL path 变更、对象/flow 增删。

并发与删除策略：

- `baseVersion` 可匹配 `resource.version`、`resource.concurrencyStamp`、当前 snapshot id 或 snapshot schemaVersion；不匹配返回 `MICROFLOW_VERSION_CONFLICT`。
- 删除采用物理删除资源行、保留历史 SchemaSnapshot 的第一版策略；后续如需审计可改为软删除。
- 保存前做最小 AuthoringSchema 结构校验，拒绝根级 `nodes`、`edges`、`workflowJson`、`flowgram` 等 FlowGram-only JSON。

请求上下文支持 `X-Workspace-Id`、`X-Tenant-Id`、`X-User-Id`、`X-Locale`、`X-Trace-Id`，并会把 traceId 写入 Envelope 与 `X-Trace-Id` 响应头。`.http` 示例见 `src/backend/Atlas.AppHost/Bosch.http/MicroflowBackend.http`。

后续建议：

- 第 39 轮：Metadata API。
- 第 40 轮：Validation API。
- 第 41 轮：References / Impact Analysis 深化。
- 第 42 轮：TestRun Mock API + Trace 存储。

当前实现仍坚持不保存 FlowGram JSON，后端只围绕 `MicroflowAuthoringSchema` / Runtime DTO / Trace 等契约概念展开。

## 建议启动顺序

1. **资源 CRUD** + 列表筛选分页（`MicroflowResource` 行 + DTO 映射)。
2. **Schema GET/PUT** + `baseVersion` 乐观锁 + 审计字段。
3. **版本**与**发布不可变快照**（`MicroflowPublishedSnapshot` 行，仅 Authoring JSON）。
4. **Metadata** 全量/缓存行（`updatedAt` / `version` 支持客户端缓存)。
5. **Validation** 端点，与 `MicroflowValidationIssue` 同构。
6. **TestRun 模拟器** + RunSession + Trace/Log 从表，REST 先全量再考虑流式。

## P0 可先交付

- 优先级：消费 `toRuntimeDto().p0RuntimeActionBlocks` 与 `toExecutionPlan().nodes[].p0ActionRuntime` 中的 **P0 强类型 DTO**（非 `unknown` blob）；`MicroflowGenericAction` 仅对应 P1/P2 modeledOnly。
- 变量：消费 `toRuntimeDto().variables` 或 `toExecutionPlan().variableDeclarations`。后者已拆出 `actionOutputs`、`loopVariables`、`systemVariables`、`errorContextVariables`、`variableScopes` 与 `variableDiagnostics`，不含 FlowGram/UI-only 信息。
- 作用域：按 `visibility=definite/maybe/unavailable` 实现发布期检查；`maybe` 可先 warning，`unavailable` 与类型不匹配应阻断保存/发布。
- ErrorHandler：`$latestError` 仅在 error handler scope；RestCall error handler 额外支持 `$latestHttpResponse`，WebService error handler 额外支持 `$latestSoapFault`。
- Flow：消费 Runtime DTO / ExecutionPlan 的 control flows；`AnnotationFlow` 不执行。Decision/ObjectType 读取 `caseValues`，ErrorHandler 读取 `isErrorHandler=true`，不要依赖 FlowGram port 或视觉顺序。
- 后端真实 Runtime 建议从 `ExecutionPlanLoader` 开始：读取 `MicroflowExecutionPlan.nodes/flows/normalFlows/decisionFlows/errorHandlerFlows/loopCollections`，不要直接解释 FlowGram JSON。
- 表达式：后端 Runtime 可按 `runtime-expression-contract.md` 实现 P0 子集；禁止把前端 AST 当持久化主数据，AuthoringSchema 中仍以 expression raw/text 为准。
- Validation API：建议接收 AuthoringSchema + MetadataCatalog 版本，返回同构 `MicroflowValidationIssue`，并支持 edit/save/publish/testRun mode。
- 微流资源 + Schema 存取 + 列表。
- 发布 + 只读发布快照 + 基础版本树。
- 元数据全量 `GET`。
- 与前端 mock adapter 可切换的**单租户**、单工作区。

## 可暂缓

- 引用关系的完整索引/反向搜索（可先用简表+批任务）。
- Trace 长保留与冷归档。
- 流式 trace WebSocket。

## JSON 与事务

- 大 JSON 列（`SchemaJson`）建议**压缩/哈希**（`SchemaHash`）与乐观锁/去重；事务边界：单资源 `PUT /schema` 与行级 `MicroflowVersion` 追加一致提交。
- **禁止** 依赖 FlowGram JSON；仅 Authoring 与 DTO/Trace 契约。

## 权限

- 所有写路径带 `WorkspaceId` / `TenantId` 与 `userId` 校验，与 DTO 上 `permissions` 可组合；审计列 `CreatedBy/UpdatedBy`。

## 与前端切换真实 API

1. 实现上述 REST，响应包 `MicroflowApiResponse`。
2. 前端已提供 `createHttpMicroflowResourceAdapter`、`createHttpMicroflowMetadataAdapter`、`createHttpMicroflowRuntimeAdapter` 与 `createHttpMicroflowValidationAdapter`，均通过统一 `MicroflowApiClient` 解 Envelope。
3. 宿主只需传 `adapterConfig: { mode: "http", apiBaseUrl, workspaceId, tenantId, currentUser }`；不要在 app-web 手写 fetch 或 mock/local 逻辑。
4. 元数据：实现 `GET /api/microflow-metadata`（及可选子路径），字段与 `MicroflowMetadataCatalog` 对齐；校验、表达式、变量作用域均以传入 catalog 为准，缺失时返回明确 issue，由 ProblemPanel 展示。
5. TestRun 等同理接入；后端暂未实现时，前端 http 模式会显示服务未连接/请求失败，不会静默切回 mock。
6. 生产构建默认 `mode=http`，并由前端 runtime policy 拒绝 `mock/local/enableMockFallback`；联调环境若需使用 mock API server，应保持 `mode=http`，仅切换 `apiBaseUrl`。
7. 错误响应请统一返回 `MicroflowApiResponse<T>`；401/403/404/409/422/5xx 应提供 `MicroflowApiError.code/message/traceId`。校验失败、发布阻断、试运行校验失败必须携带 `validationIssues`，前端会展示到 ProblemPanel / PublishModal。
8. 前端 Contract Mock 已覆盖全量第 21 轮路径，可作为后端实现前的契约样板；真实后端应以 `backend-api-contract.md` 和 `openapi-draft.yaml` 为准，不依赖 MSW store 或 fixture。

## 未知项

- 多区域复制与**最终一致**的 catalog 版本传播（可后续在 `metadata` 上扩展 ETag）。
