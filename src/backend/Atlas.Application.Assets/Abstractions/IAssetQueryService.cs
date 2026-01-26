using Atlas.Application.Assets.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Assets.Abstractions;

public interface IAssetQueryService
{
    Task<PagedResult<AssetListItem>> QueryAssetsAsync(
        PagedRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken);
}