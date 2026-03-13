namespace Atlas.Application.AiPlatform.Models;

public sealed record AiSearchResultItem(
    string ResourceType,
    long ResourceId,
    string Title,
    string? Description,
    string Path,
    DateTime? UpdatedAt);

public sealed record AiRecentEditItem(
    long Id,
    string ResourceType,
    long ResourceId,
    string Title,
    string Path,
    DateTime UpdatedAt);

public sealed record AiSearchResponse(
    IReadOnlyList<AiSearchResultItem> Items,
    IReadOnlyList<AiRecentEditItem> RecentEdits);

public sealed record AiRecentEditCreateRequest(
    string ResourceType,
    long ResourceId,
    string Title,
    string Path);
