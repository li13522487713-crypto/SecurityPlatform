using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.Events;

/// <summary>
/// 平台统一事件模型（对标 CloudMonitor/EventBridge 事件结构）
/// </summary>
public sealed class PlatformEvent : TenantEntity
{
    public PlatformEvent()
        : base(TenantId.Empty)
    {
    }

    public PlatformEvent(
        TenantId tenantId,
        long id,
        string eventType,
        string source,
        string payloadJson,
        DateTimeOffset occurredAt)
        : base(tenantId)
    {
        SetId(id);
        EventType = eventType;
        Source = source;
        PayloadJson = payloadJson;
        OccurredAt = occurredAt;
        CreatedAt = occurredAt;
    }

    /// <summary>事件类型（如 tenant.expired、release.created、user.login.failed）</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>事件来源（如 TenantService、ReleaseCommandService）</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>事件 Payload（JSON 格式）</summary>
    [SugarColumn(ColumnDataType = "text")]
    public string PayloadJson { get; set; } = "{}";

    /// <summary>事件发生时间</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>是否已被至少一个订阅者处理</summary>
    public bool IsProcessed { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTimeOffset? ProcessedAt { get; set; }

    public void MarkProcessed(DateTimeOffset at)
    {
        IsProcessed = true;
        ProcessedAt = at;
    }
}
