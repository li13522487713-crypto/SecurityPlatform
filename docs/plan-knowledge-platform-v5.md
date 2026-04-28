# 知识库专题（v5 报告 §32–44）实施记录

> 本文档归档 `Coze 低代码平台补充研究与前端复刻方案总报告 v5` 第 32–44 章
> "资源中心 / 上传与解析 / 切片与索引 / 检索与注入 / 治理与开放接口"五个系统面
> 在 Atlas Security Platform 内的实际落地方案、里程碑、契约与验收命令。
>
> 编辑准则：当本文档与 [contracts.md](./contracts.md) 冲突时，
> 以 `contracts.md` 的"AI 资源库与知识库 API → v5 §32-44 知识库专题扩展接口"
> 为契约权威；本文档负责记录实施过程、命令清单与回滚指引。

## 1. 总体节奏与里程碑

```mermaid
flowchart LR
  M1[M1 前端契约+mock 骨架] --> M2[M2 资源中心 UI]
  M2 --> M3[M3 上传与解析 UI]
  M3 --> M4[M4 切片与索引 UI]
  M4 --> M5[M5 检索与注入 UI]
  M5 --> M6[M6 治理与开放接口 UI]
  M6 --> M7[M7 后端 Application 契约]
  M7 --> M8[M8 后端实体与仓储]
  M8 --> M9[M9 后端任务系统]
  M9 --> M10[M10 后端检索协议]
  M10 --> M11[M11 控制器与 .http]
  M11 --> M12[M12 DAG 节点扩展]
  M12 --> M13[M13 前端切真实 API]
  M13 --> M14[M14 文档同步收尾]
```

- 前端阶段（M1–M6）：所有页面通过 `LibraryKnowledgeApi` 调注入式 mock 适配器，
  状态机 / 异步任务用 timer 推进，可独立验收，跑前端 lint/单测/i18n/build 即可。
- 后端阶段（M7–M12）：按前端冻结的契约补 SqlSugar 实体 / Hangfire-style 任务 /
  控制器 / `.http` / DAG 节点参数；跑 `dotnet build`（0 警告 0 错误）+ `.http` 烟测。
- 切换阶段（M13–M14）：把 `VITE_LIBRARY_MOCK` 关掉即接入真实链路；文档定稿。

## 2. 五个系统面 × 实现位置一览

| v5 系统面 | 前端落点 | 后端落点 | 控制器 / API |
|---|---|---|---|
| 资源中心（§32-34） | `library-page.tsx`、`knowledge-detail-page.tsx`（Tabs 八宫格）、`KnowledgeBaseCreateWizard`、`KnowledgeResourcePicker` | `KnowledgeBase`、`KnowledgeBaseMetaEntity`、`KnowledgeBaseService` | `KnowledgeBasesController`（既有） |
| 上传与解析（§35） | `KnowledgeUploadPage`（4-step wizard）、`ParsingStrategyForm`、`ParsingStrategyComparePanel` | `KnowledgeJob`（type=parse/index）、`KnowledgeJobService.EnqueueParseAsync`、`DocumentService` 改造 | `POST /jobs/parse`、`POST /jobs/rebuild-index`、`POST /jobs/{id}:retry/cancel` |
| 切片与索引（§36-37） | `SlicesTab`（Text/Table/Image 分支）、`ChunkingProfileEditor`、`RetrievalProfileEditor`、`KnowledgeStateBadge` | 表格三表 `KnowledgeTable*Entity`、图片两表 `KnowledgeImage*Entity`、`KnowledgeBaseMetaEntity.ChunkingProfileJson/RetrievalProfileJson` | `GET /documents/{id}/table-columns/table-rows/image-items` |
| 检索与注入（§38） | `RetrievalTab`（透明度面板）、`RetrievalLogsPanel`、`WorkflowKnowledgeNodePanel`、`AgentKnowledgeBindingPanel` | `RagRetrievalService.SearchWithProfileAsync`、`KnowledgeRetrievalLogEntity`、`RetrievalLogService` | `POST /retrieval`、`GET /{id}/retrieval-logs`、`GET /retrieval-logs/{traceId}` |
| 治理与开放接口（§39-44） | `KnowledgePermissionsTab`、`KnowledgeVersionsTab`、`KnowledgeBindingsTab`、`KnowledgeJobsCenterPage`、`KnowledgeProviderConfigPage` | `KnowledgeBindingEntity`、`KnowledgePermissionEntity`、`KnowledgeVersionEntity`、`KnowledgeProviderConfigEntity`、对应 `*Service` | `bindings/permissions/versions/provider-configs/jobs` 完整 CRUD |

