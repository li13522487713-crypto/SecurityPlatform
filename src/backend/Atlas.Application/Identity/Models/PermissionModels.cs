namespace Atlas.Application.Identity.Models;

public sealed record PermissionListItem(
    string Id,
    string Name,
    string Code,
    string Type,
    string? Description);

public sealed record PermissionQueryRequest(
    int PageIndex,
    int PageSize,
    string? Keyword,
    string? SortBy,
    bool SortDesc,
    string? Type);

public sealed record PermissionDetail(
    string Id,
    string Name,
    string Code,
    string Type,
    string? Description);

public sealed record PermissionCreateRequest(
    string Name,
    string Code,
    string Type,
    string? Description);

public sealed record PermissionUpdateRequest(
    string Name,
    string Type,
    string? Description);
