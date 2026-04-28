using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IKnowledgeBaseService
{
    Task<PagedResult<KnowledgeBaseDto>> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        long? workspaceId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<KnowledgeBaseDto?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<long> CreateAsync(
        TenantId tenantId,
        KnowledgeBaseCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        TenantId tenantId,
        long id,
        KnowledgeBaseUpdateRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);
}
