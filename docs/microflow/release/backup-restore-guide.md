# Backup / Restore Guide

## Scope

必须备份：

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

## SQLite Backup

1. 停止 AppHost 或进入只读维护窗口。
2. 复制 `Database:ConnectionString` 指向的 SQLite 文件。
3. 同步复制 wal/shm 文件（如存在）。
4. 记录 AppHost version、git commit、备份时间、数据库大小。

## MySQL / PostgreSQL

当前默认是 SQLite；若生产切到 MySQL/PostgreSQL，使用平台 DBA 工具执行一致性快照，并确保上述表在同一个事务快照中导出。

## Restore Order

1. 停止 AppHost。
2. 恢复数据库备份。
3. 启动 AppHost。
4. 检查 `MicroflowSchemaMigration`。
5. 调用 storage health。
6. 对关键资源执行 schema read、validation、publish snapshot read。
7. `MicroflowMetadataCache` 可删除后重建，但恢复后必须重新检查 metadata health。

## Consistency Checks

- 每个 `MicroflowResource.CurrentSchemaSnapshotId` 能找到对应 `MicroflowSchemaSnapshot`。
- 每个 `MicroflowVersion.SchemaSnapshotId` 能找到对应 snapshot。
- 每个 `MicroflowPublishSnapshot.ResourceId` 能找到 resource。
- Trace/log 缺失不影响资源恢复，但会影响故障审计。

## Retention Notes

- 不自动删除 `MicroflowSchemaSnapshot`、`MicroflowVersion`、`MicroflowPublishSnapshot`。
- `MicroflowRunTraceFrame` 和 `MicroflowRunLog` 可按 retention 归档或选择性恢复。
- 测试数据清理必须按前缀，不允许模糊删除真实资源。
