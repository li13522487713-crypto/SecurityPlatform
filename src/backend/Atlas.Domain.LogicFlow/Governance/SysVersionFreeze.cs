using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LogicFlow.Governance;

[SugarTable("sys_version_freeze")]
public sealed class SysVersionFreeze : TenantEntity
{
    public SysVersionFreeze() : base(default) { }

    public SysVersionFreeze(
        TenantId tenantId,
        string resourceType,
        long resourceId,
        string reason,
        string frozenBy,
        DateTime frozenAt)
        : base(tenantId)
    {
        ResourceType = resourceType;
        ResourceId = resourceId;
        Reason = reason;
        FrozenBy = frozenBy;
        FrozenAt = frozenAt;
    }

    [SugarColumn(IsPrimaryKey = true)]
    public new long Id { get => base.Id; set => SetId(value); }

    [SugarColumn(Length = 100)]
    public string ResourceType { get; set; } = string.Empty;

    public long ResourceId { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? Reason { get; set; }

    [SugarColumn(Length = 100)]
    public string FrozenBy { get; set; } = string.Empty;

    public DateTime FrozenAt { get; set; }
}
