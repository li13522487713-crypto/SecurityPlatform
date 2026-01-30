namespace Atlas.Application.Identity.Models;

public sealed record PositionListItem(
    string Id,
    string Name,
    string Code,
    string? Description,
    bool IsActive,
    bool IsSystem,
    int SortOrder);

public sealed record PositionDetail(
    string Id,
    string Name,
    string Code,
    string? Description,
    bool IsActive,
    bool IsSystem,
    int SortOrder);

public sealed record PositionCreateRequest(
    string Name,
    string Code,
    string? Description,
    bool IsActive,
    int SortOrder);

public sealed record PositionUpdateRequest(
    string Name,
    string? Description,
    bool IsActive,
    int SortOrder);
