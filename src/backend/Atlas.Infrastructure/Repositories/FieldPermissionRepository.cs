using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Infrastructure.Services;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class FieldPermissionRepository : IFieldPermissionRepository
{
    private readonly ISqlSugarClient _mainDb;
    private readonly IAppDbScopeFactory _appDbScopeFactory;
    private readonly IAppContextAccessor _appContextAccessor;

    public FieldPermissionRepository(
        ISqlSugarClient mainDb,
        IAppDbScopeFactory appDbScopeFactory,
        IAppContextAccessor appContextAccessor)
    {
        _mainDb = mainDb;
        _appDbScopeFactory = appDbScopeFactory;
        _appContextAccessor = appContextAccessor;
    }

    public FieldPermissionRepository(ISqlSugarClient db)
        : this(db, new MainOnlyAppDbScopeFactory(db), NullAppContextAccessor.Instance)
    {
    }

    public async Task<IReadOnlyList<FieldPermission>> ListByTableKeyAsync(
        TenantId tenantId,
        string tableKey,
        long? appId,
        CancellationToken cancellationToken)
    {
        var db = await GetDbAsync(tenantId, appId, cancellationToken);
        var scopedTableKey = BuildScopedTableKey(tableKey, appId);
        return await db.Queryable<FieldPermission>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TableKey == scopedTableKey)
            .ToListAsync(cancellationToken);
    }

    public async Task ReplaceByTableKeyAsync(
        TenantId tenantId,
        string tableKey,
        long? appId,
        IReadOnlyList<FieldPermission> permissions,
        CancellationToken cancellationToken)
    {
        var db = await GetDbAsync(tenantId, appId, cancellationToken);
        var scopedTableKey = BuildScopedTableKey(tableKey, appId);
        var result = await db.Ado.UseTranAsync(async () =>
        {
            await db.Deleteable<FieldPermission>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.TableKey == scopedTableKey)
                .ExecuteCommandAsync(cancellationToken);

            if (permissions.Count > 0)
            {
                await db.Insertable(permissions.ToList()).ExecuteCommandAsync(cancellationToken);
            }
        });

        if (!result.IsSuccess)
        {
            throw result.ErrorException ?? new InvalidOperationException("替换字段权限失败。");
        }
    }

    private static string BuildScopedTableKey(string tableKey, long? appId)
    {
        return appId.HasValue ? $"app:{appId.Value}:{tableKey}" : tableKey;
    }

    public async Task<IReadOnlyList<FieldPermission>> ListByRoleCodeAndAppIdAsync(
        TenantId tenantId,
        long appId,
        string roleCode,
        CancellationToken cancellationToken)
    {
        var db = await GetDbAsync(tenantId, appId, cancellationToken);
        var prefix = $"app:{appId}:";
        return await db.Queryable<FieldPermission>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value
                && x.RoleCode == roleCode
                && x.TableKey.StartsWith(prefix))
            .ToListAsync(cancellationToken);
    }

    public async Task ReplaceByRoleCodeAndAppIdAsync(
        TenantId tenantId,
        long appId,
        string roleCode,
        IReadOnlyList<FieldPermission> permissions,
        CancellationToken cancellationToken)
    {
        var db = await GetDbAsync(tenantId, appId, cancellationToken);
        var prefix = $"app:{appId}:";
        var result = await db.Ado.UseTranAsync(async () =>
        {
            await db.Deleteable<FieldPermission>()
                .Where(x =>
                    x.TenantIdValue == tenantId.Value
                    && x.RoleCode == roleCode
                    && x.TableKey.StartsWith(prefix))
                .ExecuteCommandAsync(cancellationToken);

            if (permissions.Count > 0)
            {
                await db.Insertable(permissions.ToList()).ExecuteCommandAsync(cancellationToken);
            }
        });

        if (!result.IsSuccess)
        {
            throw result.ErrorException ?? new InvalidOperationException("替换角色字段权限失败。");
        }
    }

    private async Task<ISqlSugarClient> GetDbAsync(TenantId tenantId, long? appId, CancellationToken cancellationToken)
    {
        if (appId.HasValue && appId.Value > 0)
        {
            return await _appDbScopeFactory.GetAppClientAsync(tenantId, appId.Value, cancellationToken);
        }

        var contextAppId = _appContextAccessor.ResolveAppId();
        if (contextAppId.HasValue && contextAppId.Value > 0)
        {
            return await _appDbScopeFactory.GetAppClientAsync(tenantId, contextAppId.Value, cancellationToken);
        }

        return _mainDb;
    }
}
