using System.Text.Json.Serialization;

namespace Atlas.Application.Microflows.Models;

public sealed record MicroflowAppAssetDto
{
    [JsonPropertyName("appId")]
    public string AppId { get; init; } = string.Empty;

    [JsonPropertyName("workspaceId")]
    public string WorkspaceId { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = "draft";

    [JsonPropertyName("modules")]
    public IReadOnlyList<MicroflowModuleAssetDto> Modules { get; init; } = Array.Empty<MicroflowModuleAssetDto>();
}

public sealed record MicroflowModuleAssetDto
{
    [JsonPropertyName("moduleId")]
    public string ModuleId { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("qualifiedName")]
    public string QualifiedName { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("pages")]
    public IReadOnlyList<MicroflowPageAssetSummaryDto> Pages { get; init; } = Array.Empty<MicroflowPageAssetSummaryDto>();

    [JsonPropertyName("workflows")]
    public IReadOnlyList<MicroflowWorkflowAssetSummaryDto> Workflows { get; init; } = Array.Empty<MicroflowWorkflowAssetSummaryDto>();

    [JsonPropertyName("entities")]
    public IReadOnlyList<MicroflowDomainEntitySummaryDto> Entities { get; init; } = Array.Empty<MicroflowDomainEntitySummaryDto>();

    [JsonPropertyName("security")]
    public MicroflowSecurityAssetSummaryDto? Security { get; init; }
}

public sealed record MicroflowPageAssetSummaryDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("qualifiedName")]
    public string QualifiedName { get; init; } = string.Empty;

    [JsonPropertyName("moduleName")]
    public string ModuleName { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("parameterCount")]
    public int ParameterCount { get; init; }
}

public sealed record MicroflowWorkflowAssetSummaryDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("qualifiedName")]
    public string QualifiedName { get; init; } = string.Empty;

    [JsonPropertyName("moduleName")]
    public string ModuleName { get; init; } = string.Empty;

    [JsonPropertyName("contextEntityQualifiedName")]
    public string? ContextEntityQualifiedName { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

public sealed record MicroflowDomainEntitySummaryDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("qualifiedName")]
    public string QualifiedName { get; init; } = string.Empty;

    [JsonPropertyName("moduleName")]
    public string ModuleName { get; init; } = string.Empty;

    [JsonPropertyName("attributeCount")]
    public int AttributeCount { get; init; }

    [JsonPropertyName("associationCount")]
    public int AssociationCount { get; init; }

    [JsonPropertyName("isPersistable")]
    public bool IsPersistable { get; init; } = true;
}

public sealed record MicroflowSecurityAssetSummaryDto
{
    [JsonPropertyName("moduleId")]
    public string ModuleId { get; init; } = string.Empty;

    [JsonPropertyName("moduleName")]
    public string ModuleName { get; init; } = string.Empty;

    [JsonPropertyName("entityAccessCount")]
    public int EntityAccessCount { get; init; }

    [JsonPropertyName("readonly")]
    public bool Readonly { get; init; } = true;
}
