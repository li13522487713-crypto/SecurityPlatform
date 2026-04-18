using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions.Knowledge;

/// <summary>
/// 知识库版本治理（v5 §40）：snapshot / release / rollback / diff。
/// 回退仅恢复 Schema 元信息，已上传文件保持不变。
/// </summary>
public interface IKnowledgeVersionService
{
    Task<PagedResult<KnowledgeVersionDto>> ListAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<long> CreateSnapshotAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        KnowledgeVersionCreateRequest request,
        string createdByUserId,
        CancellationToken cancellationToken);

    Task ReleaseAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long versionId,
        CancellationToken cancellationToken);

    Task RollbackAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long versionId,
        CancellationToken cancellationToken);

    Task<KnowledgeVersionDiffDto> DiffAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long fromVersionId,
        long toVersionId,
        CancellationToken cancellationToken);
}
