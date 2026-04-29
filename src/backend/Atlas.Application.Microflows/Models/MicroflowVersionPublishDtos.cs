using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atlas.Application.Microflows.Models;

public sealed record PublishMicroflowApiRequestDto
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("confirmBreakingChanges")]
    public bool ConfirmBreakingChanges { get; init; }

    [JsonPropertyName("force")]
    public bool Force { get; init; }
}

public sealed record UnpublishMicroflowRequestDto
{
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    /// <summary>
    /// 是否在仍存在 active reference 时仍然取消发布。默认 false，存在 active 引用时返回
    /// MICROFLOW_REFERENCE_BLOCKED 让前端先解除引用或显式覆盖。
    /// </summary>
    [JsonPropertyName("force")]
    public bool Force { get; init; }
}

public sealed record MicroflowPublishResultDto
{
    [JsonPropertyName("resource")]
    public MicroflowResourceDto Resource { get; init; } = new();

    [JsonPropertyName("version")]
    public MicroflowVersionSummaryDto Version { get; init; } = new();

    [JsonPropertyName("snapshot")]
    public MicroflowPublishedSnapshotDto Snapshot { get; init; } = new();

    [JsonPropertyName("validationSummary")]
    public MicroflowValidationSummaryDto ValidationSummary { get; init; } = new();

    [JsonPropertyName("impactAnalysis")]
    public MicroflowPublishImpactAnalysisDto ImpactAnalysis { get; init; } = new();
}

public record MicroflowVersionSummaryDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("resourceId")]
    public string ResourceId { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = "published";

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("createdBy")]
    public string? CreatedBy { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("schemaSnapshotId")]
    public string SchemaSnapshotId { get; init; } = string.Empty;

    [JsonPropertyName("validationSummary")]
    public MicroflowValidationSummaryDto? ValidationSummary { get; init; }

    [JsonPropertyName("referenceCount")]
    public int ReferenceCount { get; init; }

    [JsonPropertyName("isLatestPublished")]
    public bool IsLatestPublished { get; init; }
}

public sealed record MicroflowPublishedSnapshotDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("resourceId")]
    public string ResourceId { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("schema")]
    public JsonElement Schema { get; init; }

    [JsonPropertyName("publishedAt")]
    public DateTimeOffset PublishedAt { get; init; }

    [JsonPropertyName("publishedBy")]
    public string? PublishedBy { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("validationSummary")]
    public MicroflowValidationSummaryDto ValidationSummary { get; init; } = new();

    [JsonPropertyName("schemaHash")]
    public string? SchemaHash { get; init; }
}

public sealed record MicroflowVersionDetailDto : MicroflowVersionSummaryDto
{
    [JsonPropertyName("snapshot")]
    public MicroflowPublishedSnapshotDto Snapshot { get; init; } = new();

    [JsonPropertyName("diffFromCurrent")]
    public MicroflowVersionDiffDto? DiffFromCurrent { get; init; }
}

public sealed record MicroflowVersionDiffDto
{
    [JsonPropertyName("addedParameters")]
    public IReadOnlyList<string> AddedParameters { get; init; } = Array.Empty<string>();

    [JsonPropertyName("removedParameters")]
    public IReadOnlyList<string> RemovedParameters { get; init; } = Array.Empty<string>();

    [JsonPropertyName("changedParameters")]
    public IReadOnlyList<MicroflowChangedParameterDto> ChangedParameters { get; init; } = Array.Empty<MicroflowChangedParameterDto>();

    [JsonPropertyName("returnTypeChanged")]
    public MicroflowReturnTypeChangedDto? ReturnTypeChanged { get; init; }

    [JsonPropertyName("addedObjects")]
    public IReadOnlyList<string> AddedObjects { get; init; } = Array.Empty<string>();

    [JsonPropertyName("removedObjects")]
    public IReadOnlyList<string> RemovedObjects { get; init; } = Array.Empty<string>();

    [JsonPropertyName("changedObjects")]
    public IReadOnlyList<string> ChangedObjects { get; init; } = Array.Empty<string>();

    [JsonPropertyName("addedFlows")]
    public IReadOnlyList<string> AddedFlows { get; init; } = Array.Empty<string>();

    [JsonPropertyName("removedFlows")]
    public IReadOnlyList<string> RemovedFlows { get; init; } = Array.Empty<string>();

