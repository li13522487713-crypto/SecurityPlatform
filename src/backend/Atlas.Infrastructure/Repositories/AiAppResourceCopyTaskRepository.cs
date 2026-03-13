using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AiAppResourceCopyTaskRepository : RepositoryBase<AiAppResourceCopyTask>
{
    public AiAppResourceCopyTaskRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<AiAppResourceCopyTask?> GetLatestAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<AiAppResourceCopyTask>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .FirstAsync(cancellationToken);
    }

    public Task DeleteByAppIdAsync(TenantId tenantId, long appId, CancellationToken cancellationToken)
    {
        return Db.Deleteable<AiAppResourceCopyTask>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .ExecuteCommandAsync(cancellationToken);
    }
}