## 3. 契约要点（与 contracts.md 同型）

详见 [contracts.md](./contracts.md) "AI 资源库与知识库 API → v5 §32-44 知识库专题扩展接口"。
本节仅列前端组件与后端 DTO 的对应表，便于排查类型漂移：

| 前端类型（`@atlas/library-module-react`） | 后端类型（`Atlas.Application.AiPlatform.Models`） | 实体 |
|---|---|---|
| `KnowledgeBaseDto.kind` (`text/table/image`) | `KnowledgeBaseDto.Kind` (`KnowledgeBaseKind`) | `KnowledgeBaseMetaEntity.Kind` |
| `ParsingStrategy` | `ParsingStrategy` (`Atlas.Application.AiPlatform.Models`) | `KnowledgeDocumentMetaEntity.ParsingStrategyJson` |
| `ChunkingProfile` | `ChunkingProfile` | `KnowledgeBaseMetaEntity.ChunkingProfileJson` |
| `RetrievalProfile` | `RetrievalProfile` | `KnowledgeBaseMetaEntity.RetrievalProfileJson` |
| `KnowledgeJob` | `KnowledgeJobDto` | `KnowledgeJob` 实体（int Type/Status） |
| `KnowledgeBinding` | `KnowledgeBindingDto` | `KnowledgeBindingEntity`（string CallerType） |
| `KnowledgePermission` | `KnowledgePermissionDto` | `KnowledgePermissionEntity`（string Scope/SubjectType） |
| `KnowledgeVersion` | `KnowledgeVersionDto` | `KnowledgeVersionEntity` |
| `RetrievalLog` | `RetrievalLogDto` | `KnowledgeRetrievalLogEntity` |
| `KnowledgeProviderConfig` | `KnowledgeProviderConfigDto` | `KnowledgeProviderConfigEntity` |
| `KnowledgeTableColumn` / `KnowledgeTableRow` | `KnowledgeTableColumnDto` / `KnowledgeTableRowDto` | `KnowledgeTableColumnEntity` / `KnowledgeTableRowEntity` |
| `KnowledgeImageItem` / `KnowledgeImageAnnotation` | `KnowledgeImageItemDto` / `KnowledgeImageAnnotationDto` | `KnowledgeImageItemEntity` / `KnowledgeImageAnnotationEntity` |

## 4. 验收命令清单

每个里程碑落地后必须执行的最小验证集（与 `AGENTS.md` 长任务规则一致）：

```bash
# 后端
dotnet build                                    # 必须 0 错误 0 警告
dotnet test tests/Atlas.SecurityPlatform.Tests --filter "FullyQualifiedName!~Integration"

# 前端
cd src/frontend
pnpm --filter @atlas/library-module-react run test     # mock 适配器单测，11/11 必须通过
pnpm run lint
pnpm run i18n:check
pnpm run build
```

后端 `.http` 烟测：
- `src/backend/Atlas.AppHost/Bosch.http/KnowledgeBases.http`（基础 CRUD）
- `src/backend/Atlas.AppHost/Bosch.http/KnowledgeBasesV5.http`（v5 §32-44 全量）
- `src/backend/Atlas.AppHost/Bosch.http/KnowledgeBases.http`
- `src/backend/Atlas.AppHost/Bosch.http/KnowledgeBasesV5.http`

## 5. 切换开关

```bash
# 前端走 mock 适配器（不依赖后端，便于演示与回归）
VITE_LIBRARY_MOCK=true pnpm run dev:app-web

# 默认走真实 API（已对齐 v5 §32-44 全量）
pnpm run dev:app-web
```

实现见 [src/frontend/apps/app-web/src/app/app.tsx](../src/frontend/apps/app-web/src/app/app.tsx) `readLibraryMockFlag()`。

## 6. 关键风险与回滚

- **Provider 配置切换**：upload / storage / vector / embedding / generation 必须走
  Adapter 注册（见 `Atlas.Infrastructure/DependencyInjection/AiCoreServiceRegistration.cs`），
  不得在业务代码硬编码具体 Provider；切换时务必同步更新 `appsettings.*.json`。
- **任务系统兼容性**：当前 `KnowledgeJobService` 复用 `IBackgroundWorkQueue`；
  未来切到纯 Hangfire 时，任务持久化（`KnowledgeJob` 表）已存在，无需迁移。
