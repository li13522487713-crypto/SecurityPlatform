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

    public async Task<IReadOnlyList<DynamicField>> ListByTableIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> tableIds,
        CancellationToken cancellationToken)
    {
        if (tableIds.Count == 0)
        {
            return Array.Empty<DynamicField>();
        }

        var ids = tableIds.Distinct().ToArray();
        return await _db.Queryable<DynamicField>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(ids, x.TableId))
            .ToListAsync(cancellationToken);
    }

    public Task AddRangeAsync(IReadOnlyList<DynamicField> fields, CancellationToken cancellationToken)
    {
        if (fields.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _db.Insertable(fields.ToList()).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateRangeAsync(IReadOnlyList<DynamicField> fields, CancellationToken cancellationToken)
    {
        if (fields.Count == 0)
        {
            return;
        }

        var affected = await _db.Updateable(fields.ToList())
            .WhereColumns(x => new { x.Id, x.TenantIdValue })
            .ExecuteCommandAsync(cancellationToken);

        if (affected == 0 && fields.Count > 0)
        {
            throw new InvalidOperationException("批量更新动态字段失败。");
        }
    }

    public Task DeleteByTableIdAsync(TenantId tenantId, long tableId, CancellationToken cancellationToken)
    {
        return _db.Deleteable<DynamicField>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TableId == tableId)
            .ExecuteCommandAsync(cancellationToken);
    }
}
