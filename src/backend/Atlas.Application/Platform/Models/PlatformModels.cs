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

public sealed record RuntimePageResponse(
    string AppKey,
    string PageKey,
    int SchemaVersion,
    bool IsActive);

public sealed record RuntimePageDescriptor(
    long AppId,
    long PageId,
    string AppKey,
    string PageKey,
    string PageName,
    string? DataTableKey,
    bool IsPublished);

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

