using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class DynamicIndexRepository : IDynamicIndexRepository
{
    private readonly ISqlSugarClient _db;

    public DynamicIndexRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<DynamicIndex>> ListByTableIdAsync(
        TenantId tenantId,
        long tableId,
        CancellationToken cancellationToken)
    {
        var list = await _db.Queryable<DynamicIndex>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TableId == tableId)
            .ToListAsync(cancellationToken);

        return list;
    }

    public Task AddRangeAsync(IReadOnlyList<DynamicIndex> indexes, CancellationToken cancellationToken)
    {
        if (indexes.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _db.Insertable(indexes.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteByTableIdAsync(TenantId tenantId, long tableId, CancellationToken cancellationToken)
    {
        return _db.Deleteable<DynamicIndex>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TableId == tableId)
            .ExecuteCommandAsync(cancellationToken);
    }
}
