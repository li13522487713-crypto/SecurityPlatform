using Atlas.Application.Assets.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Assets.Abstractions;

public interface IAssetQueryService
{
    PagedResult<AssetListItem> QueryAssets(PagedRequest request, TenantId tenantId);
}