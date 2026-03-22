using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class DynamicSchemaMigrationRepository : IDynamicSchemaMigrationRepository
{
    private readonly ISqlSugarClient _db;

    public DynamicSchemaMigrationRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<DynamicSchemaMigration> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        long tableId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<DynamicSchemaMigration>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TableId == tableId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, total);
    }

    public Task AddAsync(DynamicSchemaMigration migration, CancellationToken cancellationToken)
    {
        return _db.Insertable(migration).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<DynamicSchemaMigration?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<DynamicSchemaMigration>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }
}
