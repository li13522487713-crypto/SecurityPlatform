using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.DynamicTables.Entities;

/// <summary>
/// 不可变的 Schema 发布快照，记录某次发布时的完整表结构定义。
/// 用于版本比较、回滚参考和审计留痕。
/// </summary>
public sealed class SchemaPublishSnapshot : TenantEntity
{
    public SchemaPublishSnapshot()
        : base(TenantId.Empty)
    {
        TableKey = string.Empty;
        SnapshotJson = string.Empty;
        PublishNote = null;
    }

    public SchemaPublishSnapshot(
        TenantId tenantId,
        long tableId,
        string tableKey,
        int version,
        string snapshotJson,
        string? publishNote,
        long publishedBy,
        long id,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        TableId = tableId;
        TableKey = tableKey;
        Version = version;
        SnapshotJson = snapshotJson;
        PublishNote = publishNote;
        PublishedBy = publishedBy;
        PublishedAt = now;
        MigrationTaskId = null;
    }

    public long TableId { get; private set; }
    public string TableKey { get; private set; }

    /// <summary>发布时对应的 Schema 版本号</summary>
    public int Version { get; private set; }

    /// <summary>表结构完整快照 JSON（字段、索引、关系等）</summary>
    [SugarColumn(ColumnDataType = "text")]
    public string SnapshotJson { get; private set; }

    /// <summary>发布备注</summary>
    [SugarColumn(IsNullable = true, Length = 500)]
    public string? PublishNote { get; private set; }

    public long PublishedBy { get; private set; }
    public DateTimeOffset PublishedAt { get; private set; }

    /// <summary>关联的迁移任务 ID（可追溯至 SchemaChangeTask）</summary>
    [SugarColumn(IsNullable = true)]
    public long? MigrationTaskId { get; private set; }

    public void BindMigrationTask(long migrationTaskId)
    {
        MigrationTaskId = migrationTaskId;
    }
}
