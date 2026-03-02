namespace Atlas.Application.LowCode.Models;

public sealed record LowCodeAppListItem(
    string Id,
    string AppKey,
    string Name,
    string? Description,
    string? Category,
    string? Icon,
    int Version,
    string Status,
    DateTimeOffset CreatedAt,
    long CreatedBy,
    DateTimeOffset? PublishedAt);

public sealed record LowCodeAppDetail(
    string Id,
    string AppKey,
    string Name,
    string? Description,
    string? Category,
    string? Icon,
    int Version,
    string Status,
    string? ConfigJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long CreatedBy,
    long UpdatedBy,
    DateTimeOffset? PublishedAt,
    long? PublishedBy,
    IReadOnlyList<LowCodePageListItem> Pages);

public sealed record LowCodeAppCreateRequest(
    string AppKey,
    string Name,
    string? Description,
    string? Category,
    string? Icon);

public sealed record LowCodeAppUpdateRequest(
    string Name,
    string? Description,
    string? Category,
    string? Icon);
