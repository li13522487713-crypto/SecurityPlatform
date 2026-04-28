using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions.Knowledge;

/// <summary>图片知识库的项目与标注视图（v5 §37）。</summary>
public interface IKnowledgeImageItemService
{
    Task<PagedResult<KnowledgeImageItemDto>> ListAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);
}
