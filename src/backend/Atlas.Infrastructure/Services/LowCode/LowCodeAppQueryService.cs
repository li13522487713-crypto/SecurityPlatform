using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services.LowCode;

public sealed class LowCodeAppQueryService : ILowCodeAppQueryService
{
    private readonly ILowCodeAppRepository _appRepository;
    private readonly ILowCodePageRepository _pageRepository;

    public LowCodeAppQueryService(
        ILowCodeAppRepository appRepository,
        ILowCodePageRepository pageRepository)
    {
        _appRepository = appRepository;
        _pageRepository = pageRepository;
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
}
