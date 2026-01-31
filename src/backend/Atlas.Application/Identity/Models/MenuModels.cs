namespace Atlas.Application.Identity.Models;

public sealed record MenuListItem(
    string Id,
    string Name,
    string Path,
    long? ParentId,
    int SortOrder,
    string? Component,
    string? Icon,
    string? PermissionCode,
    bool IsHidden);

public sealed record MenuQueryRequest(
    int PageIndex,
    int PageSize,
    string? Keyword,
    string? SortBy,
    bool SortDesc,
    bool? IsHidden);

public sealed record MenuCreateRequest(
    string Name,
    string Path,
    long? ParentId,
    int SortOrder,
    string? Component,
    string? Icon,
    string? PermissionCode,
    bool IsHidden);

public sealed record MenuUpdateRequest(
    string Name,
    string Path,
    long? ParentId,
    int SortOrder,
    string? Component,
    string? Icon,
    string? PermissionCode,
    bool IsHidden);
