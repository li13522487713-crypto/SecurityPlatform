# 后端实现指引与 Skeleton 现状

## 第 58 轮 Runtime ErrorHandling 实现现状

- 新增 `src/backend/Atlas.Application.Microflows/Runtime/ErrorHandling`，统一承载 rollback、customWithRollback、customWithoutRollback、continue、ErrorHandlerFlow resolver、continue policy、diagnostic 与 summary。
- `AddAtlasApplicationMicroflows()` 注册 `IMicroflowErrorHandlingService`，Runner 通过服务联动 `TransactionManager`，ActionExecutor 不直接跳转错误分支。
- `RuntimeExecutionContext` 维护 error scope、`$latestError`、`$latestHttpResponse`、`$latestSoapFault` 预留与 `errorHandlingSummary`。
- ErrorEvent 后端错误码统一为 `RUNTIME_ERROR_EVENT_REACHED`，cause 保留当前 `$latestError`。
- 不新增数据库表，不改 Resource / Publish / Metadata / References / Coze 兼容接口。

## 第 57 轮 Runtime RestCall / LogMessage 实现现状

- `Atlas.Application.Microflows` 已新增 Runtime HTTP 抽象与实现：`IMicroflowRuntimeHttpClient`、`MicroflowRuntimeHttpClient`、`MicroflowRestSecurityPolicy`、`MicroflowRestRequestBuilder`、`MicroflowRestResponseHandler`、`RestCallActionExecutor`、`LogMessageActionExecutor`。
- `AddAtlasApplicationMicroflows()` 注册命名 HttpClient `microflow-runtime-rest`，禁用自动 redirect，由 Runtime 按策略手动处理。
- AppHost 可通过 `Microflow:Runtime:Rest` 绑定 `MicroflowRestExecutionOptions`：`AllowRealHttp` 默认 false，另有 `AllowPrivateNetwork`、`AllowedHosts`、`DeniedHosts`、`MaxResponseBytes`、`TimeoutSecondsDefault`、`FollowRedirects`、`MaxRedirects`、mock response 等。
- 本轮不新增数据库表；RuntimeLog 扩展字段写入已有 `MicroflowRunLog.ExtraJson`。
- 完整 ErrorHandling 事务语义留第 58 轮，本轮只保证 RestCall error trace 与 `$latestHttpResponse` 上下文变量。

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

第 43 轮联调回归修正：

- Resource 列表 `tags` 过滤按契约使用 OR 语义，与前端 `MicroflowResourceQuery.tags` 保持一致。
- `src/frontend/scripts/verify-microflow-resource-schema-integration.mjs` 可对真实 AppHost 执行 Resource / Schema 闭环验证；默认 base url 为 `http://localhost:5002`，也可通过 `MICROFLOW_API_BASE_URL` 覆盖。
- `.http` 中 `MicroflowBackend.http` 已补齐 Resource / Schema 回归段落，包括保存后读取、FlowGram JSON 拒绝、冲突、删除后 not found。

第 38 轮已补充 Version / Publish Snapshot：

- `POST /api/microflows/{id}/publish`：执行基础版本号校验、AuthoringSchema 校验、impact analysis，占用 high breaking changes 时要求 `confirmBreakingChanges=true`。
- 发布会在事务中新增 `MicroflowSchemaSnapshot`、不可变 `MicroflowPublishSnapshot` 与 `MicroflowVersion`，并更新资源 `status=published`、`publishStatus=published`、`version`、`latestPublishedVersion`。
- `GET /api/microflows/{id}/versions` 与 `GET /api/microflows/{id}/versions/{versionId}` 已从 DB 读取版本列表、发布快照和当前 diff。
- `POST /rollback` 从历史版本 snapshot 创建新的 current schema snapshot，不修改历史快照；资源回到 `draft`，已发布过则 `publishStatus=changedAfterPublish`。
- `POST /duplicate` 从历史版本复制为新草稿资源，不复制运行记录、trace、log 或 references。
- `GET /compare-current` 与 `GET /impact` 使用基础 JSON diff：参数删除/类型变更、返回类型变更、暴露 URL path 变更、对象/flow 增删。

第 48 轮已补充 Runtime ExecutionPlanLoader：

