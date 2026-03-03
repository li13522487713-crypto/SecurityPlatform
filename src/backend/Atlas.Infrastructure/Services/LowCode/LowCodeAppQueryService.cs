using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services.LowCode;

public sealed class LowCodeAppQueryService : ILowCodeAppQueryService
{
    private readonly ILowCodeAppRepository _appRepository;
    private readonly ILowCodePageRepository _pageRepository;
    private readonly ILowCodePageVersionRepository _pageVersionRepository;

    public LowCodeAppQueryService(
        ILowCodeAppRepository appRepository,
        ILowCodePageRepository pageRepository,
        ILowCodePageVersionRepository pageVersionRepository)
    {
        _appRepository = appRepository;
        _pageRepository = pageRepository;
        _pageVersionRepository = pageVersionRepository;
    }

    public async Task<PagedResult<LowCodeAppListItem>> QueryAsync(
        PagedRequest request, TenantId tenantId, string? category = null,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await _appRepository.GetPagedAsync(
            tenantId, request.PageIndex, request.PageSize, request.Keyword, category, cancellationToken);

        var mapped = items.Select(e => new LowCodeAppListItem(
            e.Id.ToString(),
            e.AppKey,
            e.Name,
            e.Description,
            e.Category,
            e.Icon,
            e.Version,
            e.Status.ToString(),
            e.CreatedAt,
            e.CreatedBy,
            e.PublishedAt
        )).ToList();

        return new PagedResult<LowCodeAppListItem>(mapped, total, request.PageIndex, request.PageSize);
    }

    public async Task<LowCodeAppDetail?> GetByIdAsync(
        TenantId tenantId, long id, CancellationToken cancellationToken = default)
    {
        var entity = await _appRepository.GetByIdAsync(tenantId, id, cancellationToken);
        if (entity is null) return null;

        var pages = await _pageRepository.GetByAppIdAsync(tenantId, id, cancellationToken);
        return MapToDetail(entity, pages);
    }

    public async Task<LowCodeAppDetail?> GetByKeyAsync(
        TenantId tenantId, string appKey, CancellationToken cancellationToken = default)
    {
        var entity = await _appRepository.GetByKeyAsync(tenantId, appKey, cancellationToken);
        if (entity is null) return null;

        var pages = await _pageRepository.GetByAppIdAsync(tenantId, entity.Id, cancellationToken);
        return MapToDetail(entity, pages);
    }

    public async Task<LowCodeAppExportPackage?> ExportAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _appRepository.GetByIdAsync(tenantId, appId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var pages = await _pageRepository.GetByAppIdAsync(tenantId, appId, cancellationToken);
        var versions = await _pageVersionRepository.GetByAppIdAsync(tenantId, appId, cancellationToken);

        return new LowCodeAppExportPackage(
            entity.AppKey,
            entity.Name,
            entity.Description,
            entity.Category,
            entity.Icon,
            entity.Status.ToString(),
            entity.ConfigJson,
            pages.Select(x => new LowCodeAppExportPagePackage(
                x.Id.ToString(),
                x.PageKey,
                x.Name,
                x.PageType.ToString(),
                x.SchemaJson,
                x.RoutePath,
                x.Description,
                x.Icon,
                x.SortOrder,
                x.ParentPageId?.ToString(),
                x.PermissionCode,
                x.DataTableKey,
                x.IsPublished))
                .ToArray(),
            versions.Select(x => new LowCodeAppExportPageVersionPackage(
                x.Id.ToString(),
                x.PageId.ToString(),
                x.SnapshotVersion,
                x.PageKey,
                x.Name,
                x.PageType.ToString(),
                x.SchemaJson,
                x.RoutePath,
                x.Description,
                x.Icon,
                x.SortOrder,
                x.ParentPageId?.ToString(),
                x.PermissionCode,
                x.DataTableKey,
                x.CreatedAt,
                x.CreatedBy))
                .ToArray());
    }

    public async Task<LowCodePageDetail?> GetPageByIdAsync(
        TenantId tenantId, long pageId, CancellationToken cancellationToken = default)
    {
        var page = await _pageRepository.GetByIdAsync(tenantId, pageId, cancellationToken);
        return page is null ? null : MapToPageDetail(page);
    }

    public async Task<IReadOnlyList<LowCodePageTreeNode>> GetPageTreeAsync(
        TenantId tenantId, long appId, CancellationToken cancellationToken = default)
    {
        var pages = await _pageRepository.GetByAppIdAsync(tenantId, appId, cancellationToken);
        if (pages.Count == 0)
        {
            return Array.Empty<LowCodePageTreeNode>();
        }

        var pageMap = pages.ToDictionary(x => x.Id);
        var childLookup = pages
            .Where(x => x.ParentPageId.HasValue && pageMap.ContainsKey(x.ParentPageId.Value))
            .GroupBy(x => x.ParentPageId!.Value)
            .ToDictionary(
                x => x.Key,
                x => x.OrderBy(p => p.SortOrder).ThenBy(p => p.Id).ToArray());

        var roots = pages
            .Where(x => !x.ParentPageId.HasValue || !pageMap.ContainsKey(x.ParentPageId.Value))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => BuildTreeNode(x, childLookup))
            .ToArray();

