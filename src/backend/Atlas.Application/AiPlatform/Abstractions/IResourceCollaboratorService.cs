using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

/// <summary>
/// 治理 M-G03-C7（S7）：通用资源协作者服务。
/// 依据资源所在工作空间的成员表 + 工作空间角色构造 `Coze` 风格的「协作者」语义；
/// 资源级 ACL 覆写通过 <c>WorkspaceResourcePermission</c> 表表达。
/// </summary>
public interface IResourceCollaboratorService
{
    Task<IReadOnlyList<ResourceCollaboratorDto>> ListAsync(
        TenantId tenantId,
        long workspaceId,
        string resourceType,
        long resourceId,
        CancellationToken cancellationToken);

    Task AddAsync(
        TenantId tenantId,
        long workspaceId,
        string resourceType,
        long resourceId,
        long actorUserId,
        ResourceCollaboratorAddRequest request,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        TenantId tenantId,
        long workspaceId,
        string resourceType,
        long resourceId,
        long targetUserId,
        long actorUserId,
        ResourceCollaboratorUpdateRequest request,
        CancellationToken cancellationToken);

    Task RemoveAsync(
        TenantId tenantId,
        long workspaceId,
        string resourceType,
        long resourceId,
        long targetUserId,
        long actorUserId,
        CancellationToken cancellationToken);
}
