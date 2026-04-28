using System.Text.Json;
using System.Text.Json.Serialization;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Security;

namespace Atlas.Application.Microflows.Runtime.Metadata;

public static class MicroflowMetadataResolutionSeverity
{
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Error = "error";
}

public static class MicroflowResolvedDataTypeKind
{
    public const string Unknown = "unknown";
    public const string Void = "void";
    public const string Boolean = "boolean";
    public const string String = "string";
    public const string Integer = "integer";
    public const string Long = "long";
    public const string Decimal = "decimal";
    public const string DateTime = "dateTime";
    public const string Object = "object";
    public const string List = "list";
    public const string Enumeration = "enumeration";
    public const string Json = "json";
    public const string Binary = "binary";
}

public sealed record MicroflowMetadataResolutionDiagnostic
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = RuntimeErrorCode.RuntimeMetadataNotFound;

    [JsonPropertyName("severity")]
    public string Severity { get; init; } = MicroflowMetadataResolutionSeverity.Error;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("kind")]
    public string? Kind { get; init; }

    [JsonPropertyName("qualifiedName")]
    public string? QualifiedName { get; init; }

    [JsonPropertyName("sourceObjectId")]
    public string? SourceObjectId { get; init; }

    [JsonPropertyName("sourceActionId")]
    public string? SourceActionId { get; init; }

    [JsonPropertyName("fieldPath")]
    public string? FieldPath { get; init; }
}

public sealed record MicroflowMetadataResolutionContext
{
    public MicroflowMetadataCatalogDto Catalog { get; init; } = new();
    public string CatalogVersion { get; init; } = string.Empty;
    public DateTimeOffset? UpdatedAt { get; init; }
    public IReadOnlyDictionary<string, MetadataEntityDto> EntitiesByQualifiedName { get; init; } = new Dictionary<string, MetadataEntityDto>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, MetadataAttributeDto> AttributesByQualifiedName { get; init; } = new Dictionary<string, MetadataAttributeDto>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, string> AttributeOwnersByQualifiedName { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, MetadataAssociationDto> AssociationsByQualifiedName { get; init; } = new Dictionary<string, MetadataAssociationDto>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, MetadataEnumerationDto> EnumerationsByQualifiedName { get; init; } = new Dictionary<string, MetadataEnumerationDto>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, MetadataMicroflowRefDto> MicroflowsById { get; init; } = new Dictionary<string, MetadataMicroflowRefDto>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, MetadataMicroflowRefDto> MicroflowsByQualifiedName { get; init; } = new Dictionary<string, MetadataMicroflowRefDto>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, MetadataPageRefDto> PagesById { get; init; } = new Dictionary<string, MetadataPageRefDto>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, MetadataWorkflowRefDto> WorkflowsById { get; init; } = new Dictionary<string, MetadataWorkflowRefDto>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, MetadataConnectorDto> ConnectorsById { get; init; } = new Dictionary<string, MetadataConnectorDto>(StringComparer.OrdinalIgnoreCase);
    public MicroflowExecutionPlan ExecutionPlan { get; init; } = new();
    public IReadOnlyList<MicroflowExecutionMetadataRef> MetadataRefs { get; init; } = Array.Empty<MicroflowExecutionMetadataRef>();
    public MicroflowRuntimeSecurityContext SecurityContext { get; init; } = MicroflowRuntimeSecurityContext.System();
    public IReadOnlyList<MicroflowMetadataResolutionDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowMetadataResolutionDiagnostic>();
}

public sealed record MicroflowResolvedEntity
{
    [JsonPropertyName("found")]
    public bool Found { get; init; }

    [JsonPropertyName("qualifiedName")]
    public string QualifiedName { get; init; } = string.Empty;

    [JsonPropertyName("entity")]
    public MetadataEntityDto? Entity { get; init; }

    [JsonPropertyName("isSystemEntity")]
    public bool IsSystemEntity { get; init; }

    [JsonPropertyName("isPersistable")]
    public bool IsPersistable { get; init; }

