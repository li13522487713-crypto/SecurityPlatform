using Atlas.Application.Identity.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class PermissionRepository : IPermissionRepository
{
    private readonly ISqlSugarClient _db;

    public PermissionRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<Permission?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return await _db.Queryable<Permission>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<Permission?> FindByCodeAsync(TenantId tenantId, string code, CancellationToken cancellationToken)
    {
        return await _db.Queryable<Permission>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Code == code)
            .FirstAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Permission> Items, int TotalCount)> QueryPageAsync(
        int pageIndex,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<Permission>();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword) || x.Code.Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var list = await query
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (list, totalCount);
    }

    public async Task<IReadOnlyList<Permission>> QueryByIdsAsync(TenantId tenantId, IReadOnlyList<long> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<Permission>();
        }

        var idArray = ids.Distinct().ToArray();
        var list = await _db.Queryable<Permission>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(idArray, x.Id))
            .ToListAsync(cancellationToken);
        return list;
    }

    public Task AddAsync(Permission permission, CancellationToken cancellationToken)
    {
        return _db.Insertable(permission).ExecuteCommandAsync(cancellationToken);
    }

    public Task UpdateAsync(Permission permission, CancellationToken cancellationToken)
    {
        return _db.Updateable(permission)
            .Where(x => x.Id == permission.Id && x.TenantIdValue == permission.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }
}
