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
        return await FindByKeyAsync(tenantId, configKey, appId: null, cancellationToken);
    }

    public async Task<SystemConfig?> FindExactByKeyAsync(
        TenantId tenantId,
        string configKey,
        string? appId,
        CancellationToken cancellationToken)
    {
        var normalizedAppId = NormalizeAppId(appId);
        return await Db.Queryable<SystemConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ConfigKey == configKey && x.AppId == normalizedAppId)
            .FirstAsync(cancellationToken);
    }

    public async Task<SystemConfig?> FindByKeyAsync(
        TenantId tenantId,
        string configKey,
        string? appId,
        CancellationToken cancellationToken)
    {
        var normalizedAppId = NormalizeAppId(appId);
        if (!string.IsNullOrWhiteSpace(normalizedAppId))
        {
            var appScoped = await Db.Queryable<SystemConfig>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.ConfigKey == configKey && x.AppId == normalizedAppId)
                .FirstAsync(cancellationToken);
            if (appScoped is not null)
            {
                return appScoped;
            }
        }

        return await Db.Queryable<SystemConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ConfigKey == configKey && x.AppId == null)
            .FirstAsync(cancellationToken);
    }

    public async Task<bool> ExistsByKeyAsync(TenantId tenantId, string configKey, CancellationToken cancellationToken)
    {
        return await ExistsByKeyAsync(tenantId, configKey, appId: null, cancellationToken);
    }

    public async Task<bool> ExistsByKeyAsync(
        TenantId tenantId,
        string configKey,
        string? appId,
        CancellationToken cancellationToken)
    {
        var normalizedAppId = NormalizeAppId(appId);
        var count = await Db.Queryable<SystemConfig>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && x.ConfigKey == configKey
                && x.AppId == normalizedAppId)
            .CountAsync(cancellationToken);
        return count > 0;
    }

    public async Task<List<SystemConfig>> GetByKeysAsync(
        TenantId tenantId,
        IReadOnlyCollection<string> configKeys,
        CancellationToken cancellationToken)
    {
        return await GetByKeysAsync(tenantId, configKeys, appId: null, cancellationToken);
    }

    public async Task<List<SystemConfig>> GetByKeysAsync(
        TenantId tenantId,
        IReadOnlyCollection<string> configKeys,
        string? appId,
        CancellationToken cancellationToken)
    {
        if (configKeys.Count == 0)
        {
            return [];
        }

        var keyArray = configKeys.ToArray();
        var normalizedAppId = NormalizeAppId(appId);
        if (string.IsNullOrWhiteSpace(normalizedAppId))
        {
            return await Db.Queryable<SystemConfig>()
                .Where(x =>
                    x.TenantIdValue == tenantId.Value
                    && SqlFunc.ContainsArray(keyArray, x.ConfigKey)
                    && x.AppId == null)
                .ToListAsync(cancellationToken);
        }

        var candidates = await Db.Queryable<SystemConfig>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && SqlFunc.ContainsArray(keyArray, x.ConfigKey)
                && (x.AppId == normalizedAppId || x.AppId == null))
            .ToListAsync(cancellationToken);

        var selected = new Dictionary<string, SystemConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in candidates)
        {
            if (!selected.TryGetValue(item.ConfigKey, out var existing))
            {
                selected[item.ConfigKey] = item;
                continue;
            }

            if (existing.AppId is null && string.Equals(item.AppId, normalizedAppId, StringComparison.OrdinalIgnoreCase))
            {
                selected[item.ConfigKey] = item;
            }
        }

        var result = new List<SystemConfig>(keyArray.Length);
        foreach (var key in keyArray)
        {
            if (selected.TryGetValue(key, out var item))
            {
                result.Add(item);
            }
        }

        return result;
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

        await Db.Updateable(entities.ToArray())
            .WhereColumns(static x => new { x.Id })
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<List<SystemConfig>> FindByTypeAsync(TenantId tenantId, string configType, CancellationToken cancellationToken)
    {
        return await Db.Queryable<SystemConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ConfigType == configType && x.AppId == null)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<SystemConfig>> ListByFiltersAsync(
        TenantId tenantId,
        string? groupName,
        string? appId,
        CancellationToken cancellationToken)
    {
        var normalizedGroupName = string.IsNullOrWhiteSpace(groupName) ? null : groupName.Trim();
        var normalizedAppId = NormalizeAppId(appId);

        var query = Db.Queryable<SystemConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(normalizedGroupName))
        {
            query = query.Where(x => x.GroupName == normalizedGroupName);
        }

        if (string.IsNullOrWhiteSpace(normalizedAppId))
        {
            return await query
                .Where(x => x.AppId == null)
                .OrderBy(x => x.ConfigKey)
                .ToListAsync(cancellationToken);
        }

        var candidates = await query
            .Where(x => x.AppId == normalizedAppId || x.AppId == null)
            .ToListAsync(cancellationToken);

        var selected = new Dictionary<string, SystemConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in candidates)
        {
            if (!selected.TryGetValue(item.ConfigKey, out var existing))
            {
                selected[item.ConfigKey] = item;
                continue;
            }

            if (existing.AppId is null && string.Equals(item.AppId, normalizedAppId, StringComparison.OrdinalIgnoreCase))
            {
                selected[item.ConfigKey] = item;
            }
        }

        return selected.Values
            .OrderBy(x => x.ConfigKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<List<SystemConfig>> GetByKeysAndAppIdsAsync(
        TenantId tenantId,
        IReadOnlyCollection<string> configKeys,
        IReadOnlyCollection<string?> appIds,
        CancellationToken cancellationToken)
    {
        if (configKeys.Count == 0 || appIds.Count == 0)
        {
            return [];
        }

        var keyArray = configKeys.ToArray();
        var normalizedAppIds = appIds
            .Select(NormalizeAppId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var includePlatform = normalizedAppIds.Any(static x => x is null);
        var appIdArray = normalizedAppIds.Where(static x => x is not null).Cast<string>().ToArray();

        var query = Db.Queryable<SystemConfig>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(keyArray, x.ConfigKey));

        if (appIdArray.Length == 0)
        {
            query = query.Where(x => x.AppId == null);
        }
        else if (includePlatform)
        {
            query = query.Where(x => x.AppId == null || SqlFunc.ContainsArray(appIdArray, x.AppId!));
        }
        else
        {
            query = query.Where(x => x.AppId != null && SqlFunc.ContainsArray(appIdArray, x.AppId));
        }

        return await query.ToListAsync(cancellationToken);
    }

    private static string? NormalizeAppId(string? appId)
    {
        return string.IsNullOrWhiteSpace(appId) ? null : appId.Trim();
    }
}
