using AutoMapper;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services;

public sealed class PositionQueryService : IPositionQueryService
{
    private readonly IPositionRepository _positionRepository;
    private readonly IMapper _mapper;

    public PositionQueryService(IPositionRepository positionRepository, IMapper mapper)
    {
        _positionRepository = positionRepository;
        _mapper = mapper;
    }

    public async Task<PagedResult<PositionListItem>> QueryPositionsAsync(
        PagedRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

        var (items, total) = await _positionRepository.QueryPageAsync(
            pageIndex,
            pageSize,
            request.Keyword,
            cancellationToken);

        var resultItems = items.Select(x => _mapper.Map<PositionListItem>(x)).ToArray();
        return new PagedResult<PositionListItem>(resultItems, total, pageIndex, pageSize);
    }

    public async Task<PositionDetail?> GetDetailAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        var position = await _positionRepository.FindByIdAsync(tenantId, id, cancellationToken);
        if (position is null)
        {
            return null;
        }

        return new PositionDetail(
            position.Id.ToString(),
            position.Name,
            position.Code,
            position.Description,
            position.IsActive,
            position.IsSystem,
            position.SortOrder);
    }

    public async Task<IReadOnlyList<PositionListItem>> QueryAllAsync(
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var items = await _positionRepository.QueryAllAsync(tenantId, cancellationToken);
        return items.Select(x => _mapper.Map<PositionListItem>(x)).ToArray();
    }
}
