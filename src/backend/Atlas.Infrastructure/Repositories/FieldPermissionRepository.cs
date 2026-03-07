using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class FieldPermissionRepository : IFieldPermissionRepository
{
    private readonly ISqlSugarClient _db;

    public FieldPermissionRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<FieldPermission>> ListByTableKeyAsync(
        TenantId tenantId,
        string tableKey,
        long? appId,
        CancellationToken cancellationToken)
    {
        var scopedTableKey = BuildScopedTableKey(tableKey, appId);
        return await _db.Queryable<FieldPermission>()
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
        var scopedTableKey = BuildScopedTableKey(tableKey, appId);
        var result = await _db.Ado.UseTranAsync(async () =>
        {
            await _db.Deleteable<FieldPermission>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.TableKey == scopedTableKey)
                .ExecuteCommandAsync(cancellationToken);

            if (permissions.Count > 0)
            {
                await _db.Insertable(permissions.ToList()).ExecuteCommandAsync(cancellationToken);
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
}
