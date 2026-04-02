using Atlas.Core.Abstractions;
using Atlas.Core.Expressions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LogicFlow.Expressions;

/// <summary>
/// 函数定义实体 —— 持久化用户自定义函数或内置函数元数据。
/// </summary>
[SugarTable("lf_function_definition")]
public sealed class FunctionDefinition : TenantEntity
{
    public FunctionDefinition() : base(default) { }

    public FunctionDefinition(TenantId tenantId, string name, FunctionCategory category) : base(tenantId)
    {
        Name = name;
        Category = category;
    }

    [SugarColumn(IsPrimaryKey = true)]
    public new long Id { get => base.Id; set => SetId(value); }

    [SugarColumn(Length = 100)]
    public string Name { get; set; } = string.Empty;

    [SugarColumn(Length = 200)]
    public string? DisplayName { get; set; }

    [SugarColumn(Length = 500)]
    public string? Description { get; set; }

    public FunctionCategory Category { get; set; }

    /// <summary>JSON 序列化的参数签名列表。</summary>
    [SugarColumn(ColumnDataType = "text")]
    public string ParametersJson { get; set; } = "[]";

    /// <summary>返回类型。</summary>
    public ExprType ReturnType { get; set; } = ExprType.Any;

    /// <summary>函数体表达式（内置函数为空，自定义函数为表达式文本）。</summary>
    [SugarColumn(ColumnDataType = "text")]
    public string? BodyExpression { get; set; }

    /// <summary>是否为系统内置函数。</summary>
    public bool IsBuiltin { get; set; }

    /// <summary>是否启用。</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>排序权重。</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [SugarColumn(Length = 100)]
    public string? CreatedBy { get; set; }

    [SugarColumn(Length = 100)]
    public string? UpdatedBy { get; set; }
}
