using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IDocumentService
{
    Task<long> CreateAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        DocumentCreateRequest request,
        CancellationToken cancellationToken);

    Task<PagedResult<KnowledgeDocumentDto>> ListByKnowledgeBaseAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        CancellationToken cancellationToken);

    Task<DocumentProgressDto> GetProgressAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        CancellationToken cancellationToken);

    Task ResegmentAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        DocumentResegmentRequest request,
        CancellationToken cancellationToken);
}
