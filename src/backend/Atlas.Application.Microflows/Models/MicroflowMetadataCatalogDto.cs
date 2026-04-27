using System.Text.Json;
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

public sealed record MetadataModuleDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("qualifiedName")]
    public string QualifiedName { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

public sealed record MetadataEntityDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("qualifiedName")]
    public string QualifiedName { get; init; } = string.Empty;

    [JsonPropertyName("moduleName")]
    public string ModuleName { get; init; } = string.Empty;

    [JsonPropertyName("documentation")]
    public string? Documentation { get; init; }

    [JsonPropertyName("attributes")]
    public IReadOnlyList<MetadataAttributeDto> Attributes { get; init; } = Array.Empty<MetadataAttributeDto>();

    [JsonPropertyName("associations")]
    public IReadOnlyList<MetadataAssociationRefDto> Associations { get; init; } = Array.Empty<MetadataAssociationRefDto>();

    [JsonPropertyName("generalization")]
    public string? Generalization { get; init; }

    [JsonPropertyName("specializations")]
    public IReadOnlyList<string> Specializations { get; init; } = Array.Empty<string>();

    [JsonPropertyName("isPersistable")]
    public bool IsPersistable { get; init; } = true;

    [JsonPropertyName("isSystemEntity")]
    public bool IsSystemEntity { get; init; }
}

public sealed record MetadataAttributeDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("qualifiedName")]
    public string QualifiedName { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public JsonElement Type { get; init; }

    [JsonPropertyName("required")]
    public bool Required { get; init; }

    [JsonPropertyName("defaultValue")]
    public string? DefaultValue { get; init; }

    [JsonPropertyName("documentation")]
    public string? Documentation { get; init; }

    [JsonPropertyName("enumQualifiedName")]
    public string? EnumQualifiedName { get; init; }

    [JsonPropertyName("isReadonly")]
    public bool IsReadonly { get; init; }
}

public sealed record MetadataAssociationRefDto
{
    [JsonPropertyName("associationQualifiedName")]
    public string AssociationQualifiedName { get; init; } = string.Empty;

    [JsonPropertyName("targetEntityQualifiedName")]
    public string TargetEntityQualifiedName { get; init; } = string.Empty;

    [JsonPropertyName("direction")]
    public string Direction { get; init; } = "sourceToTarget";

    [JsonPropertyName("multiplicity")]
    public string Multiplicity { get; init; } = "manyToOne";
}

public sealed record MetadataAssociationDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("qualifiedName")]
    public string QualifiedName { get; init; } = string.Empty;

    [JsonPropertyName("sourceEntityQualifiedName")]
    public string SourceEntityQualifiedName { get; init; } = string.Empty;

    [JsonPropertyName("targetEntityQualifiedName")]
    public string TargetEntityQualifiedName { get; init; } = string.Empty;

    [JsonPropertyName("ownerEntityQualifiedName")]
    public string? OwnerEntityQualifiedName { get; init; }

    [JsonPropertyName("multiplicity")]
    public string Multiplicity { get; init; } = "manyToOne";

    [JsonPropertyName("direction")]
    public string Direction { get; init; } = "sourceToTarget";

    [JsonPropertyName("documentation")]
    public string? Documentation { get; init; }
}

public sealed record MetadataEnumerationDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("qualifiedName")]
    public string QualifiedName { get; init; } = string.Empty;

    [JsonPropertyName("moduleName")]
    public string ModuleName { get; init; } = string.Empty;

    [JsonPropertyName("values")]
    public IReadOnlyList<MetadataEnumerationValueDto> Values { get; init; } = Array.Empty<MetadataEnumerationValueDto>();

    [JsonPropertyName("documentation")]
    public string? Documentation { get; init; }
}

public sealed record MetadataEnumerationValueDto
{
    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    [JsonPropertyName("caption")]
    public string Caption { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("colorToken")]
    public string? ColorToken { get; init; }
}

public sealed record MetadataMicroflowRefDto
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

    [JsonPropertyName("parameters")]
    public IReadOnlyList<MetadataMicroflowParameterDto> Parameters { get; init; } = Array.Empty<MetadataMicroflowParameterDto>();

    [JsonPropertyName("returnType")]
    public JsonElement ReturnType { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

public sealed record MetadataMicroflowParameterDto
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public JsonElement Type { get; init; }

    [JsonPropertyName("required")]
    public bool Required { get; init; }

    [JsonPropertyName("documentation")]
    public string? Documentation { get; init; }
}

public sealed record MetadataPageRefDto
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

    [JsonPropertyName("parameters")]
    public IReadOnlyList<MetadataPageParameterDto> Parameters { get; init; } = Array.Empty<MetadataPageParameterDto>();
}

public sealed record MetadataPageParameterDto
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public JsonElement Type { get; init; }

    [JsonPropertyName("required")]
    public bool Required { get; init; }
}

public sealed record MetadataWorkflowRefDto
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

public sealed record MetadataConnectorDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("capabilities")]
    public IReadOnlyList<string> Capabilities { get; init; } = Array.Empty<string>();
}
