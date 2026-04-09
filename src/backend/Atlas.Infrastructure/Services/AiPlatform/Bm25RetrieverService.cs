using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class Bm25RetrieverService : IRetriever
{
    private readonly BM25RetrievalService _bm25RetrievalService;

    public Bm25RetrieverService(BM25RetrievalService bm25RetrievalService)
    {
        _bm25RetrievalService = bm25RetrievalService;
    }

    public Task<IReadOnlyList<RagSearchResult>> RetrieveAsync(
        TenantId tenantId,
        IReadOnlyList<long> knowledgeBaseIds,
        string query,
        int topK = 8,
        CancellationToken cancellationToken = default)
    {
        return _bm25RetrievalService.SearchAsync(
            tenantId,
            knowledgeBaseIds,
            query,
            topK,
            cancellationToken);
    }
}
