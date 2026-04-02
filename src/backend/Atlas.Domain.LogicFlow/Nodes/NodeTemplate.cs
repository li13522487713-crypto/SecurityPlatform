using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LogicFlow.Nodes;

/// <summary>
/// 单节点模板——预设了特定配置的节点类型实例化模板。
/// </summary>
public sealed class NodeTemplate : TenantEntity
{
    public NodeTemplate()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        NodeTypeKey = string.Empty;
    }

    public NodeTemplate(TenantId tenantId, long id, string name, string nodeTypeKey)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        NodeTypeKey = nodeTypeKey;
        CreatedAt = DateTime.UtcNow;
    }

    [SugarColumn(Length = 200, IsNullable = false)]
    public string Name { get; private set; }

    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Description { get; private set; }

    [SugarColumn(Length = 128, IsNullable = false)]
    public string NodeTypeKey { get; private set; }

    public NodeCategory Category { get; private set; }

    [SugarColumn(IsJson = true, ColumnDataType = "text", IsNullable = true)]
    public NodeConfigSchema? PresetConfig { get; private set; }

    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Tags { get; private set; }

    public bool IsPublic { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    public void Update(string name, string? description, NodeConfigSchema? presetConfig, string? tags, bool isPublic)
    {
        Name = name;
        Description = description;
        PresetConfig = presetConfig;
        Tags = tags;
        IsPublic = isPublic;
        UpdatedAt = DateTime.UtcNow;
    }
}