    [JsonPropertyName("generalization")]
    public string? Generalization { get; init; }

    [JsonPropertyName("specializations")]
    public IReadOnlyList<string> Specializations { get; init; } = Array.Empty<string>();

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowMetadataResolutionDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowMetadataResolutionDiagnostic>();
}

public sealed record MicroflowResolvedAttribute
{
    [JsonPropertyName("found")]
    public bool Found { get; init; }

    [JsonPropertyName("qualifiedName")]
    public string QualifiedName { get; init; } = string.Empty;

    [JsonPropertyName("attribute")]
    public MetadataAttributeDto? Attribute { get; init; }

    [JsonPropertyName("ownerEntityQualifiedName")]
    public string? OwnerEntityQualifiedName { get; init; }

    [JsonPropertyName("dataType")]
    public MicroflowResolvedDataType DataType { get; init; } = MicroflowResolvedDataType.Unknown();

    [JsonPropertyName("isReadonly")]
    public bool IsReadonly { get; init; }

    [JsonPropertyName("isRequired")]
    public bool IsRequired { get; init; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowMetadataResolutionDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowMetadataResolutionDiagnostic>();
}

public sealed record MicroflowResolvedAssociation
{
    [JsonPropertyName("found")]
    public bool Found { get; init; }

    [JsonPropertyName("qualifiedName")]
    public string QualifiedName { get; init; } = string.Empty;

    [JsonPropertyName("association")]
    public MetadataAssociationDto? Association { get; init; }

    [JsonPropertyName("sourceEntityQualifiedName")]
    public string? SourceEntityQualifiedName { get; init; }

    [JsonPropertyName("targetEntityQualifiedName")]
    public string? TargetEntityQualifiedName { get; init; }

    [JsonPropertyName("multiplicity")]
    public string Multiplicity { get; init; } = string.Empty;

    [JsonPropertyName("direction")]
    public string Direction { get; init; } = string.Empty;

    [JsonPropertyName("returnsList")]
    public bool ReturnsList { get; init; }

    [JsonPropertyName("returnsSingleObject")]
    public bool ReturnsSingleObject => Found && !ReturnsList;

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowMetadataResolutionDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowMetadataResolutionDiagnostic>();
}

public sealed record MicroflowResolvedEnumeration
{
    [JsonPropertyName("found")]
    public bool Found { get; init; }

    [JsonPropertyName("qualifiedName")]
    public string QualifiedName { get; init; } = string.Empty;

    [JsonPropertyName("enumeration")]
    public MetadataEnumerationDto? Enumeration { get; init; }

    [JsonPropertyName("values")]
    public IReadOnlyList<MetadataEnumerationValueDto> Values { get; init; } = Array.Empty<MetadataEnumerationValueDto>();

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowMetadataResolutionDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowMetadataResolutionDiagnostic>();
}

public sealed record MicroflowResolvedEnumerationValue
{
    [JsonPropertyName("found")]
    public bool Found { get; init; }

    [JsonPropertyName("enumerationQualifiedName")]
    public string EnumerationQualifiedName { get; init; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; init; } = string.Empty;

    [JsonPropertyName("caption")]
    public string? Caption { get; init; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowMetadataResolutionDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowMetadataResolutionDiagnostic>();
}

public sealed record MicroflowResolvedMicroflowRef
{
    [JsonPropertyName("found")]
    public bool Found { get; init; }

    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("qualifiedName")]
    public string? QualifiedName { get; init; }

    [JsonPropertyName("microflow")]
    public MetadataMicroflowRefDto? Microflow { get; init; }

    [JsonPropertyName("parameters")]
    public IReadOnlyList<MetadataMicroflowParameterDto> Parameters { get; init; } = Array.Empty<MetadataMicroflowParameterDto>();

    [JsonPropertyName("returnType")]
    public MicroflowResolvedDataType ReturnType { get; init; } = MicroflowResolvedDataType.Unknown();

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowMetadataResolutionDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowMetadataResolutionDiagnostic>();
}

public sealed record MicroflowResolvedDataType
{
    [JsonPropertyName("found")]
    public bool Found { get; init; }

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = MicroflowResolvedDataTypeKind.Unknown;

