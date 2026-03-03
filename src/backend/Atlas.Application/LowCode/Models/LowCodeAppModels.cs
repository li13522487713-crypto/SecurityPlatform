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

public sealed record LowCodeAppExportPackage(
    string AppKey,
    string Name,
    string? Description,
    string? Category,
    string? Icon,
    string Status,
    string? ConfigJson,
    IReadOnlyList<LowCodeAppExportPagePackage> Pages,
    IReadOnlyList<LowCodeAppExportPageVersionPackage> PageVersions);

public sealed record LowCodeAppExportPagePackage(
    string Id,
    string PageKey,
    string Name,
    string PageType,
    string SchemaJson,
    string? RoutePath,
    string? Description,
    string? Icon,
    int SortOrder,
    string? ParentPageId,
    string? PermissionCode,
    string? DataTableKey,
    bool IsPublished);

public sealed record LowCodeAppExportPageVersionPackage(
    string Id,
    string PageId,
    int SnapshotVersion,
    string PageKey,
    string Name,
    string PageType,
    string SchemaJson,
    string? RoutePath,
    string? Description,
    string? Icon,
    int SortOrder,
    string? ParentPageId,
    string? PermissionCode,
    string? DataTableKey,
    DateTimeOffset CreatedAt,
    long CreatedBy);

public sealed record LowCodeAppImportRequest(
    LowCodeAppExportPackage Package,
    string ConflictStrategy,
    string? KeySuffix);

public sealed record LowCodeAppImportResult(
    string AppId,
    string AppKey,
    bool Skipped,
    bool Overwritten,
    int ImportedPageCount,
    int ImportedVersionCount);
