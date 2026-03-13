using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AiDatabaseImportTaskRepository : RepositoryBase<AiDatabaseImportTask>
{
    public AiDatabaseImportTaskRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<AiDatabaseImportTask?> GetLatestAsync(
        TenantId tenantId,
        long databaseId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<AiDatabaseImportTask>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DatabaseId == databaseId)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .FirstAsync(cancellationToken);
    }

    public Task DeleteByDatabaseAsync(TenantId tenantId, long databaseId, CancellationToken cancellationToken)
    {
        return Db.Deleteable<AiDatabaseImportTask>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DatabaseId == databaseId)
            .ExecuteCommandAsync(cancellationToken);
    }
}
