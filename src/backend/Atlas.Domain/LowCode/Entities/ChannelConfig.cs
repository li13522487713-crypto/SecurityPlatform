using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// 消息渠道配置（SMTP、SMS、Webhook 等）
/// </summary>
public sealed class ChannelConfig : TenantEntity
{
    public ChannelConfig() : base(TenantId.Empty) { Channel = string.Empty; ConfigJson = string.Empty; }

    public ChannelConfig(TenantId tenantId, string channel, string configJson, bool isActive, long id, DateTimeOffset now) : base(tenantId)
    {
        Id = id; Channel = channel; ConfigJson = configJson; IsActive = isActive;
        CreatedAt = now; UpdatedAt = now;
    }

    public string Channel { get; private set; }
    public string ConfigJson { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(string configJson, bool isActive, DateTimeOffset now)
    {
        ConfigJson = configJson; IsActive = isActive; UpdatedAt = now;
    }
}
