using Atlas.Core.Abstractions;

namespace Atlas.Core.Messaging;

public sealed class InboxMessage : EntityBase
{
    public new long Id { get => base.Id; set => SetId(value); }

    public Guid MessageId { get; set; }

    public string HandlerType { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = "{}";

    public bool IsProcessed { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? IdempotencyKey { get; set; }
}
