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
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<FieldPermission>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TableKey == tableKey)
            .ToListAsync(cancellationToken);
    }

    public async Task ReplaceByTableKeyAsync(
        TenantId tenantId,
        string tableKey,
        IReadOnlyList<FieldPermission> permissions,
        CancellationToken cancellationToken)
    {
        var result = await _db.Ado.UseTranAsync(async () =>
        {
            await _db.Deleteable<FieldPermission>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.TableKey == tableKey)
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
}
