using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities.Channels;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories.AiPlatform;

public sealed class FeishuChannelCredentialRepository : RepositoryBase<FeishuChannelCredential>
{
    public FeishuChannelCredentialRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<FeishuChannelCredential?> FindByChannelAsync(
        TenantId tenantId,
        long channelId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<FeishuChannelCredential>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ChannelId == channelId)
            .FirstAsync(cancellationToken);
    }

    public async Task<FeishuChannelCredential?> FindByAppIdAsync(
        TenantId tenantId,
        string appId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<FeishuChannelCredential>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .FirstAsync(cancellationToken);
    }
}