- 新增 `IMicroflowExecutionPlanLoader`、`MicroflowRuntimeDtoBuilder`、`MicroflowExecutionPlanBuilder`、`MicroflowExecutionPlanValidator`、`MicroflowActionSupportMatrix`。
- 支持从 current resource、version snapshot、inline schema 生成 `MicroflowExecutionPlan`。
- 诊断 API 为 `POST /api/microflows/runtime/plan`、`GET /api/microflows/{id}/runtime/plan`、`GET /api/microflows/{id}/versions/{versionId}/runtime/plan`。
- TestRunService 仅预热 plan loader，不改变既有 MockRuntimeRunner 行为。
- 本轮仍不实现真实 Runtime 执行器、真实 CRUD、真实 REST、完整表达式、事务引擎或 FlowNavigator。
- 自动化回归入口：`scripts/verify-microflow-execution-plan-loader.ts`。

第 49 轮已补充 Runtime FlowNavigator：

- 新增 `IMicroflowFlowNavigator`、`MicroflowFlowNavigator` 与 ExecutionPlan query helper，基于 `MicroflowExecutionPlan` 的 node/flow/loop map 做导航，避免每步全量扫描。
- 新增导航内部模型：`MicroflowNavigationOptions`、`MicroflowNavigationContext`、`MicroflowNavigationResult`、`MicroflowNavigationStep`、`MicroflowNavigationError`、`MicroflowFlowNavigatorDiagnostics`。
- 新增诊断 API：`POST /api/microflows/runtime/navigate`、`GET /api/microflows/{id}/runtime/navigate`，只读生成 plan 并 dry-run 导航，不保存 run session，不修改资源或 publishStatus。
- 支持 Start / End / ErrorEvent、SequenceFlow / Merge、Boolean / Enumeration / ObjectType Decision、ActionActivity placeholder、ErrorHandlerFlow 骨架、Loop / Break / Continue 骨架、maxSteps 与 CancellationToken。
- 本轮不执行真实 Action、不访问业务数据库、不调用 REST、不执行表达式、不实现 VariableStore / TransactionManager / CallMicroflow。
- TestRun API 保持现有 MockRuntimeRunner，不切换 FlowNavigator；避免破坏第 46～47 轮 DebugPanel 与持久化 trace 联调。
- 自动化验证入口：`scripts/verify-microflow-flow-navigator.ts`；`.http` 已补 Runtime FlowNavigator 示例。

第 50 轮已补充 Runtime VariableStore：

- 新增 `IMicroflowVariableStore`、`MicroflowVariableStore`、`MicroflowRuntimeVariableValue`、`MicroflowVariableScopeFrame`、`MicroflowVariableScopeStack`、`MicroflowVariableStoreSnapshot` 与 `MicroflowVariableStoreDiagnostic`。
- 新增 `RuntimeExecutionContext`，持有 run/resource/schema/version/mode、ExecutionPlan、VariableStore、当前节点/flow/collection/loop、call/loop/error stack、securityContext 与 diagnostics。
- 参数从 `ExecutionPlan.parameters` 初始化，系统变量支持 `$currentUser`；Loop scope 支持 iterator 与 `$currentIndex`；ErrorHandler scope 支持 `$latestError`、`$latestHttpResponse`，`$latestSoapFault` 预留。
- FlowNavigator 现在可在 navigation steps / traceFrames 上输出 `variablesSnapshot`，P0 supported action 仍只写 placeholder 变量，不做真实执行。
- MockRuntimeRunner 改用 VariableStore 生成 TestRun trace 的变量快照，保留既有 RunSession/Trace/Log DTO 与持久化结构。
- DebugPanel 仅小修变量行类型与 tag 展示；不改 trace/log/error tab 架构。
- 自动化验证入口：`scripts/verify-microflow-variable-store.ts`；`.http` 已补 Round 50 navigate 示例。
- 本轮仍不实现 ExpressionEvaluator、真实 DB CRUD、真实 REST、事务、EntityAccess 或 CallMicroflow 执行。

第 39 轮已补充 Metadata API：

- `GET /api/microflow-metadata` 返回完整 `MicroflowMetadataCatalog`，支持 `workspaceId`、`moduleId`、`includeSystem`、`includeArchived`。
- `GET /entities/{qualifiedName}`、`GET /enumerations/{qualifiedName}`、`GET /microflows`、`GET /pages`、`GET /workflows`、`GET /health` 已接入 `IMicroflowMetadataService`。
- MetadataCatalog 优先读取 `MicroflowMetadataCache`；无 cache 时使用后端 seed catalog，Development 默认可写入 `seed-v1` cache。
- `catalog.microflows` 由 `MicroflowResource` 表动态生成，并读取当前 AuthoringSchema 中的 `parameters` / `returnType`；不解析 FlowGram。
- Page / Workflow 第一版可为空数组，完整领域模型/页面/工作流元数据服务后续接入。

