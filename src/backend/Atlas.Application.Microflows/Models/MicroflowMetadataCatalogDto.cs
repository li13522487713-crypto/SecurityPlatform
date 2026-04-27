using System.Text.Json.Serialization;

namespace Atlas.Application.Microflows.Models;

public sealed record MicroflowMetadataCatalogDto
{
    [JsonPropertyName("modules")]
    public IReadOnlyList<MetadataModuleDto> Modules { get; init; } = Array.Empty<MetadataModuleDto>();

    [JsonPropertyName("entities")]
    public IReadOnlyList<MetadataEntityDto> Entities { get; init; } = Array.Empty<MetadataEntityDto>();

    [JsonPropertyName("associations")]
    public IReadOnlyList<MetadataAssociationDto> Associations { get; init; } = Array.Empty<MetadataAssociationDto>();

    [JsonPropertyName("enumerations")]
    public IReadOnlyList<MetadataEnumerationDto> Enumerations { get; init; } = Array.Empty<MetadataEnumerationDto>();

    [JsonPropertyName("microflows")]
    public IReadOnlyList<MetadataMicroflowRefDto> Microflows { get; init; } = Array.Empty<MetadataMicroflowRefDto>();

    [JsonPropertyName("pages")]
    public IReadOnlyList<MetadataPageRefDto> Pages { get; init; } = Array.Empty<MetadataPageRefDto>();

    [JsonPropertyName("workflows")]
    public IReadOnlyList<MetadataWorkflowRefDto> Workflows { get; init; } = Array.Empty<MetadataWorkflowRefDto>();

    [JsonPropertyName("connectors")]
    public IReadOnlyList<MetadataConnectorDto> Connectors { get; init; } = Array.Empty<MetadataConnectorDto>();

    [JsonPropertyName("version")]
    public string Version { get; init; } = "backend-skeleton";

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record MetadataModuleDto(string Id, string Name, string QualifiedName);

public sealed record MetadataEntityDto(string Id, string Name, string QualifiedName, string ModuleName);

public sealed record MetadataAssociationDto(string Id, string Name, string QualifiedName);

public sealed record MetadataEnumerationDto(string Id, string Name, string QualifiedName, string ModuleName);

public sealed record MetadataMicroflowRefDto(string Id, string Name, string QualifiedName, string ModuleName);

public sealed record MetadataPageRefDto(string Id, string Name, string QualifiedName, string ModuleName);

public sealed record MetadataWorkflowRefDto(string Id, string Name, string QualifiedName, string ModuleName);

public sealed record MetadataConnectorDto(string Id, string Name, string Type, bool Enabled);
