using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Application.Platform.Models;

namespace Atlas.Application.Platform.Abstractions;

public interface IWorkspacePortalService
{
    Task<IReadOnlyList<WorkspaceListItem>> ListWorkspacesAsync(
        TenantId tenantId,
        long userId,
        bool isPlatformAdmin,
        CancellationToken cancellationToken = default);

    Task<WorkspaceDetailDto?> GetWorkspaceAsync(
        TenantId tenantId,
        long workspaceId,
        long userId,
        bool isPlatformAdmin,
        CancellationToken cancellationToken = default);

    Task<WorkspaceDetailDto?> GetWorkspaceByAppKeyAsync(
        TenantId tenantId,
        string appKey,
        long userId,
        bool isPlatformAdmin,
        CancellationToken cancellationToken = default);

    Task<long> CreateWorkspaceAsync(
        TenantId tenantId,
        long userId,
        WorkspaceCreateRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateWorkspaceAsync(
        TenantId tenantId,
        long workspaceId,
        long userId,
        bool isPlatformAdmin,
        WorkspaceUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteWorkspaceAsync(
        TenantId tenantId,
        long workspaceId,
        long userId,
        bool isPlatformAdmin,
        CancellationToken cancellationToken = default);

    Task<PagedResult<WorkspaceAppCardDto>> GetDevelopAppsAsync(
        TenantId tenantId,
        long workspaceId,
        long userId,
        bool isPlatformAdmin,
        PagedRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkspaceAppCreateResult> CreateDevelopAppAsync(
        TenantId tenantId,
        long workspaceId,
        long userId,
        bool isPlatformAdmin,
        WorkspaceAppCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<WorkspaceResourceCardDto>> GetResourcesAsync(
        TenantId tenantId,
        long workspaceId,
        long userId,
        bool isPlatformAdmin,
        string? resourceType,
        PagedRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkspaceMemberDto>> GetMembersAsync(
        TenantId tenantId,
        long workspaceId,
        long userId,
        bool isPlatformAdmin,
        CancellationToken cancellationToken = default);

    Task AddMemberAsync(
        TenantId tenantId,
        long workspaceId,
        long operatorUserId,
        bool isPlatformAdmin,
        WorkspaceMemberCreateRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateMemberRoleAsync(
        TenantId tenantId,
        long workspaceId,
        long targetUserId,
        long operatorUserId,
        bool isPlatformAdmin,
        WorkspaceMemberRoleUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task RemoveMemberAsync(
        TenantId tenantId,
        long workspaceId,
        long targetUserId,
        long operatorUserId,
        bool isPlatformAdmin,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkspaceRolePermissionDto>> GetResourcePermissionsAsync(
        TenantId tenantId,
        long workspaceId,
        long userId,
        bool isPlatformAdmin,
        string resourceType,
        long resourceId,
        CancellationToken cancellationToken = default);

    Task UpdateResourcePermissionsAsync(
        TenantId tenantId,
        long workspaceId,
        long userId,
        bool isPlatformAdmin,
        string resourceType,
        long resourceId,
        WorkspaceResourcePermissionUpdateRequest request,
        CancellationToken cancellationToken = default);
}
