using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LogicFlow.Governance;

[SugarTable("sys_quota")]
public sealed class SysQuota : TenantEntity
{
    public SysQuota() : base(default) { }

    public SysQuota(TenantId tenantId, string resourceType, int limit, int used)
        : base(tenantId)
    {
        ResourceType = resourceType;
        Limit = limit;
        Used = used;
    }

    [SugarColumn(IsPrimaryKey = true)]
    public new long Id { get => base.Id; set => SetId(value); }

    [SugarColumn(Length = 100)]
    public string ResourceType { get; set; } = string.Empty;

    public int Limit { get; set; }

    public int Used { get; set; }
}
