using Atlas.Application.Coze.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Coze.Abstractions;

public interface IWorkspaceFolderService
{
    Task<PagedResult<WorkspaceFolderListItem>> ListAsync(
        TenantId tenantId,
        string workspaceId,
        string? keyword,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken);

    Task<string> CreateAsync(
        TenantId tenantId,
        string workspaceId,
        CurrentUserInfo currentUser,
        WorkspaceFolderCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        TenantId tenantId,
        string workspaceId,
        string folderId,
        WorkspaceFolderUpdateRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        string workspaceId,
        string folderId,
        CancellationToken cancellationToken);

    Task MoveItemAsync(
        TenantId tenantId,
        string workspaceId,
        string folderId,
        WorkspaceFolderItemMoveRequest request,
        CancellationToken cancellationToken);
}

public interface IWorkspacePublishChannelService
{
    Task<PagedResult<WorkspacePublishChannelDto>> ListAsync(
        TenantId tenantId,
        string workspaceId,
        string? keyword,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken);

    Task<string> CreateAsync(
        TenantId tenantId,
        string workspaceId,
        WorkspacePublishChannelCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        WorkspacePublishChannelUpdateRequest request,
        CancellationToken cancellationToken);

    Task ReauthorizeAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        CancellationToken cancellationToken);
}
