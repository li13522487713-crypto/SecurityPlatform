using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LogicFlow.Nodes;

/// <summary>
/// 统一节点类型定义——逻辑流中每种节点的完整元模型。
/// </summary>
public sealed class NodeTypeDefinition : TenantEntity
{
    public NodeTypeDefinition()
        : base(TenantId.Empty)
    {
        TypeKey = string.Empty;
        DisplayName = string.Empty;
        Version = "1.0.0";
        Ports = new List<PortDefinition>();
    }

    public NodeTypeDefinition(
        TenantId tenantId,
        long id,
        string typeKey,
        NodeCategory category,
        string displayName)
        : base(tenantId)
    {
        Id = id;
        TypeKey = typeKey;
        Category = category;
        DisplayName = displayName;
        Version = "1.0.0";
        IsActive = true;
        Ports = new List<PortDefinition>();
        CreatedAt = DateTime.UtcNow;
    }

    [SugarColumn(Length = 128, IsNullable = false)]
    public string TypeKey { get; private set; }

    public NodeCategory Category { get; private set; }

    [SugarColumn(Length = 200, IsNullable = false)]
    public string DisplayName { get; private set; }

    [SugarColumn(Length = 1000, IsNullable = true)]
    public string? Description { get; private set; }

    [SugarColumn(Length = 32, IsNullable = false)]
    public string Version { get; private set; }

    public bool IsBuiltIn { get; private set; }

    public bool IsActive { get; private set; }

    [SugarColumn(IsJson = true, ColumnDataType = "text")]
    public List<PortDefinition> Ports { get; private set; }

    [SugarColumn(IsJson = true, ColumnDataType = "text", IsNullable = true)]
    public NodeConfigSchema? ConfigSchema { get; private set; }

    [SugarColumn(IsJson = true, ColumnDataType = "text", IsNullable = true)]
    public NodeCapability? Capabilities { get; private set; }

    [SugarColumn(IsJson = true, ColumnDataType = "text", IsNullable = true)]
    public NodeUiMetadata? UiMetadata { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    public void Update(
        string displayName,
        string? description,
        List<PortDefinition> ports,
        NodeConfigSchema? configSchema,
        NodeCapability? capabilities,
        NodeUiMetadata? uiMetadata)
    {
        DisplayName = displayName;
        Description = description;
        Ports = ports;
        ConfigSchema = configSchema;
        Capabilities = capabilities;
        UiMetadata = uiMetadata;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkBuiltIn()
    {
        IsBuiltIn = true;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
