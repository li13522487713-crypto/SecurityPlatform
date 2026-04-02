using Atlas.Core.Abstractions;
using SqlSugar;

namespace Atlas.Core.Messaging;

[SugarTable("sys_inbox_message")]
[SugarIndex("UX_sys_inbox_message_message_id", nameof(MessageId), OrderByType.Asc, true)]
public sealed class InboxMessage : EntityBase
{
    [SugarColumn(IsPrimaryKey = true)]
    public new long Id { get => base.Id; set => SetId(value); }

    public Guid MessageId { get; set; }

    [SugarColumn(Length = 200)]
    public string HandlerType { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "text")]
    public string PayloadJson { get; set; } = "{}";

    public bool IsProcessed { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    [SugarColumn(Length = 200, IsNullable = true)]
    public string? IdempotencyKey { get; set; }
}
