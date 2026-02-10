using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

/// <summary>
/// Base repository providing common CRUD operations for tenant-scoped entities.
/// </summary>
public abstract class RepositoryBase<TEntity> where TEntity : TenantEntity, new()
{
    protected readonly ISqlSugarClient Db;

    protected RepositoryBase(ISqlSugarClient db)
    {
        Db = db;
    }

    public virtual async Task<TEntity?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return await Db.Queryable<TEntity>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public virtual Task AddAsync(TEntity entity, CancellationToken cancellationToken)
    {
        return Db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public virtual Task UpdateAsync(TEntity entity, CancellationToken cancellationToken)
    {
        return Db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    public virtual Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return Db.Deleteable<TEntity>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .ExecuteCommandAsync(cancellationToken);
    }

    public virtual async Task<IReadOnlyList<TEntity>> QueryByIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<TEntity>();
        }

        var idArray = ids.Distinct().ToArray();
        return await Db.Queryable<TEntity>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(idArray, x.Id))
            .ToListAsync(cancellationToken);
    }
}
