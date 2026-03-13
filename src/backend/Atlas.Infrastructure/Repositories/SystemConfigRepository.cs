using Atlas.Domain.System.Entities;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class SystemConfigRepository : RepositoryBase<SystemConfig>
{
    public SystemConfigRepository(ISqlSugarClient db) : base(db) { }

    public async Task<(List<SystemConfig> Items, long Total)> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<SystemConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.ConfigKey.Contains(keyword) || x.ConfigName.Contains(keyword));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.IsBuiltIn, OrderByType.Desc)
            .OrderBy(x => x.Id)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, total);
    }

    public async Task<SystemConfig?> FindByKeyAsync(TenantId tenantId, string configKey, CancellationToken cancellationToken)
    {
        return await Db.Queryable<SystemConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ConfigKey == configKey)
            .FirstAsync(cancellationToken);
    }

    public async Task<bool> ExistsByKeyAsync(TenantId tenantId, string configKey, CancellationToken cancellationToken)
    {
        var count = await Db.Queryable<SystemConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ConfigKey == configKey)
            .CountAsync(cancellationToken);
        return count > 0;
    }

    public async Task<List<SystemConfig>> GetByKeysAsync(
        TenantId tenantId,
        IReadOnlyCollection<string> configKeys,
        CancellationToken cancellationToken)
    {
        if (configKeys.Count == 0)
        {
            return [];
        }

        var keyArray = configKeys.ToArray();
        return await Db.Queryable<SystemConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(keyArray, x.ConfigKey))
            .ToListAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IReadOnlyCollection<SystemConfig> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
        {
            return;
        }

        await Db.Insertable(entities.ToArray()).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateRangeAsync(IReadOnlyCollection<SystemConfig> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
        {
            return;
        }

        await Db.Updateable(entities.ToArray()).ExecuteCommandAsync(cancellationToken);
    }
}
