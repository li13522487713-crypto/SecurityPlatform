using Atlas.Domain.LogicFlow.Nodes;

namespace Atlas.Application.LogicFlow.Nodes.Models;

public sealed class NodeTypeQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public NodeCategory? Category { get; set; }
    public bool? IsBuiltIn { get; set; }
}

public sealed record NodeTypeListItem(
    string Id,
    string TypeKey,
    NodeCategory Category,
    string DisplayName,
    string? Description,
    string Version,
    bool IsBuiltIn,
    bool IsActive,
    DateTime CreatedAt);

public sealed record NodeTypeDetailResponse(
    string Id,
    string TypeKey,
    NodeCategory Category,
    string DisplayName,
    string? Description,
    string Version,
    bool IsBuiltIn,
    bool IsActive,
    List<PortDefinition> Ports,
    NodeConfigSchema? ConfigSchema,
    NodeCapability? Capabilities,
    NodeUiMetadata? UiMetadata,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed class NodeTypeCreateRequest
{
    public string TypeKey { get; set; } = string.Empty;
    public NodeCategory Category { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<PortDefinition>? Ports { get; set; }
    public NodeConfigSchema? ConfigSchema { get; set; }
    public NodeCapability? Capabilities { get; set; }
    public NodeUiMetadata? UiMetadata { get; set; }
}

public sealed class NodeTypeUpdateRequest
{
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<PortDefinition>? Ports { get; set; }
    public NodeConfigSchema? ConfigSchema { get; set; }
    public NodeCapability? Capabilities { get; set; }
    public NodeUiMetadata? UiMetadata { get; set; }
}

public sealed record NodeCategoryInfo(
    NodeCategory Category,
    string DisplayName,
    int Count);
