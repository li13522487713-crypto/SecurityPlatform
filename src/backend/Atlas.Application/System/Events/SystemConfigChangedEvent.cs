using Atlas.Core.Events;
using Atlas.Core.Tenancy;

namespace Atlas.Application.System.Events;

public sealed record SystemConfigChangedEvent : IDomainEvent
{
    public SystemConfigChangedEvent(
        TenantId tenantId,
        string configKey,
        string? appId,
        string? oldValue,
        string? newValue)
    {
        TenantId = tenantId;
        ConfigKey = configKey;
        AppId = appId;
        OldValue = oldValue;
        NewValue = newValue;
    }

    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;

    public TenantId TenantId { get; }

    public string ConfigKey { get; }

    public string? AppId { get; }

    public string? OldValue { get; }

    public string? NewValue { get; }
}