第 40 轮已补充 Validation API：

- `POST /api/microflows/{id}/validate` 已由 `MicroflowValidationService` 实现，不再返回空 skeleton。
- 支持 `edit/save/publish/testRun` mode、inline schema 校验与读取当前保存 schema。
- 后端轻量 `MicroflowSchemaReader` 从 AuthoringSchema 提取 root、parameters、objects、flows、loop collection、action 与 caseValues。
- P0 规则覆盖 root、objectCollection、flows、events、decisions、loop、P0 actions、metadata references、基础 variables、基础 expressions、error handling、reachability。
- Validation 使用后端 MetadataService，不依赖前端 mock metadata；表达式执行器和完整变量作用域图仍留后续 Runtime 前深化。

第 41 轮已补充 References / Impact Analysis 深化：

- 新增 `IMicroflowReferenceIndexer` / `MicroflowReferenceIndexer`，基于 `MicroflowAuthoringSchema` 扫描 `callMicroflow` action 与 `exposure.url`，不解析 FlowGram JSON。
- 新增 `IMicroflowReferenceService` / `MicroflowReferenceService`，暴露 `GET /api/microflows/{id}/references` 与 `POST /api/microflows/{id}/references/rebuild`，所有响应仍包 `MicroflowApiResponse<T>`。
- `IMicroflowReferenceRepository` 支持按 target、source、query 过滤、count、source upsert 与批量插入；`Active=false` 默认过滤。
- `PUT /schema` 成功后同步重建 outgoing references；回滚/复制历史版本后尝试重建新 current schema 的 references。
- `VersionDiffService` 扩展 breaking changes：参数删除/类型/required、returnType、URL path、microflow/workflow action exposure disabled、node/flow removed。
- `PublishImpactService` 结合 active references 与 schema diff 计算 impact；发布 high impact 且未确认仍返回 `MICROFLOW_PUBLISH_BLOCKED`。
- 删除/归档 target 微流前检查 active references，存在引用时返回 `MICROFLOW_REFERENCE_BLOCKED`；Page / Workflow / Schedule 引用作为模型扩展入口，真实资源系统后续接入。

第 42 轮已补充 TestRun Mock API + Trace 存储：

- `POST /api/microflows/{id}/test-run`、`POST /api/microflows/runs/{runId}/cancel`、`GET /api/microflows/runs/{runId}`、`GET /api/microflows/runs/{runId}/trace` 已接入真实后端服务。
- `MicroflowTestRunService` 负责读取资源与草稿/已保存 schema、调用 Validation `mode=testRun`、执行 MockRunner、保存 RunSession/TraceFrame/RunLog，并更新 `LastRunStatus/LastRunAt`。
- `MicroflowMockRuntimeRunner` 基于 `MicroflowSchemaReader` 的 AuthoringSchema 模型执行契约级 mock，不依赖 FlowGram JSON，不访问业务数据库，不调用外部 REST，不实现完整表达式引擎。
- RunSession、TraceFrame、RunLog 通过既有 SqlSugar Repository 持久化；failed run 也保存，validation failed 不生成成功 run。
- 支持 `simulateRestError`、decision/object type case、loop iterations、max steps、LogMessage、unsupported action、RestCall error handler 的基础 trace。

并发与删除策略：

- `baseVersion` 可匹配 `resource.version`、`resource.concurrencyStamp`、当前 snapshot id 或 snapshot schemaVersion；不匹配返回 `MICROFLOW_VERSION_CONFLICT`。
- 删除采用物理删除资源行、保留历史 SchemaSnapshot 的第一版策略；第 41 轮后被 active references 引用的资源会被阻止删除/归档，源资源删除时清理 outgoing references。
- 保存前做最小 AuthoringSchema 结构校验，拒绝根级 `nodes`、`edges`、`workflowJson`、`flowgram` 等 FlowGram-only JSON。

