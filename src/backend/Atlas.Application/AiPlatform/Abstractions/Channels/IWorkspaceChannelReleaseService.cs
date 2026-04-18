using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions.Channels;

/// <summary>
/// 工作空间渠道发布与回滚服务（M-G02-C2 / 治理 §3 S1）。
///
/// - 「发布」：创建一条新的 <c>WorkspaceChannelRelease</c>，组装 <see cref="ChannelPublishContext"/>
///   交给 <see cref="IWorkspaceChannelConnector"/> 真实接通；如未注册对应 connector，
///   记录置 <c>failed</c> 并写 <c>connectorMessage</c>，绝不返回桩响应。
/// - 「回滚」：把指定历史发布提升为最新活动版本。底层逻辑等价于以历史
///   <c>configSnapshotJson</c> 再次发布一次，并把新记录的 <c>RolledBackFromReleaseId</c>
///   指向源记录；旧的 <c>active</c> 记录 → <c>rolled-back</c>。
/// - 「列表」：按 channelId 分页倒序返回。
/// </summary>
public interface IWorkspaceChannelReleaseService
{
    Task<PagedResult<WorkspaceChannelReleaseDto>> ListAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken);

    Task<WorkspaceChannelReleaseDto> GetAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        string releaseId,
        CancellationToken cancellationToken);

    Task<WorkspaceChannelReleaseDto> PublishAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        CurrentUserInfo currentUser,
        WorkspaceChannelReleaseCreateRequest request,
        CancellationToken cancellationToken);

    Task<WorkspaceChannelReleaseDto> RollbackAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        CurrentUserInfo currentUser,
        WorkspaceChannelReleaseRollbackRequest request,
        CancellationToken cancellationToken);
}