- **DAG 节点字段扩展**：`KnowledgeRetriever` 新增 `retrievalProfile` / `debug`、
  `KnowledgeIndexer` 新增 `parsingStrategy` 时已保持向后兼容（只在配置存在时才走新协议）。
- **等保 2.0**：调试日志默认 `maskSensitive=true`；`debug=false` 时检索 candidates 仅返回
  分数与 id，不返回原文，避免低权限调用方拿到敏感原文。
- **回滚策略**：所有新增表均为 sidecar / 独立表（不修改既有 `KnowledgeBase` /
  `KnowledgeDocument` / `DocumentChunk` 字段），删除新表即可回到 v5 之前形态。

## 7. 里程碑完成情况

| 里程碑 | 状态 | 关键交付物 |
|---|---|---|
| M1 共享契约 + mock 适配器 | 完成 | `library-module-react/src/types.ts` + `src/mock/` + 11 个单测 |
| M2 资源中心 UI | 完成 | 类型化创建向导 + 八宫 Tab 详情页 + `KnowledgeResourcePicker` |
| M3 上传与解析 UI | 完成 | 4-step 上传向导 + 三类 ParsingStrategy 表单 + 策略对比 |
| M4 切片与索引 UI | 完成 | TextSlicesView / TableRowsView / ImageItemsView + Profile 编辑器 |
| M5 检索与注入 UI | 完成 | 检索透明度面板 + RetrievalLogsPanel + Workflow/Agent Binding Panel |
| M6 治理与开放接口 UI | 完成 | 四层权限 / 版本 / 绑定 / 任务中心 / Provider 中心（只读） |
| M7 后端 Application 契约 | 完成 | KnowledgeStrategyModels / Job / Binding / Permission / Version / RetrievalLog / Provider DTO + Validators |
| M8 后端实体与仓储 | 完成 | 12 个 v5 实体 + 对应 Repository + AtlasOrmSchemaCatalog 注册 |
| M9 后端任务系统 | 完成 | `KnowledgeJobService`（队列状态机 + 重试 / 死信 / 取消 / rebuild） |
| M10 后端检索协议 | 完成 | `RagRetrievalService.SearchWithProfileAsync` + `RetrievalLogService` |
| M11 控制器与 .http | 完成 | 双宿主 `KnowledgeBasesV5Controller` + 双宿主 `KnowledgeBasesV5.http` + contracts.md 章节 |
| M12 DAG 节点扩展 | 完成 | `KnowledgeRetriever`/`KnowledgeIndexer` Executor + 节点元数据 + 验证矩阵 |
| M13 前端切真实 API | 完成 | `realLibraryApi` 装配 v5 全量；前端 lint / 单测 / i18n / build / 后端 dotnet build 全绿 |
| M14 文档同步收尾 | 完成 | 本文档 + contracts.md v5 章节定稿 |

## 8. 100% 深度补齐执行清单（G1–G10）

> 二次审查后按 [knowledge-v5_100%_remediation_0975746e.plan.md](../.cursor/plans/knowledge-v5_100%25_remediation_0975746e.plan.md) 完成的补齐工作。每个 phase 列出关键变更与对应文件 / commit 占位。

