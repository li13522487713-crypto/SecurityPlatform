using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LogicFlow.Nodes;

/// <summary>
/// 业务模板块——包含多个节点与连线的可复用流程片段。
/// </summary>
public sealed class BusinessTemplateBlock : TenantEntity
{
    public BusinessTemplateBlock()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
    }

    public BusinessTemplateBlock(TenantId tenantId, long id, string name)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        CreatedAt = DateTime.UtcNow;
    }

    [SugarColumn(Length = 200, IsNullable = false)]
    public string Name { get; private set; }

    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Description { get; private set; }

    /// <summary>
    /// 模板中包含的节点实例列表（JSON 序列化）。
    /// </summary>
    [SugarColumn(IsJson = true, ColumnDataType = "text", IsNullable = true)]
    public List<TemplateNodeInstance>? Nodes { get; private set; }

    /// <summary>
    /// 模板中节点之间的连线定义（JSON 序列化）。
    /// </summary>
    [SugarColumn(IsJson = true, ColumnDataType = "text", IsNullable = true)]
    public List<TemplateEdge>? Edges { get; private set; }

    [SugarColumn(IsJson = true, ColumnDataType = "text", IsNullable = true)]
    public List<PortDefinition>? InputPorts { get; private set; }

    [SugarColumn(IsJson = true, ColumnDataType = "text", IsNullable = true)]
    public List<PortDefinition>? OutputPorts { get; private set; }

    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Tags { get; private set; }

    public bool IsPublic { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    public void Update(
        string name,
        string? description,
        List<TemplateNodeInstance>? nodes,
        List<TemplateEdge>? edges,
        List<PortDefinition>? inputPorts,
        List<PortDefinition>? outputPorts,
        string? tags,
        bool isPublic)
    {
        Name = name;
        Description = description;
        Nodes = nodes;
        Edges = edges;
        InputPorts = inputPorts;
        OutputPorts = outputPorts;
        Tags = tags;
        IsPublic = isPublic;
        UpdatedAt = DateTime.UtcNow;
    }
}

public sealed class TemplateNodeInstance
{
    public string NodeKey { get; set; } = string.Empty;
    public string NodeTypeKey { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    [SugarColumn(IsJson = true, ColumnDataType = "text", IsNullable = true)]
    public NodeConfigSchema? Config { get; set; }
}

public sealed class TemplateEdge
{
    public string SourceNodeKey { get; set; } = string.Empty;
    public string SourcePortKey { get; set; } = string.Empty;
    public string TargetNodeKey { get; set; } = string.Empty;
    public string TargetPortKey { get; set; } = string.Empty;
}
