namespace Atlas.Application.LowCode.Models;

public sealed record LowCodePageListItem(
    string Id,
    string AppId,
    string PageKey,
    string Name,
    string PageType,
    string? RoutePath,
    string? Description,
    string? Icon,
    int SortOrder,
    string? ParentPageId,
    int Version,
    bool IsPublished,
    DateTimeOffset CreatedAt,
    string? PermissionCode,
    string? DataTableKey);

public sealed record LowCodePageDetail(
    string Id,
    string AppId,
    string PageKey,
    string Name,
    string PageType,
    string SchemaJson,
    string? RoutePath,
    string? Description,
    string? Icon,
    int SortOrder,
    string? ParentPageId,
    int Version,
    bool IsPublished,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long CreatedBy,
    long UpdatedBy,
    string? PermissionCode,
    string? DataTableKey);

public sealed record LowCodePageCreateRequest(
    string PageKey,
    string Name,
    string PageType,
    string SchemaJson,
    string? RoutePath,
    string? Description,
    string? Icon,
    int SortOrder,
    long? ParentPageId,
    string? PermissionCode,
    string? DataTableKey);

public sealed record LowCodePageUpdateRequest(
    string Name,
    string PageType,
    string SchemaJson,
    string? RoutePath,
    string? Description,
    string? Icon,
    int SortOrder,
    long? ParentPageId,
    string? PermissionCode,
    string? DataTableKey);

public sealed record LowCodePageSchemaUpdateRequest(
    string SchemaJson);
