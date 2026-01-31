using Atlas.Application.Identity.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class DepartmentRepository : IDepartmentRepository
{
    private readonly ISqlSugarClient _db;

    public DepartmentRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<Department?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return await _db.Queryable<Department>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Department> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<Department>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var list = await query
            .OrderBy(x => x.SortOrder, OrderByType.Asc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (list, totalCount);
    }

    public async Task<(IReadOnlyList<Department> Items, int TotalCount)> QueryPageByIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> ids,
        int pageIndex,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return (Array.Empty<Department>(), 0);
        }

        var idArray = ids.Distinct().ToArray();
        var query = _db.Queryable<Department>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(idArray, x.Id));
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var list = await query
            .OrderBy(x => x.SortOrder, OrderByType.Asc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (list, totalCount);
    }

    public async Task<IReadOnlyList<Department>> QueryAllAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var list = await _db.Queryable<Department>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .OrderBy(x => x.SortOrder, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<IReadOnlyList<Department>> QueryByIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<Department>();
        }

        var idArray = ids.Distinct().ToArray();
        return await _db.Queryable<Department>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(idArray, x.Id))
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(Department department, CancellationToken cancellationToken)
    {
        return _db.Insertable(department).ExecuteCommandAsync(cancellationToken);
    }

    public Task UpdateAsync(Department department, CancellationToken cancellationToken)
    {
        return _db.Updateable(department).ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return _db.Deleteable<Department>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<bool> ExistsByParentIdAsync(TenantId tenantId, long parentId, CancellationToken cancellationToken)
    {
        var count = await _db.Queryable<Department>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ParentId == parentId)
            .CountAsync(cancellationToken);
        return count > 0;
    }
}
