using System.Text.Json.Serialization;
using Atlas.Application.Microflows.Runtime.Metadata;
using Atlas.Application.Microflows.Runtime.Security;

namespace Atlas.Application.Microflows.Runtime.Objects;

public static class MicroflowObjectOperationKind
{
    public const string Retrieve = "retrieve";
    public const string Create = "create";
    public const string ChangeMembers = "changeMembers";
    public const string Commit = "commit";
    public const string Delete = "delete";
}

public sealed record MicroflowObjectMemberRef
{
    [JsonPropertyName("qualifiedName")]
    public string QualifiedName { get; init; } = string.Empty;

    [JsonPropertyName("isAssociation")]
    public bool IsAssociation { get; init; }
}

public sealed record MicroflowObjectOperationRequest
{
    public MicroflowMetadataResolutionContext MetadataContext { get; init; } = null!;
    public string EntityQualifiedName { get; init; } = string.Empty;
    public IReadOnlyList<MicroflowObjectMemberRef> Members { get; init; } = Array.Empty<MicroflowObjectMemberRef>();
    public string? SourceObjectId { get; init; }
    public string? FieldPath { get; init; }
}

public sealed record MicroflowObjectOperationPlan
{
    [JsonPropertyName("operation")]
    public string Operation { get; init; } = string.Empty;

    [JsonPropertyName("entity")]
    public MicroflowResolvedEntity Entity { get; init; } = new();

    [JsonPropertyName("attributes")]
    public IReadOnlyList<MicroflowResolvedAttribute> Attributes { get; init; } = Array.Empty<MicroflowResolvedAttribute>();

    [JsonPropertyName("associations")]
    public IReadOnlyList<MicroflowResolvedAssociation> Associations { get; init; } = Array.Empty<MicroflowResolvedAssociation>();

    [JsonPropertyName("accessDecision")]
    public MicroflowEntityAccessDecision AccessDecision { get; init; } = new();

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowMetadataResolutionDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowMetadataResolutionDiagnostic>();
}
