using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AiDatabaseRecordRepository : RepositoryBase<AiDatabaseRecord>
{
    public AiDatabaseRecordRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<(List<AiDatabaseRecord> Items, long Total)> GetPagedByDatabaseAsync(
        TenantId tenantId,
        long databaseId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<AiDatabaseRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DatabaseId == databaseId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }

    public async Task<int> CountByDatabaseAsync(TenantId tenantId, long databaseId, CancellationToken cancellationToken)
    {
        return await Db.Queryable<AiDatabaseRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DatabaseId == databaseId)
            .CountAsync(cancellationToken);
    }

    public async Task<AiDatabaseRecord?> FindByDatabaseAndIdAsync(
        TenantId tenantId,
        long databaseId,
        long recordId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<AiDatabaseRecord>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value &&
                x.DatabaseId == databaseId &&
                x.Id == recordId)
            .FirstAsync(cancellationToken);
    }

    public Task DeleteByDatabaseAsync(TenantId tenantId, long databaseId, CancellationToken cancellationToken)
    {
        return Db.Deleteable<AiDatabaseRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DatabaseId == databaseId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task AddRangeAsync(IReadOnlyCollection<AiDatabaseRecord> records, CancellationToken cancellationToken)
    {
        if (records.Count == 0)
        {
            return Task.CompletedTask;
        }

        return Db.Insertable(records.ToList()).ExecuteCommandAsync(cancellationToken);
    }
}
