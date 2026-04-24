using Atlas.Core.Models;

namespace Atlas.Application.AiPlatform.Models;

public sealed record AiWorkspaceDto(
    long Id,
    string Name,
    string Theme,
    string LastVisitedPath,
    long[] FavoriteResourceIds,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AiWorkspaceUpdateRequest(
    string Name,
    string Theme,
    string LastVisitedPath,
    long[] FavoriteResourceIds);

public sealed record AiLibraryItem(
    string ResourceType,
    long ResourceId,
    string Name,
    string? Description,
    DateTime UpdatedAt,
    string Path,
    string Source = "custom",
    string? SubType = null,
    string? TypeLabel = null);

public sealed record AiLibraryImportRequest(
    string ResourceType,
    long LibraryItemId,
    long? TargetAppId,
    long? TargetWorkspaceId);

public sealed record AiLibraryExportRequest(
    string ResourceType,
    long ResourceId);

public sealed record AiLibraryMoveRequest(
    string ResourceType,
    long ResourceId);

public sealed record AiLibraryMutationResult(
    long ResourceId,
    string ResourceType,
    long LibraryItemId);

public sealed record AiLibraryQueryRequest(
    string? Keyword,
    string? ResourceType,
    int PageIndex,
    int PageSize,
    string? Source = null);

public sealed record AiLibraryPagedResult(
    IReadOnlyList<AiLibraryItem> Items,
    long Total,
    int PageIndex,
    int PageSize);
