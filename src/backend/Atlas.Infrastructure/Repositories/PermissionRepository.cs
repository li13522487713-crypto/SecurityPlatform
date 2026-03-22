using Atlas.Application.Identity.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class PermissionRepository : RepositoryBase<Permission>, IPermissionRepository
{
    public PermissionRepository(ISqlSugarClient db) : base(db) { }

    public async Task<Permission?> FindByCodeAsync(TenantId tenantId, string code, CancellationToken cancellationToken)
    {
        return await Db.Queryable<Permission>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Code == code)
            .FirstAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Permission> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        string? keyword,
        string? type,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<Permission>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword) || x.Code.Contains(keyword));
        }
        if (!string.IsNullOrWhiteSpace(type))
        {
            var normalized = type.Trim();
            query = query.Where(x => x.Type == normalized);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var list = await query
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (list, totalCount);
    }

    public async Task<IReadOnlyList<Permission>> QueryAllAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        return await Db.Queryable<Permission>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .OrderBy(x => x.Code, OrderByType.Asc)
            .ToListAsync(cancellationToken);
    }

    public override async Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        await Db.Deleteable<Permission>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .ExecuteCommandAsync(cancellationToken);
    }
}
