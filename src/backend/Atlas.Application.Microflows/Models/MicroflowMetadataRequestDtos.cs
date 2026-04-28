using System.Text.Json.Serialization;

namespace Atlas.Application.Microflows.Models;

public record GetMicroflowMetadataRequestDto
{
    [JsonPropertyName("workspaceId")]
    public string? WorkspaceId { get; init; }

    [JsonPropertyName("moduleId")]
    public string? ModuleId { get; init; }

    [JsonPropertyName("includeSystem")]
    public bool IncludeSystem { get; init; } = true;

    [JsonPropertyName("includeArchived")]
    public bool IncludeArchived { get; init; }
}

public sealed record GetMicroflowRefsRequestDto : GetMicroflowMetadataRequestDto
{
    [JsonPropertyName("status")]
    public IReadOnlyList<string> Status { get; init; } = Array.Empty<string>();

    [JsonPropertyName("keyword")]
    public string? Keyword { get; init; }
}

public sealed record GetPageRefsRequestDto
{
    [JsonPropertyName("workspaceId")]
    public string? WorkspaceId { get; init; }

    [JsonPropertyName("moduleId")]
    public string? ModuleId { get; init; }

    [JsonPropertyName("keyword")]
    public string? Keyword { get; init; }
}

public sealed record GetWorkflowRefsRequestDto
{
    [JsonPropertyName("workspaceId")]
    public string? WorkspaceId { get; init; }

    [JsonPropertyName("moduleId")]
    public string? ModuleId { get; init; }

    [JsonPropertyName("contextEntityQualifiedName")]
    public string? ContextEntityQualifiedName { get; init; }

    [JsonPropertyName("keyword")]
    public string? Keyword { get; init; }
}

public sealed record MicroflowMetadataHealthDto
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "ok";

    [JsonPropertyName("cacheExists")]
    public bool CacheExists { get; init; }

    [JsonPropertyName("catalogVersion")]
    public string? CatalogVersion { get; init; }

    [JsonPropertyName("entityCount")]
    public int EntityCount { get; init; }

    [JsonPropertyName("associationCount")]
    public int AssociationCount { get; init; }

    [JsonPropertyName("enumerationCount")]
    public int EnumerationCount { get; init; }

    [JsonPropertyName("microflowRefCount")]
    public int MicroflowRefCount { get; init; }

    [JsonPropertyName("pageCount")]
    public int PageCount { get; init; }

    [JsonPropertyName("workflowCount")]
    public int WorkflowCount { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? UpdatedAt { get; init; }

    [JsonPropertyName("source")]
    public string Source { get; init; } = "seed";
}
