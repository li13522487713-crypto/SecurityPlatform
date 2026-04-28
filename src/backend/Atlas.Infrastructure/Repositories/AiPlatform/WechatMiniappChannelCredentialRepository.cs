using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities.Channels;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories.AiPlatform;

public sealed class WechatMiniappChannelCredentialRepository : RepositoryBase<WechatMiniappChannelCredential>
{
    public WechatMiniappChannelCredentialRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<WechatMiniappChannelCredential?> FindByChannelAsync(
        TenantId tenantId,
        long channelId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<WechatMiniappChannelCredential>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ChannelId == channelId)
            .FirstAsync(cancellationToken);
    }
}
