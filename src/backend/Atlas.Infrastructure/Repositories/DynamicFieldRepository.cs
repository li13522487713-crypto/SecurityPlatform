using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class DynamicFieldRepository : IDynamicFieldRepository
{
    private readonly ISqlSugarClient _db;

    public DynamicFieldRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<DynamicField>> ListByTableIdAsync(
        TenantId tenantId,
        long tableId,
        CancellationToken cancellationToken)
    {
        var list = await _db.Queryable<DynamicField>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TableId == tableId)
            .OrderBy(x => x.SortOrder, OrderByType.Asc)
            .ToListAsync(cancellationToken);

        return list;
    }

    public Task AddRangeAsync(IReadOnlyList<DynamicField> fields, CancellationToken cancellationToken)
    {
        if (fields.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _db.Insertable(fields.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public Task UpdateRangeAsync(IReadOnlyList<DynamicField> fields, CancellationToken cancellationToken)
    {
        if (fields.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _db.Updateable(fields.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteByTableIdAsync(TenantId tenantId, long tableId, CancellationToken cancellationToken)
    {
        return _db.Deleteable<DynamicField>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TableId == tableId)
            .ExecuteCommandAsync(cancellationToken);
    }
}
