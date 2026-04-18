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
- `src/backend/Atlas.PlatformHost/Bosch.http/KnowledgeBases.http`（基础 CRUD）
- `src/backend/Atlas.PlatformHost/Bosch.http/KnowledgeBasesV5.http`（v5 §32-44 全量）
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
