using Atlas.Core.Abstractions;
using SqlSugar;

namespace Atlas.Core.Messaging;

[SugarTable("sys_idempotency_record")]
[SugarIndex(
    "UX_sys_idempotency_record_key",
    nameof(TenantId),
    OrderByType.Asc,
    nameof(UserId),
    OrderByType.Asc,
    nameof(ApiName),
    OrderByType.Asc,
    nameof(IdempotencyKey),
    OrderByType.Asc,
    true)]
public sealed class IdempotencyRecord : EntityBase
{
    [SugarColumn(IsPrimaryKey = true)]
    public new long Id { get => base.Id; set => SetId(value); }

    public Guid TenantId { get; set; }

    [SugarColumn(Length = 100)]
    public string UserId { get; set; } = string.Empty;

    [SugarColumn(Length = 200)]
    public string ApiName { get; set; } = string.Empty;

    [SugarColumn(Length = 200)]
    public string IdempotencyKey { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "text")]
    public string ResponseJson { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }
}
