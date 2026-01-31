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
    private readonly IProjectPositionRepository _projectPositionRepository;
    private readonly Atlas.Core.Identity.IProjectContextAccessor _projectContextAccessor;
    private readonly IMapper _mapper;

    public PositionQueryService(
        IPositionRepository positionRepository,
        IProjectPositionRepository projectPositionRepository,
        Atlas.Core.Identity.IProjectContextAccessor projectContextAccessor,
        IMapper mapper)
    {
        _positionRepository = positionRepository;
        _projectPositionRepository = projectPositionRepository;
        _projectContextAccessor = projectContextAccessor;
        _mapper = mapper;
    }

    public async Task<PagedResult<PositionListItem>> QueryPositionsAsync(
        PagedRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

        var projectContext = _projectContextAccessor.GetCurrent();
        (IReadOnlyList<Atlas.Domain.Identity.Entities.Position> Items, int TotalCount) result;
        if (projectContext.IsEnabled && projectContext.ProjectId.HasValue)
        {
            var relations = await _projectPositionRepository.QueryByProjectIdAsync(
                tenantId,
                projectContext.ProjectId.Value,
                cancellationToken);
            var positionIds = relations.Select(x => x.PositionId).Distinct().ToArray();
            result = await _positionRepository.QueryPageByIdsAsync(
                tenantId,
                positionIds,
                pageIndex,
                pageSize,
                request.Keyword,
                cancellationToken);
        }
        else
        {
            result = await _positionRepository.QueryPageAsync(
                tenantId,
                pageIndex,
                pageSize,
                request.Keyword,
                cancellationToken);
        }

        var resultItems = result.Items.Select(x => _mapper.Map<PositionListItem>(x)).ToArray();
        return new PagedResult<PositionListItem>(resultItems, result.TotalCount, pageIndex, pageSize);
    }

    public async Task<PositionDetail?> GetDetailAsync(long id, TenantId tenantId, CancellationToken cancellationToken)
    {
        var projectContext = _projectContextAccessor.GetCurrent();
        if (projectContext.IsEnabled && projectContext.ProjectId.HasValue)
        {
            var relations = await _projectPositionRepository.QueryByProjectIdAsync(
                tenantId,
                projectContext.ProjectId.Value,
                cancellationToken);
            if (!relations.Any(x => x.PositionId == id))
            {
                return null;
            }
        }

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
        var projectContext = _projectContextAccessor.GetCurrent();
        IReadOnlyList<Atlas.Domain.Identity.Entities.Position> items;
        if (projectContext.IsEnabled && projectContext.ProjectId.HasValue)
        {
            var relations = await _projectPositionRepository.QueryByProjectIdAsync(
                tenantId,
                projectContext.ProjectId.Value,
                cancellationToken);
            var positionIds = relations.Select(x => x.PositionId).Distinct().ToArray();
            items = await _positionRepository.QueryByIdsAsync(tenantId, positionIds, cancellationToken);
        }
        else
        {
            items = await _positionRepository.QueryAllAsync(tenantId, cancellationToken);
        }

        return items.Select(x => _mapper.Map<PositionListItem>(x)).ToArray();
    }
}
