using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class SchemaPublishSnapshotRepository : ISchemaPublishSnapshotRepository
{
    private readonly ISqlSugarClient _db;

    public SchemaPublishSnapshotRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<SchemaPublishSnapshot?> FindByIdAsync(
        TenantId tenantId,
        long snapshotId,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<SchemaPublishSnapshot>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == snapshotId)
            .FirstAsync(cancellationToken);
    }

    public async Task<SchemaPublishSnapshot?> FindLatestByTableAsync(
        TenantId tenantId,
        string tableKey,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<SchemaPublishSnapshot>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TableKey == tableKey)
            .OrderByDescending(x => x.Version)
            .FirstAsync(cancellationToken);
    }

    public async Task<SchemaPublishSnapshot?> FindByVersionAsync(
        TenantId tenantId,
        string tableKey,
        int version,
        CancellationToken cancellationToken)
    {
        return await _db.Queryable<SchemaPublishSnapshot>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.TableKey == tableKey
                && x.Version == version)
            .FirstAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<SchemaPublishSnapshot> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        string? tableKey,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<SchemaPublishSnapshot>()
            .Where(x => x.TenantIdValue == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(tableKey))
        {
            query = query.Where(x => x.TableKey == tableKey);
        }

        var totalRef = new RefAsync<int>();
        var items = await query
            .OrderByDescending(x => x.PublishedAt)
            .ToPageListAsync(pageIndex, pageSize, totalRef, cancellationToken);

        return (items, totalRef.Value);
    }

    public Task AddAsync(SchemaPublishSnapshot snapshot, CancellationToken cancellationToken)
    {
        return _db.Insertable(snapshot).ExecuteCommandAsync(cancellationToken);
    }
}
