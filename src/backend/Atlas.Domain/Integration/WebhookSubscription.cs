using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Integration;

/// <summary>
/// Webhook 订阅（外部系统注册接收平台事件回调）
/// </summary>
public sealed class WebhookSubscription : TenantEntity
{
    public WebhookSubscription()
        : base(TenantId.Empty)
    {
    }

    public string Name { get; set; } = string.Empty;

    /// <summary>订阅的事件类型，JSON 数组（如 ["approval.completed","approval.rejected"]）</summary>
    public string EventTypes { get; set; } = "[]";

    public string TargetUrl { get; set; } = string.Empty;

    /// <summary>HMAC-SHA256 签名密钥</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>额外请求头，JSON 对象</summary>
    public string? Headers { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastTriggeredAt { get; set; }
}
