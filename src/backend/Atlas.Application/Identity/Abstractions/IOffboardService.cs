using Atlas.Application.Identity.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Identity.Abstractions;

/// <summary>
/// 治理 M-G06-C3 / C4（S12）：离职资产移交 + 组织间成员迁移。
/// </summary>
public interface IOffboardService
{
    /// <summary>批量记录资产移交并把 FromUser 标记为 offboarded（如未已置）。</summary>
    Task<IReadOnlyList<ResourceOwnershipTransferDto>> ExecuteOffboardAsync(
        TenantId tenantId,
        long actorUserId,
        OffboardRequest request,
        CancellationToken cancellationToken);

    /// <summary>把组织成员从一个组织迁移到另一个组织（保留角色或重置为指定 role）。</summary>
    Task MoveMemberAcrossOrganizationsAsync(
        TenantId tenantId,
        long sourceOrganizationId,
        long targetUserId,
        long actorUserId,
        OrganizationMemberMoveRequest request,
        CancellationToken cancellationToken);
}
