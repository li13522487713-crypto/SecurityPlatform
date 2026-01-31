using AutoMapper;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services;

public sealed class MenuQueryService : IMenuQueryService
{
    private readonly IMenuRepository _menuRepository;
    private readonly IMapper _mapper;

    public MenuQueryService(IMenuRepository menuRepository, IMapper mapper)
    {
        _menuRepository = menuRepository;
        _mapper = mapper;
    }

    public async Task<PagedResult<MenuListItem>> QueryMenusAsync(
        MenuQueryRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

        var (items, total) = await _menuRepository.QueryPageAsync(
            tenantId,
            pageIndex,
            pageSize,
            request.Keyword,
            request.IsHidden,
            cancellationToken);

        var resultItems = items.Select(x => _mapper.Map<MenuListItem>(x)).ToArray();
        return new PagedResult<MenuListItem>(resultItems, total, pageIndex, pageSize);
    }

    public async Task<IReadOnlyList<MenuListItem>> QueryAllAsync(
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var items = await _menuRepository.QueryAllAsync(tenantId, cancellationToken);
        return items.Select(x => _mapper.Map<MenuListItem>(x)).ToArray();
    }
}
