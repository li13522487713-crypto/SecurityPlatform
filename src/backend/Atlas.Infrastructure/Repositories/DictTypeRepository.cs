using Atlas.Domain.System.Entities;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class DictTypeRepository : RepositoryBase<DictType>
{
    public DictTypeRepository(ISqlSugarClient db) : base(db) { }

    public async Task<(List<DictType> Items, long Total)> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<DictType>()
            .Where(x => x.TenantIdValue == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword) || x.Code.Contains(keyword));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.Id)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, total);
    }

    public async Task<List<DictType>> GetAllActiveAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        return await Db.Queryable<DictType>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Status)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<DictType?> FindByCodeAsync(TenantId tenantId, string code, CancellationToken cancellationToken)
    {
        return await Db.Queryable<DictType>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Code == code)
            .FirstAsync(cancellationToken);
    }

    public async Task<bool> ExistsByCodeAsync(TenantId tenantId, string code, CancellationToken cancellationToken)
    {
        var count = await Db.Queryable<DictType>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Code == code)
            .CountAsync(cancellationToken);
        return count > 0;
    }
}
