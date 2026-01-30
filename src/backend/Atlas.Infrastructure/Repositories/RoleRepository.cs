using Atlas.Application.Identity.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class RoleRepository : IRoleRepository
{
    private readonly ISqlSugarClient _db;

    public RoleRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<Role?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return await _db.Queryable<Role>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<Role?> FindByCodeAsync(TenantId tenantId, string code, CancellationToken cancellationToken)
    {
        return await _db.Queryable<Role>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Code == code)
            .FirstAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Role> Items, int TotalCount)> QueryPageAsync(
        int pageIndex,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<Role>();
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

    public async Task<IReadOnlyList<Role>> QueryByIdsAsync(TenantId tenantId, IReadOnlyList<long> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<Role>();
        }

        var list = await _db.Queryable<Role>()
            .Where(x => x.TenantIdValue == tenantId.Value && ids.Contains(x.Id))
            .ToListAsync(cancellationToken);
        return list;
    }

    public Task AddAsync(Role role, CancellationToken cancellationToken)
    {
        return _db.Insertable(role).ExecuteCommandAsync(cancellationToken);
    }

    public Task UpdateAsync(Role role, CancellationToken cancellationToken)
    {
        return _db.Updateable(role).ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return _db.Deleteable<Role>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .ExecuteCommandAsync(cancellationToken);
    }
}
