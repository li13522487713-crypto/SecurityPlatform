using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AiDatabaseChannelConfigRepository : RepositoryBase<AiDatabaseChannelConfig>
{
    public AiDatabaseChannelConfigRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public Task<List<AiDatabaseChannelConfig>> ListByDatabaseAsync(TenantId tenantId, long databaseId, CancellationToken cancellationToken)
    {
        return Db.Queryable<AiDatabaseChannelConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DatabaseId == databaseId)
            .OrderBy(x => x.SortOrder, OrderByType.Asc)
            .OrderBy(x => x.Id, OrderByType.Asc)
            .ToListAsync(cancellationToken);
    }

    public Task DeleteByDatabaseAsync(TenantId tenantId, long databaseId, CancellationToken cancellationToken)
    {
        return Db.Deleteable<AiDatabaseChannelConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DatabaseId == databaseId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task AddRangeAsync(IReadOnlyCollection<AiDatabaseChannelConfig> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return Task.CompletedTask;
        }

        return Db.Insertable(items.ToList()).ExecuteCommandAsync(cancellationToken);
    }
}
