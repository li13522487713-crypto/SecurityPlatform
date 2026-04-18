using Atlas.Application.Identity.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Identity.Abstractions;

public interface IOrganizationMemberService
{
    Task<IReadOnlyList<OrganizationMemberDto>> ListAsync(TenantId tenantId, long organizationId, CancellationToken cancellationToken);

    Task AddAsync(TenantId tenantId, long organizationId, long actorUserId, OrganizationMemberAddRequest request, CancellationToken cancellationToken);

    Task UpdateAsync(TenantId tenantId, long organizationId, long targetUserId, OrganizationMemberUpdateRequest request, CancellationToken cancellationToken);

    Task RemoveAsync(TenantId tenantId, long organizationId, long targetUserId, CancellationToken cancellationToken);

    /// <summary>跨组织迁移 workspace；目标组织必须存在且与 source 不同。</summary>
    Task MoveWorkspaceAsync(TenantId tenantId, long workspaceId, long targetOrganizationId, long actorUserId, CancellationToken cancellationToken);
}
