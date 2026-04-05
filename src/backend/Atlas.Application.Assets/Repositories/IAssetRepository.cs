using Atlas.Core.Tenancy;
using Atlas.Domain.Assets.Entities;

namespace Atlas.Application.Assets.Repositories;

public interface IAssetRepository
{
    Task<long> AddAsync(Asset asset, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Asset> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        string? keyword,
        long? ownerUserId,
        IReadOnlyList<long>? createdByUserIdsIn,
        CancellationToken cancellationToken);
}