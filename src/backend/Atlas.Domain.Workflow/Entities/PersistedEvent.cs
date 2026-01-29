using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Workflow.Entities;

public sealed class PersistedEvent : TenantEntity
{
    public PersistedEvent()
        : base(TenantId.Empty)
    {
        EventName = string.Empty;
        EventKey = string.Empty;
        EventDataJson = null;
    }

    public PersistedEvent(TenantId tenantId, string eventName, string eventKey, long id, string? eventDataJson = null)
        : base(tenantId)
    {
        Id = id;
        EventName = eventName;
        EventKey = eventKey;
        EventDataJson = eventDataJson;
        EventTime = DateTimeOffset.UtcNow;
        IsProcessed = false;
    }

    public string EventName { get; private set; }

    public string EventKey { get; private set; }

    public string? EventDataJson { get; private set; }

    public DateTimeOffset EventTime { get; private set; }

    public bool IsProcessed { get; private set; }

    public void MarkAsProcessed()
    {
        IsProcessed = true;
    }
}
