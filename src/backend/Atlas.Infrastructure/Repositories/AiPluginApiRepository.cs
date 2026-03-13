using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AiPluginApiRepository : RepositoryBase<AiPluginApi>
{
    public AiPluginApiRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<AiPluginApi>> GetByPluginIdAsync(
        TenantId tenantId,
        long pluginId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<AiPluginApi>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.PluginId == pluginId)
            .OrderBy(x => x.CreatedAt, OrderByType.Asc)
            .ToListAsync(cancellationToken);
    }

    public async Task<AiPluginApi?> FindByPluginAndIdAsync(
        TenantId tenantId,
        long pluginId,
        long apiId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<AiPluginApi>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.PluginId == pluginId && x.Id == apiId)
            .FirstAsync(cancellationToken);
    }

    public Task DeleteByPluginIdAsync(TenantId tenantId, long pluginId, CancellationToken cancellationToken)
    {
        return Db.Deleteable<AiPluginApi>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.PluginId == pluginId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task AddRangeAsync(IReadOnlyCollection<AiPluginApi> apis, CancellationToken cancellationToken)
    {
        if (apis.Count == 0)
        {
            return Task.CompletedTask;
        }

        return Db.Insertable(apis.ToList()).ExecuteCommandAsync(cancellationToken);
    }
}