    [JsonPropertyName("entityQualifiedName")]
    public string? EntityQualifiedName { get; init; }

    [JsonPropertyName("enumerationQualifiedName")]
    public string? EnumerationQualifiedName { get; init; }

    [JsonPropertyName("itemType")]
    public MicroflowResolvedDataType? ItemType { get; init; }

    [JsonPropertyName("rawDataTypeJson")]
    public string? RawDataTypeJson { get; init; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowMetadataResolutionDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowMetadataResolutionDiagnostic>();

    public static MicroflowResolvedDataType Unknown(string? raw = null, IReadOnlyList<MicroflowMetadataResolutionDiagnostic>? diagnostics = null)
        => new() { Kind = MicroflowResolvedDataTypeKind.Unknown, RawDataTypeJson = raw, Diagnostics = diagnostics ?? Array.Empty<MicroflowMetadataResolutionDiagnostic>() };
}

public sealed record MicroflowResolvedMemberPath
{
    [JsonPropertyName("found")]
    public bool Found { get; init; }

    [JsonPropertyName("rootType")]
    public MicroflowResolvedDataType RootType { get; init; } = MicroflowResolvedDataType.Unknown();

    [JsonPropertyName("memberPath")]
    public IReadOnlyList<string> MemberPath { get; init; } = Array.Empty<string>();

    [JsonPropertyName("finalType")]
    public MicroflowResolvedDataType FinalType { get; init; } = MicroflowResolvedDataType.Unknown();

    [JsonPropertyName("finalEntityQualifiedName")]
    public string? FinalEntityQualifiedName { get; init; }

    [JsonPropertyName("finalAttributeQualifiedName")]
    public string? FinalAttributeQualifiedName { get; init; }

    [JsonPropertyName("finalAssociationQualifiedName")]
    public string? FinalAssociationQualifiedName { get; init; }

    [JsonPropertyName("traversedMembers")]
    public IReadOnlyList<MicroflowResolvedMemberPathSegment> TraversedMembers { get; init; } = Array.Empty<MicroflowResolvedMemberPathSegment>();

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowMetadataResolutionDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowMetadataResolutionDiagnostic>();
}

public sealed record MicroflowResolvedMemberPathSegment
{
    [JsonPropertyName("member")]
    public string Member { get; init; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = "unknown";

    [JsonPropertyName("qualifiedName")]
    public string? QualifiedName { get; init; }

    [JsonPropertyName("sourceEntityQualifiedName")]
    public string? SourceEntityQualifiedName { get; init; }

    [JsonPropertyName("targetEntityQualifiedName")]
    public string? TargetEntityQualifiedName { get; init; }

    [JsonPropertyName("returnsList")]
    public bool ReturnsList { get; init; }
}

public sealed record MicroflowMetadataResolutionReport
{
    [JsonPropertyName("allResolved")]
    public bool AllResolved { get; init; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<MicroflowMetadataResolutionDiagnostic> Diagnostics { get; init; } = Array.Empty<MicroflowMetadataResolutionDiagnostic>();

    [JsonPropertyName("missingEntities")]
    public IReadOnlyList<string> MissingEntities { get; init; } = Array.Empty<string>();

    [JsonPropertyName("missingAttributes")]
    public IReadOnlyList<string> MissingAttributes { get; init; } = Array.Empty<string>();

    [JsonPropertyName("missingAssociations")]
    public IReadOnlyList<string> MissingAssociations { get; init; } = Array.Empty<string>();

    [JsonPropertyName("missingEnumerations")]
    public IReadOnlyList<string> MissingEnumerations { get; init; } = Array.Empty<string>();

    [JsonPropertyName("missingMicroflows")]
    public IReadOnlyList<string> MissingMicroflows { get; init; } = Array.Empty<string>();

    [JsonPropertyName("unsupportedRefs")]
    public IReadOnlyList<string> UnsupportedRefs { get; init; } = Array.Empty<string>();
}
