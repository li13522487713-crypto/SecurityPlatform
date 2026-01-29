using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Workflow.Entities;

public sealed class PersistedSubscription : TenantEntity
{
    public PersistedSubscription()
        : base(TenantId.Empty)
    {
        WorkflowId = string.Empty;
        EventName = string.Empty;
        EventKey = string.Empty;
        SubscriptionDataJson = null;
        EventKeySlug = null;
    }

    public PersistedSubscription(TenantId tenantId, string workflowId, int stepId, string eventName, string eventKey, long id, string? subscriptionDataJson = null)
        : base(tenantId)
    {
        Id = id;
        WorkflowId = workflowId;
        StepId = stepId;
        EventName = eventName;
        EventKey = eventKey;
        SubscriptionDataJson = subscriptionDataJson;
        SubscribeAsOf = DateTimeOffset.UtcNow;
        EventKeySlug = null;
    }

    public string WorkflowId { get; private set; }

    public int StepId { get; private set; }

    public string EventName { get; private set; }

    public string EventKey { get; private set; }

    public DateTimeOffset SubscribeAsOf { get; private set; }

    public string? SubscriptionDataJson { get; private set; }

    public string? EventKeySlug { get; private set; }
}
