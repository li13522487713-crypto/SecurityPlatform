namespace Atlas.WorkflowCore.Models;

public class EventSubscription
{
    public string Id { get; set; } = string.Empty;

    public string WorkflowId { get; set; } = string.Empty;

    public int StepId { get; set; }

    public string EventName { get; set; } = string.Empty;

    public string EventKey { get; set; } = string.Empty;

    public DateTime SubscribeAsOf { get; set; }

    public string? SubscriptionData { get; set; }

    public string? EventKeySlug { get; set; }
}
