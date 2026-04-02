using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Enums;
using SqlSugar;

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
        IsComputed = false;
        ComputedExprId = null;
        IsStatusField = false;
        IsRowVersionField = false;
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
        IsComputed = false;
        ComputedExprId = null;
        IsStatusField = false;
        IsRowVersionField = false;
    }

    public long TableId { get; private set; }
    public string Name { get; private set; }
    public string DisplayName { get; private set; }
    public DynamicFieldType FieldType { get; private set; }

    /// <summary>仅 String 等类型有意义；Long/Decimal/Bool 等应为 null。</summary>
    [SugarColumn(IsNullable = true)]
    public int? Length { get; private set; }

    [SugarColumn(IsNullable = true)]
    public int? Precision { get; private set; }

    [SugarColumn(IsNullable = true)]
    public int? Scale { get; private set; }
    public bool AllowNull { get; private set; }
    public bool IsPrimaryKey { get; private set; }
    public bool IsAutoIncrement { get; private set; }
    public bool IsUnique { get; private set; }

    [SugarColumn(IsNullable = true)]
    public string? DefaultValue { get; private set; }
    public int SortOrder { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>是否为计算字段（由表达式引擎求值）</summary>
    public bool IsComputed { get; private set; }

    /// <summary>关联的计算表达式 ID（仅 IsComputed=true 时有效）</summary>
    [SugarColumn(IsNullable = true)]
    public long? ComputedExprId { get; private set; }

    /// <summary>是否为状态字段（用于审批流等业务状态追踪）</summary>
    public bool IsStatusField { get; private set; }

    /// <summary>是否为行版本字段（用于乐观并发控制）</summary>
    public bool IsRowVersionField { get; private set; }

    public void SetComputed(long? exprId, DateTimeOffset now)
    {
        IsComputed = exprId.HasValue;
        ComputedExprId = exprId;
        UpdatedAt = now;
    }

    public void SetStatusField(bool isStatus, DateTimeOffset now)
    {
        IsStatusField = isStatus;
        UpdatedAt = now;
    }

    public void SetRowVersionField(bool isRowVersion, DateTimeOffset now)
    {
        IsRowVersionField = isRowVersion;
        UpdatedAt = now;
    }

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
