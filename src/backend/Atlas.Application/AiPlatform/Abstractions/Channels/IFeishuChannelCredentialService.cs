using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions.Channels;

/// <summary>
/// 治理 M-G02-C8：飞书凭据 CRUD 服务。
/// </summary>
public interface IFeishuChannelCredentialService
{
    Task<FeishuChannelCredentialDto?> GetAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        CancellationToken cancellationToken);

    Task<FeishuChannelCredentialDto> UpsertAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        FeishuChannelCredentialUpsertRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        CancellationToken cancellationToken);
}
