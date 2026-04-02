using Atlas.Domain.LogicFlow.Nodes;

namespace Atlas.Application.LogicFlow.Nodes.Models;

public sealed class NodeTemplateQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public NodeCategory? Category { get; set; }
}

public sealed record NodeTemplateListItem(
    string Id,
    string Name,
    string? Description,
    string NodeTypeKey,
    NodeCategory Category,
    string? Tags,
    bool IsPublic,
    DateTime CreatedAt);

public sealed record NodeTemplateDetailResponse(
    string Id,
    string Name,
    string? Description,
    string NodeTypeKey,
    NodeCategory Category,
    NodeConfigSchema? PresetConfig,
    string? Tags,
    bool IsPublic,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed class NodeTemplateCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string NodeTypeKey { get; set; } = string.Empty;
    public NodeCategory Category { get; set; }
    public NodeConfigSchema? PresetConfig { get; set; }
    public string? Tags { get; set; }
    public bool IsPublic { get; set; }
}

public sealed class NodeTemplateUpdateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public NodeConfigSchema? PresetConfig { get; set; }
    public string? Tags { get; set; }
    public bool IsPublic { get; set; }
}
