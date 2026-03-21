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
    bool IsHidden,
    string MenuType = "C",
    string? Perms = null,
    string? Query = null,
    bool IsFrame = false,
    bool IsCache = false,
    string Visible = "0",
    string Status = "0");

public sealed record MenuSortItem(long MenuId, int SortOrder);

public sealed record MenuBatchSortRequest(IReadOnlyList<MenuSortItem> Items);

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
    string MenuType,
    string? Component,
    string? Icon,
    string? Perms,
    string? Query,
    bool IsFrame,
    bool IsCache,
    string Visible,
    string Status,
    string? PermissionCode,
    bool IsHidden);

public sealed record MenuUpdateRequest(
    string Name,
    string Path,
    long? ParentId,
    int SortOrder,
    string MenuType,
    string? Component,
    string? Icon,
    string? Perms,
    string? Query,
    bool IsFrame,
    bool IsCache,
    string Visible,
    string Status,
    string? PermissionCode,
    bool IsHidden);
