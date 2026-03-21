using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.Subscription;

/// <summary>
/// 套餐定义（平台级，由平台管理员维护）
/// </summary>
public sealed class Plan : EntityBase
{
    public Plan()
    {
    }

    public Plan(long id, string code, string name, string description)
    {
        SetId(id);
        Code = code;
        Name = name;
        Description = description;
    }

    /// <summary>套餐编码（唯一标识，如 starter/pro/enterprise）</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "text")]
    public string Description { get; set; } = string.Empty;

    /// <summary>套餐月价格（人民币，0=免费）</summary>
    [SugarColumn(ColumnDataType = "decimal(10,2)")]
    public decimal MonthlyPrice { get; set; }

    /// <summary>套餐配额 JSON（与 TenantQuota ResourceType 对齐的 key-value）</summary>
    [SugarColumn(ColumnDataType = "text")]
    public string QuotaJson { get; set; } = "{}";

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// 租户订阅关系
/// </summary>
public sealed class TenantSubscription : TenantEntity
{
    public TenantSubscription()
        : base(TenantId.Empty)
    {
    }

    public TenantSubscription(TenantId tenantId, long id, long planId, DateTimeOffset startAt, DateTimeOffset? expiresAt)
        : base(tenantId)
    {
        SetId(id);
        PlanId = planId;
        StartAt = startAt;
        ExpiresAt = expiresAt;
        Status = SubscriptionStatus.Active;
    }

    public long PlanId { get; set; }

    public SubscriptionStatus Status { get; set; }

    public DateTimeOffset StartAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsExpired(DateTimeOffset now) =>
        ExpiresAt.HasValue && now > ExpiresAt.Value;

    public void Cancel(DateTimeOffset now)
    {
        Status = SubscriptionStatus.Cancelled;
        UpdatedAt = now;
    }

    public void Renew(DateTimeOffset newExpiresAt, DateTimeOffset now)
    {
        ExpiresAt = newExpiresAt;
        Status = SubscriptionStatus.Active;
        UpdatedAt = now;
    }
}

public enum SubscriptionStatus
{
    Active = 0,
    Expired = 1,
    Cancelled = 2,
    Suspended = 3
}
