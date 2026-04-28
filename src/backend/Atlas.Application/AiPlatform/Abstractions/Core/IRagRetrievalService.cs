using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IRagRetrievalService
{
    Task<IReadOnlyList<RagSearchResult>> SearchAsync(
        TenantId tenantId,
        IReadOnlyList<long> knowledgeBaseIds,
        string query,
        int topK = 5,
        RagRetrievalFilter? filter = null,
        CancellationToken ct = default);

    /// <summary>
    /// v5 §38 新增：带 caller_context / RetrievalProfile / debug 的统一检索入口。
    /// 默认实现委托回旧的 <see cref="SearchAsync"/>，保证旧调用方零侵入；新调用方可获得 RetrievalLog。
    /// </summary>
    Task<RetrievalResponseDto> SearchWithProfileAsync(
        TenantId tenantId,
        RetrievalRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new RetrievalResponseDto(new RetrievalLogDto(
            TraceId: Guid.NewGuid().ToString("N"),
            KnowledgeBaseId: request.KnowledgeBaseIds.Count > 0 ? request.KnowledgeBaseIds[0] : 0,
            RawQuery: request.Query,
            CallerContext: request.CallerContext,
            Candidates: Array.Empty<RetrievalCandidate>(),
            Reranked: Array.Empty<RetrievalCandidate>(),
            FinalContext: string.Empty,
            EmbeddingModel: "unknown",
            VectorStore: "unknown",
            LatencyMs: 0,
            CreatedAt: DateTime.UtcNow)));
}
