using Atlas.Core.Abstractions;
using SqlSugar;

namespace Atlas.Core.Messaging;

[SugarTable("sys_outbox_message")]
public sealed class OutboxMessage : EntityBase
{
    [SugarColumn(IsPrimaryKey = true)]
    public new long Id { get => base.Id; set => SetId(value); }

    [SugarColumn(Length = 200)]
    public string EventType { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "text")]
    public string PayloadJson { get; set; } = "{}";

    [SugarColumn(Length = 200, IsNullable = true)]
    public string? DestinationQueue { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public bool IsProcessed { get; set; }

    public int RetryCount { get; set; }

    public int MaxRetries { get; set; } = 3;

    public DateTime? NextRetryAt { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ErrorMessage { get; set; }

    [SugarColumn(Length = 100, IsNullable = true)]
    public string? CorrelationId { get; set; }
}
