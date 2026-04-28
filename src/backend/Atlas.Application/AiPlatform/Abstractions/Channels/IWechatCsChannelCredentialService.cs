using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions.Channels;

public interface IWechatCsChannelCredentialService
{
    Task<WechatCsChannelCredentialDto?> GetAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        CancellationToken cancellationToken);

    Task<WechatCsChannelCredentialDto> UpsertAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        WechatCsChannelCredentialUpsertRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        CancellationToken cancellationToken);
}