请求上下文支持 `X-Workspace-Id`、`X-Tenant-Id`、`X-User-Id`、`X-Locale`、`X-Trace-Id`，并会把 traceId 写入 Envelope 与 `X-Trace-Id` 响应头。`.http` 示例见 `src/backend/Atlas.AppHost/Bosch.http/MicroflowBackend.http`。

后续建议：

- 第 40 轮：Validation API。
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

## 第 46～47 轮后端联调补充

- Publish / Version / References / Impact 已通过综合脚本验证标准 envelope、版本冲突、high impact 阻断、rollback、duplicate version、references rebuild 与 delete referenced blocked。
- TestRun / Debug 已通过综合脚本验证 Validation `mode=testRun`、RunSession / TraceFrame / RunLog 持久化、get run、get trace、cancel 与错误 envelope。
- 仍不实现真实 Runtime、真实 DB CRUD action、真实外部 REST、完整表达式执行器与完整事务引擎。

## 第 51 轮后端 Runtime ExpressionEvaluator P0

- 新增后端受控解释器 `IMicroflowExpressionEvaluator`，不依赖 FlowGram JSON，不修改 AuthoringSchema，不访问业务数据库，不调用外部 REST，不使用动态编译。
- 新增 AST / tokenizer / parser / type inference / evaluator / diagnostics / evaluation result，结果可序列化并提供安全 `valuePreview`。
- VariableStore 作为唯一变量读取来源，MetadataCatalog 仅用于 entity attribute / association / enumeration 的辅助推断。
- MockRuntimeRunner 已接入 CreateVariable、ChangeVariable、EndEvent、LogMessage、RestCall request preview；FlowNavigator 已接入 Boolean Decision 表达式且保留 options override。
- 自动化验证入口：`scripts/verify-microflow-expression-evaluator.ts` 与 `MicroflowExpressionEvaluatorTests`。

## 第 52 轮后端 Runtime MetadataResolver + EntityAccess Stub

- 新增 `Runtime/Metadata`：`IMicroflowMetadataResolver`、`MicroflowMetadataResolver`、`MicroflowMetadataResolutionContext`、resolved models、structured diagnostics 与 `MicroflowMetadataResolutionReport`。
- 新增 `Runtime/Security`：`MicroflowRuntimeSecurityContext`、`IMicroflowEntityAccessService`、`MicroflowEntityAccessService`、`MicroflowEntityAccessDecision` 与可配置 stub policy。
- `Microflow:Runtime:EntityAccessMode` 支持 `AllowAll`、`RoleBasedStub`、`DenyUnknownEntity`；Development 未显式配置时使用 AllowAll，Production 默认 DenyUnknownEntity。
- 新增 `Runtime/Objects` metadata-only operation plan，为第 54 轮 Object CRUD Actions 复用 resolver + access decision，不执行业务数据库写入。
- 新增 `POST /api/microflows/runtime/metadata/resolve` 与 `scripts/verify-microflow-metadata-resolver-entity-access.ts`，覆盖 metadata resolution、member path、dataType、EntityAccess stub 和不泄漏 FlowGram JSON。

## 第 53 轮后端 Runtime TransactionManager / UnitOfWork

- 新增 `Runtime/Transactions`：事务常量、上下文、选项、changed/committed/rolledBack object、operation、log、diagnostic、savepoint、snapshot、summary、exception。
- 新增 `IMicroflowTransactionManager` / `MicroflowTransactionManager`，提供 begin/commit/rollback/savepoint/rollbackToSavepoint/trackCreate/trackUpdate/trackDelete/trackRollbackObject/trackCommitAction/snapshot 以及 ErrorHandling rollback 基础接口。
- 新增 `IMicroflowUnitOfWork` / `MicroflowUnitOfWork`，仅维护内存 staged changes 与 operations，不访问业务数据库。
- `RuntimeExecutionContext` 新增强类型 `Transaction`、`UnitOfWork`、`TransactionManager`、`TransactionOptions`、`CurrentTransactionId` 与 `TransactionDiagnostics`。
- MockRuntimeRunner 对 P0 object actions 写 transaction change set、trace preview 和 runtime logs；`CommitAction` 会形成 `operation=commit` 结构化变更，成功 run 自动 commit，失败 rollback，`customWithoutRollback` / `continue` 不 rollback。
- `Commit` 仅提交 staged/已提交的变更；`RollbackToSavepoint` 后标记为 rolledBack 的 staged changes 会保留诊断记录，但不会被最终提交。
- `MicroflowRunSessionDto` 新增 `transactionSummary`；持久化 extra 同步保存 summary，成功输出可见 `output.transactionSummary`。
- `.http` 已补 Round 53 TestRun 示例；自动化验证入口：`scripts/verify-microflow-transaction-manager.ts`。
- 本轮不做真实业务表 CRUD、不调用 ORM SaveChanges、不执行 object events、refresh client、完整 ErrorHandling 或 EntityAccess enforcement。
- 第 54 轮 Object CRUD Actions 应复用本轮 TransactionManager 作为唯一运行时对象变更日志入口。

