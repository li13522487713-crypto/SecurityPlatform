namespace Atlas.Application.Platform.Models;

public sealed record AppManifestCreateRequest(
    string AppKey,
    string Name,
    string? Description,
    string? Category,
    string? Icon,
    long? DataSourceId);

public sealed record AppManifestUpdateRequest(
    string Name,
    string? Description,
    string? Category,
    string? Icon,
    long? DataSourceId);

public sealed record AppManifestResponse(
    string Id,
    string AppKey,
    string Name,
    string Status,
    int Version,
    string? Description,
    string? Category,
    string? Icon,
    string? PublishedAt);

public sealed record AppReleaseResponse(
    string Id,
    string ManifestId,
    int Version,
    string Status,
    string ReleasedAt,
    string? ReleaseNote);

public sealed record PlatformOverviewResponse(
    int AppCount,
    int ReleaseCount,
    int ActiveRouteCount,
    int PolicyCount,
    int LicenseCount);

public sealed record PlatformResourceItem(
    string Name,
    string Value,
    string Unit,
    string Status);

public sealed record PlatformResourcesResponse(
    IReadOnlyList<PlatformResourceItem> Items);

public sealed record RuntimePageResponse(
    string AppKey,
    string PageKey,
    int SchemaVersion,
    bool IsActive);

public sealed record RuntimeTaskListItem(
    string Id,
    string Type,
    string Title,
    string Status,
    string CreatedAt);

public sealed record RuntimeTaskActionRequest(
    string Action,
    string? Comment);

public sealed record RuntimePageActionRequest(
    string TaskId,
    string Action,
    string? Comment);

public sealed record RuntimeMenuItem(
    string PageKey,
    string Title,
    string RoutePath,
    string? Icon,
    int SortOrder);

public sealed record RuntimeMenuResponse(
    string AppKey,
    IReadOnlyList<RuntimeMenuItem> Items);

public sealed record WorkspaceOverviewResponse(
    int PageCount,
    int FormCount,
    int FlowCount,
    int DataTableCount);

public sealed record WorkspacePermissionItem(
    string Code,
    string Name);

public sealed record WorkspacePermissionResponse(
    IReadOnlyList<WorkspacePermissionItem> Items);

public sealed record DesignerSnapshotResponse(
    string ManifestId,
    string Type,
    string ItemId,
    string SchemaJson,
    int Version,
    string CreatedBy,
    DateTimeOffset CreatedAt);

public sealed record DesignerSnapshotHistoryItem(
    string Id,
    int Version,
    string CreatedBy,
    DateTimeOffset CreatedAt);

public sealed record DesignerSnapshotSaveRequest(string SchemaJson);
