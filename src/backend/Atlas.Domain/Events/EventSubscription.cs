using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Events;

/// <summary>
/// 事件订阅规则（配置哪些事件路由到哪个目标）
/// </summary>
public sealed class EventSubscription : TenantEntity
{
    public EventSubscription()
        : base(TenantId.Empty)
    {
    }

    public string Name { get; set; } = string.Empty;

    /// <summary>事件类型模式（精确匹配或通配符 *）</summary>
    public string EventTypePattern { get; set; } = "*";

    public EventSubscriptionTargetType TargetType { get; set; }

    /// <summary>目标配置 JSON（队列名、Webhook ID 等）</summary>
    public string TargetConfig { get; set; } = "{}";

    /// <summary>可选过滤表达式（JSON Path 条件）</summary>
    public string? FilterExpression { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public enum EventSubscriptionTargetType
{
    Queue = 0,
    Webhook = 1,
    Handler = 2
}
