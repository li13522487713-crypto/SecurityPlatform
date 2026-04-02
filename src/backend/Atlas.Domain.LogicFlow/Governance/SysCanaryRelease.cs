using Atlas.Core.Abstractions;
using SqlSugar;

namespace Atlas.Domain.LogicFlow.Governance;

[SugarTable("sys_canary_release")]
public sealed class SysCanaryRelease : EntityBase
{
    [SugarColumn(IsPrimaryKey = true)]
    public new long Id { get => base.Id; set => SetId(value); }

    [SugarColumn(Length = 100)]
    public string FeatureKey { get; set; } = string.Empty;

    public int RolloutPercentage { get; set; }

    public bool IsActive { get; set; }

    public DateTime? ActivatedAt { get; set; }
}
