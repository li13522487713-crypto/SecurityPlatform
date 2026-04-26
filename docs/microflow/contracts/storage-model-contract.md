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
