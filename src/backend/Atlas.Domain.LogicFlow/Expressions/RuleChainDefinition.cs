using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LogicFlow.Expressions;

/// <summary>
/// 规则链定义实体 —— 持久化 if/else/switch 条件链。
/// </summary>
[SugarTable("lf_rule_chain")]
public sealed class RuleChainDefinition : TenantEntity
{
    public RuleChainDefinition() : base(default) { }

    public RuleChainDefinition(TenantId tenantId, string name) : base(tenantId)
    {
        Name = name;
    }

    [SugarColumn(IsPrimaryKey = true)]
    public new long Id { get => base.Id; set => SetId(value); }

    [SugarColumn(Length = 100)]
    public string Name { get; set; } = string.Empty;

    [SugarColumn(Length = 200)]
    public string? DisplayName { get; set; }

    [SugarColumn(Length = 500)]
    public string? Description { get; set; }

    /// <summary>JSON：规则步骤列表。</summary>
    [SugarColumn(ColumnDataType = "text")]
    public string StepsJson { get; set; } = "[]";

    /// <summary>无规则命中时的默认输出表达式。</summary>
    [SugarColumn(Length = 500)]
    public string? DefaultOutputExpression { get; set; }

    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [SugarColumn(Length = 100)]
    public string? CreatedBy { get; set; }

    [SugarColumn(Length = 100)]
    public string? UpdatedBy { get; set; }
}
