using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class CrossEncoderRerankerAdapter : IReranker
{
    private readonly CrossEncoderRerankerService _crossEncoderRerankerService;

    public CrossEncoderRerankerAdapter(CrossEncoderRerankerService crossEncoderRerankerService)
    {
        _crossEncoderRerankerService = crossEncoderRerankerService;
    }

    public Task<IReadOnlyList<RagSearchResult>> RerankAsync(
        string query,
        IReadOnlyList<RagSearchResult> candidates,
        int topK = 8,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        IReadOnlyList<RagSearchResult> reranked = _crossEncoderRerankerService.Rerank(query, candidates, topK);
        return Task.FromResult(reranked);
    }
}
