# 微流 DB / 存储模型草案（冻结）

与 `mendix-studio-core` 中 `microflow/contracts/storage/storage-types.ts` 对齐。以下为逻辑表，物理库可为 PostgreSQL / SQL Server 等。

## 核心原则

1. **只存** `MicroflowAuthoringSchema` 的 JSON（`schemaJson`），**不存** FlowGram / WorkflowJSON。
2. **发布快照**行一旦写入即**只读**（不可变策略）；新版本新增新行。
3. `schemaVersion`：Authoring 结构版本；`migrationVersion`：后端侧迁移/脚本版本，可与 Authoring 轴独立演进。
4. 审计/租户：资源与运行、发布均建议带 `CreatedBy/UpdatedBy/tenantId/workspaceId`（与 `MicroflowApiRequestContext` 配合）。

## 表清单

| 表 | 说明 |
|----|------|
| `MicroflowResource` | 主资源，列表筛选项与 `MicroflowResource` DTO 对齐。 |
| `MicroflowSchemaSnapshot` | 每次保存/草稿可还原点；`SchemaJson` 为 Authoring。 |
| `MicroflowVersion` | 版本行；关联 `SchemaSnapshotId`。 |
| `MicroflowPublishSnapshot` | 发布不可变镜像；`SchemaJson` 为 Authoring。 |
| `MicroflowReference` | 引用与影响；`SourceType/ImpactLevel` 与 DTO 枚举一致；`Active` 支持 `includeInactive` 查询。 |
| `MicroflowRunSession` | 运行主记录；`InputJson/OutputJson/ErrorJson`。 |
| `MicroflowRunTraceFrame` | Trace 行表；`ObjectId/ActionId/...` 与 `MicroflowTraceFrame` 一致。 |
| `MicroflowRunLog` | 细粒度运行日志。 |
| `MicroflowMetadataCache` | 全量/按工作区元数据 JSON 缓存。 |
| `MicroflowSchemaMigration` | 已应用迁移记录（`FromVersion/ToVersion/AppliedAt`）。 |

## 第 36 轮实现状态

当前仓库按现有 .NET 分层落地：

- `Atlas.Domain.Microflows`：SqlSugar 存储实体。
- `Atlas.Application.Microflows`：Repository 接口、DTO mapper、storage diagnostics 抽象。
- `Atlas.Infrastructure`：SqlSugar Repository 实现、DB-backed resource/metadata 查询、storage health、开发 seed。
- `Atlas.AppHost`：仅暴露 API，不直接操作 ORM。

数据库技术栈沿用现有 `SqlSugar` + `CodeFirst.InitTables`。微流实体已加入 `AtlasOrmSchemaCatalog.EnsureRuntimeSchema(db)`，启动初始化会重复安全地确保表存在，不引入 EF Migration 或第二套 ORM。

已创建的物理表：

- `MicroflowResource`
- `MicroflowSchemaSnapshot`
- `MicroflowVersion`
- `MicroflowPublishSnapshot`
- `MicroflowReference`
- `MicroflowRunSession`
- `MicroflowRunTraceFrame`
- `MicroflowRunLog`
- `MicroflowMetadataCache`
- `MicroflowSchemaMigration`

`SchemaJson` 与发布快照中的 `SchemaJson` 只保存 `MicroflowAuthoringSchema` JSON；当前不会保存 FlowGram JSON。发布快照表按不可变写入模型设计，后续发布服务只能新增行，不应覆盖已有快照。Trace 按 `MicroflowRunSession` 主表、`MicroflowRunTraceFrame` 与 `MicroflowRunLog` 从表保存，`RunId` 已建查询索引。

第 41 轮 References / Impact 存储策略：

- `MicroflowReference` 作为引用索引表，核心字段为 `TargetMicroflowId`、`SourceType`、`SourceId`、`SourcePath`、`ReferenceKind`、`ImpactLevel`、`Active`，并预留 `WorkspaceId` / `TenantId` / `ExtraJson`。
- `IMicroflowReferenceRepository.UpsertReferencesForSourceAsync` 语义为先删除 source 旧引用再插入新引用；当前 `MicroflowReferenceIndexer` 对 microflow source 会同步清理 `sourceType=microflow` 与 `sourceType=api` 的旧索引。
- active references 默认参与查询和删除/归档阻断；`includeInactive=true` 仅用于诊断或兼容历史引用。
- Page / Workflow / Schedule / API 真实资源系统暂未落地，DTO 与 Repository 字段已经预留，后续对应服务接入后可写入同一索引表。

第 38 轮发布/版本落地策略：

- 发布时新增 `MicroflowSchemaSnapshot` 作为发布时刻的 schema 固化点，`MicroflowVersion.SchemaSnapshotId` 与 `MicroflowPublishSnapshot.SchemaSnapshotId` 均指向该快照。
- `MicroflowPublishSnapshot.SchemaJson` 保存同一份 AuthoringSchema JSON 与 `SchemaHash`，作为只读发布镜像；后续发布同版本会被唯一版本校验阻止，不覆盖旧行。
- 发布、回滚、复制历史版本使用 `IMicroflowStorageTransaction` 包装 SqlSugar 事务，避免 Version / PublishSnapshot / Resource 更新半成功后返回 success。
- 回滚只新增新的 current `MicroflowSchemaSnapshot` 并更新 `MicroflowResource.CurrentSchemaSnapshotId`，不修改旧 version、旧 schema snapshot 或 publish snapshot。
- 复制历史版本创建新的 `MicroflowResource` 与新的 schema snapshot，默认 `status=draft`、`publishStatus=neverPublished`、`referenceCount=0`。

开发 seed：

- 配置键：`Microflows:SeedData:Enabled=true`
- 仅 Development 环境运行。
- 默认 seed id：`mf-seed-blank`
- seed schema 是最小 `MicroflowAuthoringSchema` 风格 JSON，不包含 FlowGram 字段。

诊断接口：

- `GET /api/microflows/storage/health`
- 返回 provider 与上述表存在性，不泄露 connection string。

## 索引建议

- `WorkspaceId` + `UpdatedAt`（列表）。
- `ResourceId`（子资源与联表）。
- `Status`、`PublishStatus`（筛选）。
- `Version` / `IsLatestPublished`（版本与当前发布查询）。
- `RunId`（trace 与 log）。

## Trace 保留策略

可配置按时间/条数/租户清理 `MicroflowRunSession` 与从表；与 API `GET .../trace` 一致，P0 可为简单 TTL。

## JSON 列

- `SchemaJson` / `CatalogJson` / `ValidationSummaryJson` / `ImpactAnalysisJson` / 变量与 trace 的 JSON 等，**形状**以 TypeScript 契约为准，不嵌入 FlowGram。
