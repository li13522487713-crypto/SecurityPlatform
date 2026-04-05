using Atlas.Application.Assets.Repositories;
using Atlas.Application.Assets.Abstractions;
using Atlas.Application.Assets.Models;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using AutoMapper;

namespace Atlas.Infrastructure.Services;

public sealed class AssetQueryService : IAssetQueryService
{
    private readonly IAssetRepository _repository;
    private readonly ITenantDataScopeFilter _dataScopeFilter;
    private readonly IUserDepartmentRepository _userDepartmentRepository;
    private readonly IMapper _mapper;

    public AssetQueryService(
        IAssetRepository repository,
        ITenantDataScopeFilter dataScopeFilter,
        IUserDepartmentRepository userDepartmentRepository,
        IMapper mapper)
    {
        _repository = repository;
        _dataScopeFilter = dataScopeFilter;
        _userDepartmentRepository = userDepartmentRepository;
        _mapper = mapper;
    }

    public async Task<PagedResult<AssetListItem>> QueryAssetsAsync(
        PagedRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

        var ownerUserId = await _dataScopeFilter.GetOwnerFilterIdAsync(cancellationToken);
        var deptFilterIds = await _dataScopeFilter.GetDeptFilterIdsAsync(cancellationToken);
        long[]? createdByIn = null;
        if (deptFilterIds is not null)
        {
            if (deptFilterIds.Count == 0)
            {
                return new PagedResult<AssetListItem>(Array.Empty<AssetListItem>(), 0, pageIndex, pageSize);
            }

            var userIds = await _userDepartmentRepository.QueryUserIdsByDepartmentIdsAsync(
                tenantId,
                deptFilterIds,
                cancellationToken);
            createdByIn = userIds.Distinct().ToArray();
        }

        var (items, total) = await _repository.QueryPageAsync(
            tenantId,
            pageIndex,
            pageSize,
            request.Keyword,
            ownerUserId,
            createdByIn,
            cancellationToken);

        var resultItems = items.Select(x => _mapper.Map<AssetListItem>(x)).ToArray();
        return new PagedResult<AssetListItem>(resultItems, total, pageIndex, pageSize);
    }
}