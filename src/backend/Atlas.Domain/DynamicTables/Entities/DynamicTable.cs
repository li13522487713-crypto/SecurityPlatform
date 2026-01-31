using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Enums;

namespace Atlas.Domain.DynamicTables.Entities;

public sealed class DynamicTable : TenantEntity
{
    public DynamicTable()
        : base(TenantId.Empty)
    {
        TableKey = string.Empty;
        DisplayName = string.Empty;
        Description = null;
        DbType = DynamicDbType.Sqlite;
        Status = DynamicTableStatus.Draft;
        CreatedAt = DateTimeOffset.MinValue;
        UpdatedAt = DateTimeOffset.MinValue;
        CreatedBy = 0;
        UpdatedBy = 0;
    }

    public DynamicTable(
        TenantId tenantId,
        string tableKey,
        string displayName,
        string? description,
        DynamicDbType dbType,
        long createdBy,
        long id,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        TableKey = tableKey;
        DisplayName = displayName;
        Description = description;
        DbType = dbType;
        Status = DynamicTableStatus.Active;
        CreatedAt = now;
        UpdatedAt = now;
        CreatedBy = createdBy;
        UpdatedBy = createdBy;
    }

    public string TableKey { get; private set; }
    public string DisplayName { get; private set; }
    public string? Description { get; private set; }
    public DynamicDbType DbType { get; private set; }
    public DynamicTableStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long CreatedBy { get; private set; }
    public long UpdatedBy { get; private set; }

    public void UpdateMeta(
        string displayName,
        string? description,
        DynamicTableStatus status,
        long updatedBy,
        DateTimeOffset now)
    {
        DisplayName = displayName;
        Description = description;
        Status = status;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }
}
