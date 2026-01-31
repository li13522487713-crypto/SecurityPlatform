using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Enums;

namespace Atlas.Domain.DynamicTables.Entities;

public sealed class DynamicField : TenantEntity
{
    public DynamicField()
        : base(TenantId.Empty)
    {
        TableId = 0;
        Name = string.Empty;
        DisplayName = string.Empty;
        FieldType = DynamicFieldType.String;
        Length = null;
        Precision = null;
        Scale = null;
        AllowNull = true;
        IsPrimaryKey = false;
        IsAutoIncrement = false;
        IsUnique = false;
        DefaultValue = null;
        SortOrder = 0;
        CreatedAt = DateTimeOffset.MinValue;
        UpdatedAt = DateTimeOffset.MinValue;
    }

    public DynamicField(
        TenantId tenantId,
        long tableId,
        string name,
        string displayName,
        DynamicFieldType fieldType,
        int? length,
        int? precision,
        int? scale,
        bool allowNull,
        bool isPrimaryKey,
        bool isAutoIncrement,
        bool isUnique,
        string? defaultValue,
        int sortOrder,
        long id,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        TableId = tableId;
        Name = name;
        DisplayName = displayName;
        FieldType = fieldType;
        Length = length;
        Precision = precision;
        Scale = scale;
        AllowNull = allowNull;
        IsPrimaryKey = isPrimaryKey;
        IsAutoIncrement = isAutoIncrement;
        IsUnique = isUnique;
        DefaultValue = defaultValue;
        SortOrder = sortOrder;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public long TableId { get; private set; }
    public string Name { get; private set; }
    public string DisplayName { get; private set; }
    public DynamicFieldType FieldType { get; private set; }
    public int? Length { get; private set; }
    public int? Precision { get; private set; }
    public int? Scale { get; private set; }
    public bool AllowNull { get; private set; }
    public bool IsPrimaryKey { get; private set; }
    public bool IsAutoIncrement { get; private set; }
    public bool IsUnique { get; private set; }
    public string? DefaultValue { get; private set; }
    public int SortOrder { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(
        string displayName,
        int? length,
        int? precision,
        int? scale,
        bool allowNull,
        bool isUnique,
        string? defaultValue,
        int sortOrder,
        DateTimeOffset now)
    {
        DisplayName = displayName;
        Length = length;
        Precision = precision;
        Scale = scale;
        AllowNull = allowNull;
        IsUnique = isUnique;
        DefaultValue = defaultValue;
        SortOrder = sortOrder;
        UpdatedAt = now;
    }
}
