using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class DynamicRelationRepository : IDynamicRelationRepository
{
    private readonly ISqlSugarClient _db;

    public DynamicRelationRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<DynamicRelation>> ListByTableIdAsync(
        TenantId tenantId,
        long tableId,
        CancellationToken cancellationToken)
    {
        var list = await _db.Queryable<DynamicRelation>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TableId == tableId)
            .ToListAsync(cancellationToken);
        return list;
    }

    public Task AddRangeAsync(
        IReadOnlyList<DynamicRelation> relations,
        CancellationToken cancellationToken)
    {
        if (relations.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _db.Insertable(relations.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteByTableIdAsync(
        TenantId tenantId,
        long tableId,
        CancellationToken cancellationToken)
    {
        return _db.Deleteable<DynamicRelation>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TableId == tableId)
            .ExecuteCommandAsync(cancellationToken);
    }
}
