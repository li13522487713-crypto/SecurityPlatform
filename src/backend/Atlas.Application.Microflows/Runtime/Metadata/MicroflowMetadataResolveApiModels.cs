using System.Text.Json;
using System.Text.Json.Serialization;
using Atlas.Application.Microflows.Runtime.Security;

namespace Atlas.Application.Microflows.Runtime.Metadata;

public sealed record MicroflowRuntimeMetadataResolveRequest
{
    [JsonPropertyName("schema")]
    public JsonElement? Schema { get; init; }

    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; init; }

    [JsonPropertyName("refs")]
    public IReadOnlyList<MicroflowMetadataResolveRefRequest> Refs { get; init; } = Array.Empty<MicroflowMetadataResolveRefRequest>();

    [JsonPropertyName("securityContext")]
    public MicroflowRuntimeSecurityContext? SecurityContext { get; init; }

    [JsonPropertyName("entities")]
    public IReadOnlyList<string> Entities { get; init; } = Array.Empty<string>();

    [JsonPropertyName("attributes")]
    public IReadOnlyList<MicroflowMetadataResolveMemberRequest> Attributes { get; init; } = Array.Empty<MicroflowMetadataResolveMemberRequest>();

    [JsonPropertyName("associations")]
    public IReadOnlyList<MicroflowMetadataResolveMemberRequest> Associations { get; init; } = Array.Empty<MicroflowMetadataResolveMemberRequest>();

    [JsonPropertyName("enumerations")]
    public IReadOnlyList<string> Enumerations { get; init; } = Array.Empty<string>();

    [JsonPropertyName("enumerationValues")]
    public IReadOnlyList<MicroflowMetadataResolveEnumerationValueRequest> EnumerationValues { get; init; } = Array.Empty<MicroflowMetadataResolveEnumerationValueRequest>();

    [JsonPropertyName("microflows")]
    public IReadOnlyList<MicroflowMetadataResolveMicroflowRequest> Microflows { get; init; } = Array.Empty<MicroflowMetadataResolveMicroflowRequest>();

    [JsonPropertyName("dataTypes")]
    public IReadOnlyList<JsonElement> DataTypes { get; init; } = Array.Empty<JsonElement>();

    [JsonPropertyName("memberPaths")]
    public IReadOnlyList<MicroflowMetadataResolveMemberPathRequest> MemberPaths { get; init; } = Array.Empty<MicroflowMetadataResolveMemberPathRequest>();

    [JsonPropertyName("entityAccessMode")]
    public string? EntityAccessMode { get; init; }
}

public sealed record MicroflowMetadataResolveRefRequest
{
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = "unknown";

    [JsonPropertyName("qualifiedName")]
    public string QualifiedName { get; init; } = string.Empty;

    [JsonPropertyName("required")]
    public bool Required { get; init; } = true;
}

public sealed record MicroflowMetadataResolveMemberRequest
{
    [JsonPropertyName("qualifiedName")]
    public string QualifiedName { get; init; } = string.Empty;

    [JsonPropertyName("entityQualifiedName")]
    public string? EntityQualifiedName { get; init; }
}

public sealed record MicroflowMetadataResolveEnumerationValueRequest
{
    [JsonPropertyName("enumerationQualifiedName")]
    public string EnumerationQualifiedName { get; init; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; init; } = string.Empty;
}

public sealed record MicroflowMetadataResolveMicroflowRequest
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("qualifiedName")]
    public string? QualifiedName { get; init; }
}

public sealed record MicroflowMetadataResolveMemberPathRequest
{
    [JsonPropertyName("rootType")]
    public JsonElement RootType { get; init; }

    [JsonPropertyName("memberPath")]
    public IReadOnlyList<string> MemberPath { get; init; } = Array.Empty<string>();
}

public sealed record MicroflowRuntimeMetadataResolveResponse
{
    [JsonPropertyName("catalogVersion")]
    public string CatalogVersion { get; init; } = string.Empty;

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? UpdatedAt { get; init; }

    [JsonPropertyName("resolutionReport")]
    public MicroflowMetadataResolutionReport ResolutionReport { get; init; } = new();

    [JsonPropertyName("entities")]
    public IReadOnlyList<MicroflowResolvedEntity> Entities { get; init; } = Array.Empty<MicroflowResolvedEntity>();

    [JsonPropertyName("attributes")]
    public IReadOnlyList<MicroflowResolvedAttribute> Attributes { get; init; } = Array.Empty<MicroflowResolvedAttribute>();

    [JsonPropertyName("associations")]
    public IReadOnlyList<MicroflowResolvedAssociation> Associations { get; init; } = Array.Empty<MicroflowResolvedAssociation>();

    [JsonPropertyName("enumerations")]
    public IReadOnlyList<MicroflowResolvedEnumeration> Enumerations { get; init; } = Array.Empty<MicroflowResolvedEnumeration>();

    [JsonPropertyName("enumerationValues")]
    public IReadOnlyList<MicroflowResolvedEnumerationValue> EnumerationValues { get; init; } = Array.Empty<MicroflowResolvedEnumerationValue>();

    [JsonPropertyName("microflows")]
    public IReadOnlyList<MicroflowResolvedMicroflowRef> Microflows { get; init; } = Array.Empty<MicroflowResolvedMicroflowRef>();

    [JsonPropertyName("dataTypes")]
    public IReadOnlyList<MicroflowResolvedDataType> DataTypes { get; init; } = Array.Empty<MicroflowResolvedDataType>();

    [JsonPropertyName("memberPaths")]
    public IReadOnlyList<MicroflowResolvedMemberPath> MemberPaths { get; init; } = Array.Empty<MicroflowResolvedMemberPath>();

    [JsonPropertyName("entityAccessDecisions")]
    public IReadOnlyList<MicroflowEntityAccessDecision> EntityAccessDecisions { get; init; } = Array.Empty<MicroflowEntityAccessDecision>();
}