    [JsonPropertyName("breakingChanges")]
    public IReadOnlyList<MicroflowBreakingChangeDto> BreakingChanges { get; init; } = Array.Empty<MicroflowBreakingChangeDto>();
}

public sealed record MicroflowChangedParameterDto(string Name, string BeforeType, string AfterType);

public sealed record MicroflowReturnTypeChangedDto(string BeforeType, string AfterType);

public sealed record MicroflowBreakingChangeDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; init; } = "low";

    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("fieldPath")]
    public string? FieldPath { get; init; }

    [JsonPropertyName("before")]
    public string? Before { get; init; }

    [JsonPropertyName("after")]
    public string? After { get; init; }
}

public sealed record MicroflowPublishImpactAnalysisDto
{
    [JsonPropertyName("resourceId")]
    public string ResourceId { get; init; } = string.Empty;

    [JsonPropertyName("currentVersion")]
    public string? CurrentVersion { get; init; }

    [JsonPropertyName("nextVersion")]
    public string NextVersion { get; init; } = string.Empty;

    [JsonPropertyName("references")]
    public IReadOnlyList<MicroflowReferenceDto> References { get; init; } = Array.Empty<MicroflowReferenceDto>();

    [JsonPropertyName("breakingChanges")]
    public IReadOnlyList<MicroflowBreakingChangeDto> BreakingChanges { get; init; } = Array.Empty<MicroflowBreakingChangeDto>();

    [JsonPropertyName("impactLevel")]
    public string ImpactLevel { get; init; } = "none";

    [JsonPropertyName("summary")]
    public MicroflowPublishImpactSummaryDto Summary { get; init; } = new();
}

public sealed record MicroflowReferenceDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("targetMicroflowId")]
    public string TargetMicroflowId { get; init; } = string.Empty;

    [JsonPropertyName("sourceType")]
    public string SourceType { get; init; } = "unknown";

    [JsonPropertyName("sourceId")]
    public string? SourceId { get; init; }

    [JsonPropertyName("sourceName")]
    public string SourceName { get; init; } = string.Empty;

    [JsonPropertyName("sourcePath")]
    public string? SourcePath { get; init; }

    [JsonPropertyName("sourceVersion")]
    public string? SourceVersion { get; init; }

    [JsonPropertyName("referencedVersion")]
    public string? ReferencedVersion { get; init; }

    [JsonPropertyName("referenceKind")]
    public string ReferenceKind { get; init; } = "unknown";

    [JsonPropertyName("impactLevel")]
    public string ImpactLevel { get; init; } = "none";

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("active")]
    public bool Active { get; init; } = true;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }

    [JsonPropertyName("canNavigate")]
    public bool CanNavigate { get; init; }
}

public sealed record MicroflowPublishImpactSummaryDto
{
    [JsonPropertyName("referenceCount")]
    public int ReferenceCount { get; init; }

    [JsonPropertyName("breakingChangeCount")]
    public int BreakingChangeCount { get; init; }

    [JsonPropertyName("highImpactCount")]
    public int HighImpactCount { get; init; }

    [JsonPropertyName("mediumImpactCount")]
    public int MediumImpactCount { get; init; }

    [JsonPropertyName("lowImpactCount")]
    public int LowImpactCount { get; init; }
}

public sealed record RollbackMicroflowVersionRequestDto
{
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}

public sealed record DuplicateMicroflowVersionRequestDto
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("moduleId")]
    public string? ModuleId { get; init; }

    [JsonPropertyName("moduleName")]
    public string? ModuleName { get; init; }

    [JsonPropertyName("tags")]
    public IReadOnlyList<string>? Tags { get; init; }
}

public sealed record AnalyzeMicroflowImpactRequestDto
{
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("includeBreakingChanges")]
    public bool IncludeBreakingChanges { get; init; } = true;

    [JsonPropertyName("includeReferences")]
    public bool IncludeReferences { get; init; } = true;
}

public sealed record GetMicroflowReferencesRequestDto
{
    [JsonPropertyName("includeInactive")]
    public bool IncludeInactive { get; init; }

    [JsonPropertyName("sourceType")]
    public IReadOnlyList<string> SourceType { get; init; } = Array.Empty<string>();

    [JsonPropertyName("impactLevel")]
    public IReadOnlyList<string> ImpactLevel { get; init; } = Array.Empty<string>();
}
