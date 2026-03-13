using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AiAppPublishRecordRepository : RepositoryBase<AiAppPublishRecord>
{
    public AiAppPublishRecordRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<AiAppPublishRecord>> GetByAppIdAsync(
        TenantId tenantId,
        long appId,
        int top,
        CancellationToken cancellationToken)
    {
        var query = Db.Queryable<AiAppPublishRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc);

        if (top > 0)
        {
            query = query.Take(top);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public Task DeleteByAppIdAsync(TenantId tenantId, long appId, CancellationToken cancellationToken)
    {
        return Db.Deleteable<AiAppPublishRecord>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .ExecuteCommandAsync(cancellationToken);
    }
}
