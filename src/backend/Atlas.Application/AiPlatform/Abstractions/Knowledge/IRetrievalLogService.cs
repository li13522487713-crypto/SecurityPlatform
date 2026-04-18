using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions.Knowledge;

/// <summary>
/// 检索日志：v5 §38 报告强调"召回透明度"必须固化 raw / rewritten / candidates / reranked / finalContext。
/// </summary>
public interface IRetrievalLogService
{
    Task<PagedResult<RetrievalLogDto>> ListAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        RetrievalLogQuery query,
        CancellationToken cancellationToken);

    Task<RetrievalLogDto?> GetAsync(
        TenantId tenantId,
        string traceId,
        CancellationToken cancellationToken);

    Task AppendAsync(
        TenantId tenantId,
        RetrievalLogDto log,
        CancellationToken cancellationToken);
}
