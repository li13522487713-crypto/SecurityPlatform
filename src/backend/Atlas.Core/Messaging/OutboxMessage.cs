using Atlas.Core.Abstractions;

namespace Atlas.Core.Messaging;

public sealed class OutboxMessage : EntityBase
{
    public new long Id { get => base.Id; set => SetId(value); }

    public string EventType { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = "{}";

    public string? DestinationQueue { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public bool IsProcessed { get; set; }

    public int RetryCount { get; set; }

    public int MaxRetries { get; set; } = 3;

    public DateTime? NextRetryAt { get; set; }

    public string? ErrorMessage { get; set; }

    public string? CorrelationId { get; set; }
}
