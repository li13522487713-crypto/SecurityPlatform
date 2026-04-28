using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions.Channels;

public interface IWechatMiniappChannelCredentialService
{
    Task<WechatMiniappChannelCredentialDto?> GetAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        CancellationToken cancellationToken);

    Task<WechatMiniappChannelCredentialDto> UpsertAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        WechatMiniappChannelCredentialUpsertRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        CancellationToken cancellationToken);
}
