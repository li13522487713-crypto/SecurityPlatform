using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions.Knowledge;

/// <summary>表格知识库的列定义与行视图（v5 §37）。</summary>
public interface IKnowledgeTableViewService
{
    Task<IReadOnlyList<KnowledgeTableColumnDto>> ListColumnsAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        CancellationToken cancellationToken);

    Task<PagedResult<KnowledgeTableRowDto>> ListRowsAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);
}
