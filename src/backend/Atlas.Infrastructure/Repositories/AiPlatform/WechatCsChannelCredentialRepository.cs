using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities.Channels;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories.AiPlatform;

public sealed class WechatCsChannelCredentialRepository : RepositoryBase<WechatCsChannelCredential>
{
    public WechatCsChannelCredentialRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<WechatCsChannelCredential?> FindByChannelAsync(
        TenantId tenantId,
        long channelId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<WechatCsChannelCredential>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ChannelId == channelId)
            .FirstAsync(cancellationToken);
    }
}