## 第 54 阶段加强版 Runtime ActionExecutor

- 新增 `Runtime/Actions` 核心模型与 `MicroflowActionExecutorRegistry`，覆盖前端 51 个 actionKind，并保留后端 legacy aliases。
- `MicroflowActionSupportMatrix` 改为读取 Registry descriptor，Validation / ExecutionPlan / Runtime 行为不再各自维护 P0 列表。
- MockRuntimeRunner 接入 Registry：ServerExecutable 写变量/事务/日志/trace，RuntimeCommand 输出 `runtimeCommands`，ConnectorBacked 缺 capability 返回 `RUNTIME_CONNECTOR_REQUIRED`，ExplicitUnsupported 返回 `RUNTIME_UNSUPPORTED_ACTION`。
- FlowNavigator 的 ActionActivity 输出包含 executorCategory/supportLevel/runtimeCommands/connectorRequests，不再只写模糊 placeholder。
- 新增 `scripts/verify-microflow-action-executors-full-coverage.ts` 与 `MicroflowActionExecutorRegistryTests` 覆盖全量矩阵、RuntimeCommand、connector required 和 fallback unsupported。

## 第 55 轮 Runtime Loop Backend

- 后端新增 `Runtime/Loops` 执行器族，并在 DI 中注册 `IMicroflowLoopExecutor`。
- `MicroflowFlowNavigator` 的 LoopedActivity 不再固定模拟次数，改为委托 `MicroflowLoopExecutor` 读取 VariableStore list 或执行 while expression。
- `MicroflowMockRuntimeRunner` 对 TestRun 同步实现 iterable / while / break / continue，DebugPanel 可通过 trace 看到 iteration、iterator、`$currentIndex` 与 control signal。
- `RuntimeErrorCode` 新增 loop source、condition、iterator、control out-of-scope、body missing、dead-end、maxIterations 等稳定错误码。
- 第 56 轮 CallMicroflow / CallStack 应复用当前 `RuntimeExecutionContext` 与 loop scope，不再新建第二套变量栈。

## 第 56 轮 Runtime CallMicroflow / CallStack Backend

- 新增 `MicroflowCallStackFrame`、`MicroflowCallMicroflowRequest/Result`、ParameterBinding、ReturnBinding、TraceLink、Diagnostic 与 `MicroflowChildExecutionContext` 等模型。
- 新增 `IMicroflowCallStackService` / `MicroflowCallStackService`；`RuntimeExecutionContext` 挂载 call stack frame、current frame、root/parent run id、callCorrelationId、maxCallDepth 与 metadata catalog。
- `CallMicroflowActionExecutor` 通过 `ActionExecutorRegistry` 执行，支持 target id / qualifiedName、current / latest published / targetVersion schema、参数表达式、子上下文、child runner、返回写回、错误传播与 recursion guard。
- `MicroflowExecutionPlanLoader` 补充 latest published / published version 入口，并修正 current schema 缺指针时回退 latest snapshot。
- `MicroflowTestRunService` 递归持久化 child RunSession / Trace / Log；trace frame extra JSON 保存 parentRunId/rootRunId/callFrameId/callDepth/caller。
- `MicroflowValidationService` 与 Runtime 对齐：target id/qualifiedName、required parameter mapping、未知参数、returnValue.storeResult/void、output variable 重复等进入校验。
- 当前仍不做异步微流队列、Workflow runtime、JavaAction host、真实 REST 深化或完整权限系统；第 57 轮 RestCall / LogMessage 可复用本轮 child trace 与 RuntimeLog 结构。

## 未知项

- 多区域复制与**最终一致**的 catalog 版本传播（可后续在 `metadata` 上扩展 ETag）。
