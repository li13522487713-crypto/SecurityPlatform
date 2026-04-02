using Atlas.Core.Abstractions;
using Atlas.Core.Expressions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LogicFlow.Expressions;

/// <summary>
/// 决策表定义实体 —— 持久化行×列规则矩阵。
/// </summary>
[SugarTable("lf_decision_table")]
public sealed class DecisionTableDefinition : TenantEntity
{
    public DecisionTableDefinition() : base(default) { }

    public DecisionTableDefinition(TenantId tenantId, string name) : base(tenantId)
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

    public DecisionHitPolicy HitPolicy { get; set; } = DecisionHitPolicy.First;

    /// <summary>JSON：输入列定义。</summary>
    [SugarColumn(ColumnDataType = "text")]
    public string InputColumnsJson { get; set; } = "[]";

    /// <summary>JSON：输出列定义。</summary>
    [SugarColumn(ColumnDataType = "text")]
    public string OutputColumnsJson { get; set; } = "[]";

    /// <summary>JSON：行规则数组。</summary>
    [SugarColumn(ColumnDataType = "text")]
    public string RowsJson { get; set; } = "[]";

    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [SugarColumn(Length = 100)]
    public string? CreatedBy { get; set; }

    [SugarColumn(Length = 100)]
    public string? UpdatedBy { get; set; }
}
