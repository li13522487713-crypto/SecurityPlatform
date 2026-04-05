using Atlas.Application.Assets.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Assets.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AssetRepository : IAssetRepository
{
    private readonly ISqlSugarClient _db;

    public AssetRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<long> AddAsync(Asset asset, CancellationToken cancellationToken)
    {
        await _db.Insertable(asset).ExecuteCommandAsync(cancellationToken);
        return asset.Id;
    }

    public async Task<(IReadOnlyList<Asset> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        string? keyword,
        long? ownerUserId,
        IReadOnlyList<long>? createdByUserIdsIn,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<Asset>().Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword));
        }

        if (ownerUserId.HasValue)
        {
            query = query.Where(x => x.CreatedByUserId == ownerUserId.Value);
        }

        if (createdByUserIdsIn is not null)
        {
            if (createdByUserIdsIn.Count == 0)
            {
                return (Array.Empty<Asset>(), 0);
            }

            var idArray = createdByUserIdsIn.Distinct().ToArray();
            query = query.Where(x => x.CreatedByUserId != null && SqlFunc.ContainsArray(idArray, x.CreatedByUserId.Value));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var list = await query
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (list, totalCount);
    }
}