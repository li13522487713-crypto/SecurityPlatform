using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.DynamicTables.Entities;

/// <summary>
/// 动态表结构迁移记录。
/// </summary>
public sealed class DynamicSchemaMigration : TenantEntity
{
    public DynamicSchemaMigration()
        : base(TenantId.Empty)
    {
        TableKey = string.Empty;
        OperationType = string.Empty;
        AppliedSql = string.Empty;
        Status = string.Empty;
    }

    public DynamicSchemaMigration(
        TenantId tenantId,
        long tableId,
        string tableKey,
        string operationType,
        string appliedSql,
        string? rollbackSql,
        string status,
        long createdBy,
        long id,
        DateTimeOffset createdAt)
        : base(tenantId)
    {
        Id = id;
        TableId = tableId;
        TableKey = tableKey;
        OperationType = operationType;
        AppliedSql = appliedSql;
        RollbackSql = rollbackSql;
        Status = status;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public long TableId { get; private set; }
    public string TableKey { get; private set; }
    public string OperationType { get; private set; }
    public string AppliedSql { get; private set; }
    public string? RollbackSql { get; private set; }
    public string Status { get; private set; }
    public long CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
