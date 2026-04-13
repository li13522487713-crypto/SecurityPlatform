using Atlas.Core.Models;

namespace Atlas.Application.AiPlatform.Models;

public sealed record ResourceIndexItem(
    long ResourceId,
    string ResourceType,
    string Name,
    string? Description,
    string? Icon = null,
    string? Status = null,
    string? Path = null);

public sealed record ConnectorCatalogItem(
    string Key,
    string Name,
    string Category,
    bool SupportsPublish,
    bool SupportsRuntime,
    string? Icon = null,
    string? Description = null);

public sealed record PublishEnvelope(
    string ResourceType,
    long ResourceId,
    string Version,
    string? ReleaseNote = null,
    IReadOnlyDictionary<string, string>? ConnectorConfig = null);

public sealed record PublishOrchestrationResult(
    string ResourceType,
    long ResourceId,
    string Version,
    bool Success,
    string? SnapshotId = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public sealed record ResourceReferenceItem(
    string ResourceType,
    long ResourceId,
    string Name,
    string? NodeKey = null,
    string? PortKey = null,
    string? Description = null);

public sealed record ResourceReferenceGraph(
    long OwnerId,
    string OwnerType,
    IReadOnlyList<ResourceReferenceItem> References);
