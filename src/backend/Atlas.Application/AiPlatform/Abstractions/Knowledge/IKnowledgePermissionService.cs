using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions.Knowledge;

/// <summary>
/// 知识库四层权限模型（v5 §39）：space / project / kb / document × user|role|group × actions[]。
/// </summary>
public interface IKnowledgePermissionService
{
    Task<PagedResult<KnowledgePermissionDto>> ListAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<long> GrantAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        KnowledgePermissionGrantRequest request,
        string grantedByUserId,
        CancellationToken cancellationToken);

    Task RevokeAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long permissionId,
        CancellationToken cancellationToken);
}
