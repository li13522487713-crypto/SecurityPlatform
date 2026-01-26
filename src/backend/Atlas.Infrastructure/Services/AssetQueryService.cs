using Atlas.Application.Assets.Abstractions;
using Atlas.Application.Assets.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services;

public sealed class AssetQueryService : IAssetQueryService
{
    public PagedResult<AssetListItem> QueryAssets(PagedRequest request, TenantId tenantId)
    {
        var items = Array.Empty<AssetListItem>();
        return new PagedResult<AssetListItem>(items, 0, request.PageIndex, request.PageSize);
    }
}