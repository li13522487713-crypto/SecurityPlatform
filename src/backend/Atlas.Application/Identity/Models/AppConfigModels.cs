namespace Atlas.Application.Identity.Models;

public sealed record AppConfigListItem(
    string Id,
    string AppId,
    string Name,
    bool IsActive,
    bool EnableProjectScope,
    string? Description,
    int SortOrder);

public sealed record AppConfigDetail(
    string Id,
    string AppId,
    string Name,
    bool IsActive,
    bool EnableProjectScope,
    string? Description,
    int SortOrder);

public sealed record AppConfigUpdateRequest(
    string Name,
    bool IsActive,
    bool EnableProjectScope,
    string? Description,
    int SortOrder);
