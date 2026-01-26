using Atlas.Application.Assets.Repositories;
using Atlas.Application.Assets.Abstractions;
using Atlas.Application.Assets.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using AutoMapper;

namespace Atlas.Infrastructure.Services;

public sealed class AssetQueryService : IAssetQueryService
{
    private readonly IAssetRepository _repository;
    private readonly IMapper _mapper;

    public AssetQueryService(IAssetRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<PagedResult<AssetListItem>> QueryAssetsAsync(
        PagedRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

        var (items, total) = await _repository.QueryPageAsync(
            pageIndex,
            pageSize,
            request.Keyword,
            cancellationToken);

        var resultItems = items.Select(x => _mapper.Map<AssetListItem>(x)).ToArray();
        return new PagedResult<AssetListItem>(resultItems, total, pageIndex, pageSize);
    }
}