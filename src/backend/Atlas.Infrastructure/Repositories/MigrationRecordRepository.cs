using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class MigrationRecordRepository : IMigrationRecordRepository
{
    private readonly ISqlSugarClient _db;

    public MigrationRecordRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<MigrationRecord> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        string? keyword,
        string? tableKey,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<MigrationRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.TableKey.Contains(keyword) || x.Status.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(tableKey))
        {
            query = query.Where(x => x.TableKey == tableKey);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, totalCount);
    }

    public Task<MigrationRecord?> FindByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        return _db.Queryable<MigrationRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public Task<MigrationRecord?> FindByVersionAsync(
        TenantId tenantId,
        string tableKey,
        int version,
        CancellationToken cancellationToken)
    {
        return _db.Queryable<MigrationRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TableKey == tableKey && x.Version == version)
            .FirstAsync(cancellationToken);
    }

    public Task AddAsync(MigrationRecord entity, CancellationToken cancellationToken)
    {
        return _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public Task UpdateAsync(MigrationRecord entity, CancellationToken cancellationToken)
    {
        return _db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }
}
