using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AiDatabaseFieldRepository : RepositoryBase<AiDatabaseField>
{
    public AiDatabaseFieldRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public Task<List<AiDatabaseField>> ListByDatabaseAsync(TenantId tenantId, long databaseId, CancellationToken cancellationToken)
    {
        return Db.Queryable<AiDatabaseField>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DatabaseId == databaseId)
            .OrderBy(x => x.IsSystemField, OrderByType.Desc)
            .OrderBy(x => x.SortOrder, OrderByType.Asc)
            .OrderBy(x => x.Id, OrderByType.Asc)
            .ToListAsync(cancellationToken);
    }

    public Task DeleteByDatabaseAsync(TenantId tenantId, long databaseId, CancellationToken cancellationToken)
    {
        return Db.Deleteable<AiDatabaseField>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DatabaseId == databaseId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task AddRangeAsync(IReadOnlyCollection<AiDatabaseField> fields, CancellationToken cancellationToken)
    {
        if (fields.Count == 0)
        {
            return Task.CompletedTask;
        }

        return Db.Insertable(fields.ToList()).ExecuteCommandAsync(cancellationToken);
    }
}
