namespace Atlas.Application.Platform.Models;

public sealed record ApplicationCatalogListItem(
    string Id,
    string CatalogKey,
    string Name,
    string Status,
    int Version,
    string? Description,
    string? Category,
    string? Icon,
    string? PublishedAt);

public sealed record ApplicationCatalogDetail(
    string Id,
    string CatalogKey,
    string Name,
    string Status,
    int Version,
    string? Description,
    string? Category,
    string? Icon,
    string? PublishedAt,
    string? DataSourceId);

public sealed record TenantApplicationListItem(
    string Id,
    string ApplicationCatalogId,
    string ApplicationCatalogName,
    string TenantAppInstanceId,
    string AppKey,
    string Name,
    string Status,
    string OpenedAt,
    string? DataSourceId);

public sealed record TenantApplicationDetail(
    string Id,
    string ApplicationCatalogId,
    string ApplicationCatalogName,
    string TenantAppInstanceId,
    string AppKey,
    string Name,
    string Status,
    string OpenedAt,
    string UpdatedAt,
    string? DataSourceId);

public sealed record TenantAppInstanceListItem(
    string Id,
    string AppKey,
    string Name,
    string Status,
    int Version,
    string? Description,
    string? Category,
    string? Icon,
    string? PublishedAt);

public sealed record TenantAppInstanceDetail(
    string Id,
    string AppKey,
    string Name,
    string Status,
    int Version,
    string? Description,
    string? Category,
    string? Icon,
    string? PublishedAt,
    string? DataSourceId);

public sealed record TenantAppDataSourceBinding(
    string TenantAppInstanceId,
    string? DataSourceId,
    string? DataSourceName,
    string? DbType,
    bool? DataSourceActive,
    string? LastTestedAt);

public sealed record RuntimeContextListItem(
    string Id,
    string AppKey,
    string PageKey,
    int SchemaVersion,
    string EnvironmentCode,
    bool IsActive);

public sealed record RuntimeContextDetail(
    string Id,
    string AppKey,
    string PageKey,
    int SchemaVersion,
    string EnvironmentCode,
    bool IsActive);

public sealed record RuntimeExecutionListItem(
    string Id,
    string RuntimeContextId,
    string Status,
    string StartedAt,
    string? CompletedAt,
    string? ErrorMessage);

public sealed record RuntimeExecutionDetail(
    string Id,
    string RuntimeContextId,
    string Status,
    string StartedAt,
    string? CompletedAt,
    string? InputsJson,
    string? OutputsJson,
    string? ErrorMessage);

public sealed record ResourceCenterGroupEntry(
    string ResourceId,
    string ResourceName,
    string ResourceType,
    string? Status,
    string? Description);

public sealed record ResourceCenterGroupItem(
    string GroupKey,
    string GroupName,
    int Total,
    IReadOnlyList<ResourceCenterGroupEntry> Items);

public sealed record ReleaseCenterListItem(
    string ReleaseId,
    string ApplicationCatalogId,
    string ApplicationCatalogName,
    string AppKey,
    int Version,
    string Status,
    string ReleasedAt,
    string? ReleaseNote);

public sealed record ReleaseCenterDetail(
    string ReleaseId,
    string ApplicationCatalogId,
    string ApplicationCatalogName,
    string AppKey,
    int Version,
    string Status,
    string ReleasedAt,
    string? ReleaseNote,
    string SnapshotJson);

public sealed record RuntimeExecutionAuditTrailItem(
    string AuditId,
    string Actor,
    string Action,
    string Result,
    string Target,
    string OccurredAt);

public sealed record CozeLayerMappingItem(
    string LayerKey,
    string LayerName,
    int Total,
    string Description);

public sealed record CozeLayerMappingOverview(
    IReadOnlyList<CozeLayerMappingItem> Layers);

public sealed record DebugLayerResourceItem(
    string ResourceKey,
    string ResourceName,
    string RequiredPermission,
    string Description);

public sealed record DebugLayerEmbedMetadata(
    string TenantId,
    string AppId,
    string? ProjectId,
    bool ProjectScopeEnabled,
    IReadOnlyList<DebugLayerResourceItem> Resources);