        return roots;
    }

    public async Task<IReadOnlyList<LowCodePageVersionListItem>> GetPageVersionsAsync(
        TenantId tenantId,
        long pageId,
        CancellationToken cancellationToken = default)
    {
        var versions = await _pageVersionRepository.GetByPageIdAsync(tenantId, pageId, cancellationToken);
        return versions
            .Select(x => new LowCodePageVersionListItem(
                x.Id.ToString(),
                x.PageId.ToString(),
                x.SnapshotVersion,
                x.CreatedAt,
                x.CreatedBy))
            .ToArray();
    }

    public async Task<LowCodePageRuntimeSchema?> GetRuntimePageSchemaAsync(
        TenantId tenantId,
        long pageId,
        string mode,
        CancellationToken cancellationToken = default)
    {
        var page = await _pageRepository.GetByIdAsync(tenantId, pageId, cancellationToken);
        if (page is null)
        {
            return null;
        }

        var usePublished = string.Equals(mode, "published", StringComparison.OrdinalIgnoreCase);
        if (usePublished && !page.IsPublished)
        {
            return null;
        }
        var schema = usePublished
            ? (!string.IsNullOrWhiteSpace(page.PublishedSchemaJson) ? page.PublishedSchemaJson : page.SchemaJson)
            : page.SchemaJson;
        var version = usePublished && page.PublishedVersion.HasValue ? page.PublishedVersion.Value : page.Version;

        return new LowCodePageRuntimeSchema(
            page.Id.ToString(),
            page.PageKey,
            page.Name,
            schema,
            version,
            usePublished ? "published" : "draft");
    }

    private static LowCodeAppDetail MapToDetail(
        Atlas.Domain.LowCode.Entities.LowCodeApp entity,
        IReadOnlyList<Atlas.Domain.LowCode.Entities.LowCodePage> pages)
    {
        var pageItems = pages.Select(p => new LowCodePageListItem(
            p.Id.ToString(),
            p.AppId.ToString(),
            p.PageKey,
            p.Name,
            p.PageType.ToString(),
            p.RoutePath,
            p.Description,
            p.Icon,
            p.SortOrder,
            p.ParentPageId?.ToString(),
            p.Version,
            p.IsPublished,
            p.PublishedVersion,
            p.CreatedAt,
            p.PermissionCode,
            p.DataTableKey
        )).ToList();

        return new LowCodeAppDetail(
            entity.Id.ToString(),
            entity.AppKey,
            entity.Name,
            entity.Description,
            entity.Category,
            entity.Icon,
            entity.Version,
            entity.Status.ToString(),
            entity.ConfigJson,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.CreatedBy,
            entity.UpdatedBy,
            entity.PublishedAt,
            entity.PublishedBy,
            pageItems
        );
    }

    private static LowCodePageDetail MapToPageDetail(Atlas.Domain.LowCode.Entities.LowCodePage page)
    {
        return new LowCodePageDetail(
            page.Id.ToString(),
            page.AppId.ToString(),
            page.PageKey,
            page.Name,
            page.PageType.ToString(),
            page.SchemaJson,
            page.RoutePath,
            page.Description,
            page.Icon,
            page.SortOrder,
            page.ParentPageId?.ToString(),
            page.Version,
            page.IsPublished,
            page.PublishedVersion,
            page.CreatedAt,
            page.UpdatedAt,
            page.CreatedBy,
            page.UpdatedBy,
            page.PermissionCode,
            page.DataTableKey
        );
    }

    private static LowCodePageTreeNode BuildTreeNode(
        Atlas.Domain.LowCode.Entities.LowCodePage page,
        IReadOnlyDictionary<long, Atlas.Domain.LowCode.Entities.LowCodePage[]> childLookup)
    {
        var children = childLookup.TryGetValue(page.Id, out var rawChildren)
            ? rawChildren.Select(x => BuildTreeNode(x, childLookup)).ToArray()
            : Array.Empty<LowCodePageTreeNode>();

        return new LowCodePageTreeNode(
            page.Id.ToString(),
            page.AppId.ToString(),
            page.PageKey,
            page.Name,
            page.PageType.ToString(),
            page.RoutePath,
            page.Description,
            page.Icon,
            page.SortOrder,
            page.ParentPageId?.ToString(),
            page.Version,
            page.IsPublished,
            page.CreatedAt,
            page.PermissionCode,
            page.DataTableKey,
            children
        );
    }
}
