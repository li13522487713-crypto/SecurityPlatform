using Atlas.Application.LowCode.Abstractions;

namespace Atlas.Application.LowCode.Models;

public sealed record ProjectIdeBootstrapDto(
    string AppId,
    AppDefinitionDetail App,
    AppDraftResponse Draft,
    string ProjectedSchemaJson,
    bool ProjectionDrift,
    ProjectIdeGraphDto Graph,
    ComponentRegistryDto ComponentRegistry,
    AppResourceCatalogDto ResourceCatalog,
    IReadOnlyList<AppTemplateDto> Templates,
    IReadOnlyList<AppVersionArchiveListItem> Versions,
    IReadOnlyList<PublishArtifactDto> Artifacts,
    AppDraftLockInfo? DraftLock,
    ProjectIdePublishPreviewDto PublishPreview);

public sealed record ProjectIdeGraphDto(
    string AppId,
    string Code,
    string ProjectedSchemaJson,
    bool ProjectionDrift,
    string ResourceSnapshotJson,
    IReadOnlyList<ProjectIdeReferenceGroupDto> Groups,
    int TotalReferences,
    int MissingReferences);

public sealed record ProjectIdeReferenceGroupDto(
    string ResourceType,
    IReadOnlyList<ProjectIdeReferenceDto> References);

public sealed record ProjectIdeReferenceDto(
    string ResourceType,
    string ResourceId,
    string? DisplayName,
    string? ResolvedVersion,
    string? Status,
    bool Exists,
    string ReferencePath,
    string? PageId,
    string? ComponentId);

public sealed record ProjectIdeValidationRequest(string? SchemaJson);

public sealed record ProjectIdeValidationResultDto(
    bool IsValid,
    IReadOnlyList<ProjectIdeValidationIssueDto> Issues,
    ProjectIdeGraphDto Graph);

public sealed record ProjectIdeValidationIssueDto(
    string Severity,
    string Code,
    string Message,
    string? ReferencePath,
    string? PageId,
    string? ComponentId,
    string? ResourceType,
    string? ResourceId,
    string? WorkflowId = null,
    string? NodeId = null,
    string? Expression = null,
    string? ReplacementSuggestion = null);

public sealed record ProjectIdePublishRequest(
    string Kind,
    string? VersionId,
    string? VersionLabel,
    string? Note,
    string? RendererMatrixJson);

public sealed record ProjectIdePublishResultDto(
    string AppId,
    string VersionId,
    string ResourceSnapshotJson,
    PublishArtifactDto Artifact,
    ProjectIdeGraphDto Graph);

public sealed record ProjectIdePublishPreviewDto(
    string AppId,
    string SuggestedVersionLabel,
    string ResourceSnapshotJson,
    int MissingReferences,
    IReadOnlyList<string> Warnings);

public sealed record LowCodeAssetDescriptorDto(
    string FileHandle,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Url,
    DateTimeOffset? UploadedAt,
    string? ETag);