| Phase | 内容 | 关键文件 | Commit 占位 |
|---|---|---|---|
| G1 Application 重命名与契约补齐 | KnowledgeBaseProvider 重命名（旧名 [Obsolete] 别名）；KnowledgeDocumentLifecycleStatus 移到 DocumentModels；ParseJobDto/IndexJobDto/RebuildJobDto/GcJobDto；ParseJobReplayRequest/IndexJobRebuildRequest/DeadLetterRetryRequest；KnowledgePermissionUpdateRequest/KnowledgeProviderConfigUpsertRequest；RetrievalCallerContext.Preset；RetrievalRequest.Rerank 顶层；扁平化 RetrievalResponseDto；IKnowledgeParseJobService/IKnowledgeIndexJobService 拆分（保留 IKnowledgeJobService facade + RetryDeadLetterBatchAsync）；IKnowledgeBindingService.GetByIdAsync；IKnowledgePermissionService.UpdateAsync；IKnowledgeProviderConfigService.UpsertAsync；FluentValidation 全字段补齐 | `KnowledgeStrategyModels.cs`、`DocumentModels.cs`、`KnowledgeJobModels.cs`、`KnowledgePermissionModels.cs`、`KnowledgeProviderConfigModels.cs`、`RetrievalLogModels.cs`、`KnowledgeValidators.cs`、`Abstractions/Knowledge/*.cs`、`KnowledgePlatformV5Services.cs` | _PR pending_ |
| G2 Domain 实体拆分与 SqlSugar 属性 | KnowledgeJob 拆为 KnowledgeParseJob+KnowledgeIndexJob（KnowledgeJob 保留为兼容查询视图）；KnowledgeVersionEntity 重命名 KnowledgeDocumentVersion（旧名 [Obsolete] 别名同表）；新增 KnowledgeTable 父表（表格 KB 三表完整）；全部 v5 实体补齐 [SugarTable]+[SugarColumn]；AtlasOrmSchemaCatalog 去重；KnowledgeParseJobRepository/KnowledgeIndexJobRepository/KnowledgeTableRepository 仓储 | `KnowledgePlatformV5Entities.cs`、`KnowledgePlatformV5Repositories.cs`、`AtlasOrmSchemaCatalog.cs`、`AiCoreServiceRegistration.cs` | _PR pending_ |
| G3 Hangfire 真迁移 | 新建 KnowledgeParseJobService/KnowledgeIndexJobService（IBackgroundJobClient.Enqueue<TRunner>）；新建 KnowledgeParseJobRunner/KnowledgeIndexJobRunner（[AutomaticRetry(Attempts=3)] + [DisableConcurrentExecution]，失败时 IncrementAttempts → DeadLetter）；DocumentService.Create/Resegment 切到 IKnowledgeParseJobService；KnowledgeJobService.RerunParseAsync/RebuildIndexAsync/RetryDeadLetterAsync 委托给 Hangfire 链；KnowledgeVersionService.RollbackAsync 实现真回滚（写新版本+恢复 KB 计数）；DocumentProcessingService 内分阶段写 lifecycle Parsing→Chunking→Indexing→Ready | `KnowledgeParseJobService.cs`、`KnowledgeIndexJobService.cs`、`KnowledgeJobService.cs`、`DocumentService.cs`、`KnowledgePlatformV5Services.cs` | _PR pending_ |
| G4 检索协议真深度 | RagRetrievalService 注入 IQueryRewriter + IReranker + DocumentChunkRepository；SearchWithProfileAsync 真改写（无 provider 降级）+ 真重排 + MetadataFilter 严格执行 + debug 模式填充候选 metadata；HybridRagRetrieverService.RetrieveWithProfileAsync 透传 weights/EnableHybrid；HybridRetrievalService.MergeAndRerankWithWeights 加权 RRF；BM25/Vector retriever profile-aware overload；RetrievalRequest.Rerank 顶层覆盖 profile.EnableRerank；RetrievalResponseDto.FromLog 扁平化 | `RagRetrievalService.cs`、`HybridRagRetrieverService.cs`、`HybridRetrievalService.cs`、`BM25RetrievalService.cs`、`VectorRetrieverService.cs` | _PR pending_ |
| G5 REST 路径重命名与 .http 镜像 | AppHost 上 `KnowledgeBasesV5Controller` 新增 documents/{id}/parse-jobs (GET/POST)、documents/{id}/index-jobs/rebuild、jobs/dead-letter:retry、bindings/{id} GET、permissions/{id} PUT、provider-configs/{role} PUT；旧路径双轨保留；`Bosch.http` 完整镜像；docs/contracts.md 追加新路径 + deprecation 表格 | `KnowledgeBasesV5Controller.cs`（AppHost）、`KnowledgeBasesV5.http`（AppHost）、`docs/contracts.md` | _PR pending_ |
| G6 DAG NodeExecutor 全参数 | KnowledgeRetrieverNodeExecutor: TryParseFilters + TryParseCallerContextOverride + MergeCallerContext，输出 traceId/finalContext/candidates/rewrittenQuery/latencyMs camelCase（保留 snake_case 别名）；合并 legacy/v5 两路；KnowledgeIndexerNodeExecutor: TryParseChunkingProfile + ResolveMode (append/overwrite)，改用 IKnowledgeIndexJobService → Hangfire；BuiltInWorkflowNodeDeclarations 默认/form-meta/JSON schema 全部加 filters/callerContextOverride/chunkingProfile/mode；docs/workflow-editor-validation-matrix.md 第 52-53 行重写 | `KnowledgeRetrieverNodeExecutor.cs`、`KnowledgeIndexerNodeExecutor.cs`、`BuiltInWorkflowNodeDeclarations.cs`、`docs/workflow-editor-validation-matrix.md` | _PR pending_ |
| G7 Frontend Playground 全量改造 | dataset-search/form.tsx + 新增 atlas-v5-settings.tsx（retrievalProfile 全字段编辑器、filters key-value、callerContextOverride、debug switch）；data-transformer 双向映射 atlasV5；dataset-write-setting.tsx + 新增 atlas-v5-write-settings.tsx（完整 ParsingStrategy + ChunkingProfile mode 选择 + append/overwrite radio）；data-transformer.ts 序列化 atlasV5Write；library-module-react 提取 RetrievalProfileFields 共享组件；WorkflowKnowledgeNodePanel 嵌入 RetrievalProfileFields + filters + callerContextOverride 表单；AgentKnowledgeBindingPanel 嵌入 RetrievalProfileFields（modal）+ 新建绑定时透传 retrievalProfileOverride | `dataset-search/{form,data-transformer}.tsx`、`dataset-search/components/atlas-v5-settings.tsx`、`dataset-write/components/{dataset-write-setting,atlas-v5-write-settings}.tsx`、`dataset-write/data-transformer.ts`、`workflow-knowledge-node-panel.tsx`、`agent-knowledge-binding-panel.tsx`、`knowledge-detail/retrieval-profile-editor.tsx`、`types.ts` | _PR pending_ |
| G8 前端 UX 与 Mock 完整化 | knowledge-bases/new?kind 独立路由 + WorkspaceKnowledgeCreateRoute；messages.ts 镜像 8 个 detailTab key（zh/en）；upload page 接入 ParsingStrategyComparePanel；mock 三段任务链 parse→chunking→index + parse 完成时按 KB kind 生成 3-6 mock chunk；TableRowsView 列+关键词过滤；ImageItemsView 标注类型+关键词过滤；retrieval-tab 加 filters key-value/preset Select/finalContext Collapse/metadata expand；抽出 RetrievalLogsPanel；permissions Modal→SideSheet + scope=document 时 documentId 选择器；knowledge-jobs-center-page 用 spaceId 过滤；diffVersions mock 真 deepDiff 字段对比；types.ts 加 RetrievalCallerPreset 与 KnowledgeJobsListRequest.spaceId 与 KnowledgeJobType="chunking" | `app.tsx`、`messages.ts`、`knowledge-upload-page.tsx`、`mock/{adapter,scheduler}.ts`、`knowledge-detail/{slices,permissions,retrieval}-tab.tsx`、`knowledge-detail/retrieval-logs-panel.tsx`、`knowledge-jobs-center-page.tsx`、`types.ts` | _PR pending_ |
| G9 单测与文档收尾 | tests/Atlas.SecurityPlatform.Tests/Services/AiPlatform/KnowledgeNodeExecutorTests.cs（3 用例：filters/callerContextOverride 透传、ChunkingProfile + overwrite、默认 append）；mock/__tests__/adapter.spec.ts 验证三段链 parse→chunking→index + 3-6 mock chunk；diffVersions deepDiff 字段断言；docs/contracts.md G5 端点 + deprecation 表格；docs/plan-knowledge-platform-v5.md 本节；AGENTS.md 知识库 Hangfire 强约束 | `tests/Atlas.SecurityPlatform.Tests/Services/AiPlatform/KnowledgeNodeExecutorTests.cs`、`adapter.spec.ts`、`docs/contracts.md`、`docs/plan-knowledge-platform-v5.md`、`AGENTS.md` | _PR pending_ |
| G10 端到端回归 | dotnet build 双宿主全绿；pnpm --filter @atlas/library-module-react test 11/11 + KnowledgeNodeExecutorTests 3/3；前端关闭 VITE_LIBRARY_MOCK 后详情页 8 tab + 创建向导路由 + 全局 jobs/providers center + playground 知识节点表单提交链路通；Bosch.http 全部 v5 stanza 烟测脚本就绪 | `bash` / `pwsh` 脚本 + 烟测报告 | _PR pending_ |

## 9. 验收命令清单（每 phase 完成后跑）

- 后端：`dotnet build src/backend/Atlas.AppHost`、`dotnet test "tests/Atlas.SecurityPlatform.Tests/Atlas.SecurityPlatform.Tests.csproj" --filter "FullyQualifiedName~KnowledgeNodeExecutorTests"`
- 前端：`pnpm --filter @atlas/library-module-react test`、`pnpm --filter @atlas/app-web build`、`pnpm run i18n:check`
- 烟测：`http -- src/backend/Atlas.AppHost/Bosch.http/KnowledgeBasesV5.http` 全部端点（含 G5 新 stanza：parse-jobs / index-jobs/rebuild / dead-letter:retry / permissions PUT / provider-configs PUT）
