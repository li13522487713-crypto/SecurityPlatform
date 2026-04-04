using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IChunkService
{
    Task<PagedResult<DocumentChunkDto>> GetByDocumentAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<long> CreateAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        ChunkCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long chunkId,
        ChunkUpdateRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long chunkId,
        CancellationToken cancellationToken);
}
