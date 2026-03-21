using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.Metering;

/// <summary>资源使用量记录（计量域）</summary>
public sealed class UsageRecord : TenantEntity
{
    public UsageRecord()
        : base(TenantId.Empty)
    {
    }

    public UsageRecord(
        TenantId tenantId,
        long id,
        string resourceType,
        decimal quantity,
        string? unitLabel,
        DateTimeOffset recordedAt)
        : base(tenantId)
    {
        SetId(id);
        ResourceType = resourceType;
        Quantity = quantity;
        UnitLabel = unitLabel ?? string.Empty;
        RecordedAt = recordedAt;
    }

    /// <summary>资源类型（用户数、应用数、API调用数、存储量等）</summary>
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>本次计量量值</summary>
    [SugarColumn(ColumnDataType = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    /// <summary>单位标签（次/MB/个）</summary>
    public string UnitLabel { get; set; } = string.Empty;

    public DateTimeOffset RecordedAt { get; set; }
}

/// <summary>租户配额配置（计量域）</summary>
public sealed class TenantQuota : TenantEntity
{
    public TenantQuota()
        : base(TenantId.Empty)
    {
    }

    public TenantQuota(TenantId tenantId, long id, string resourceType, decimal maxQuantity)
        : base(tenantId)
    {
        SetId(id);
        ResourceType = resourceType;
        MaxQuantity = maxQuantity;
    }

    /// <summary>资源类型（与 UsageRecord 对齐）</summary>
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>最大配额量</summary>
    [SugarColumn(ColumnDataType = "decimal(18,4)")]
    public decimal MaxQuantity { get; set; }

    /// <summary>是否启用配额限制</summary>
    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public void Update(decimal maxQuantity, bool isEnabled, DateTimeOffset now)
    {
        MaxQuantity = maxQuantity;
        IsEnabled = isEnabled;
        UpdatedAt = now;
    }
}

/// <summary>内置资源类型常量</summary>
public static class ResourceTypes
{
    public const string Users = "users";
    public const string Apps = "apps";
    public const string ApiCalls = "api_calls";
    public const string StorageMb = "storage_mb";
}
