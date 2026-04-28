using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities.Channels;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories.AiPlatform;

public sealed class WechatMpChannelCredentialRepository : RepositoryBase<WechatMpChannelCredential>
{
    public WechatMpChannelCredentialRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<WechatMpChannelCredential?> FindByChannelAsync(
        TenantId tenantId,
        long channelId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<WechatMpChannelCredential>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ChannelId == channelId)
            .FirstAsync(cancellationToken);
    }
}
