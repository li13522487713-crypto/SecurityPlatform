using Atlas.Domain.System.Entities;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class DictDataRepository : RepositoryBase<DictData>
{
    public DictDataRepository(ISqlSugarClient db) : base(db) { }

    public async Task<(List<DictData> Items, long Total)> GetPagedByTypeCodeAsync(
        TenantId tenantId,
        string typeCode,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<DictData>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DictTypeCode == typeCode);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Label.Contains(keyword) || x.Value.Contains(keyword));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.SortOrder)
            .OrderBy(x => x.Id)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, total);
    }

    public async Task<List<DictData>> GetActiveByTypeCodeAsync(
        TenantId tenantId,
        string typeCode,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<DictData>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DictTypeCode == typeCode && x.Status)
            .OrderBy(x => x.SortOrder)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteByTypeCodeAsync(TenantId tenantId, string typeCode, CancellationToken cancellationToken)
    {
        await Db.Deleteable<DictData>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DictTypeCode == typeCode)
            .ExecuteCommandAsync(cancellationToken);
    }
}
