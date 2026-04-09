using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class HybridRagRetrieverService : IRetriever
{
    private readonly VectorRetrieverService _vectorRetrieverService;
    private readonly Bm25RetrieverService _bm25RetrieverService;
    private readonly HybridRetrievalService _hybridRetrievalService;
    private readonly IReranker _reranker;
    private readonly FreshnessBoostService _freshnessBoostService;
    private readonly ContextCompressionService _contextCompressionService;
    private readonly IOptionsMonitor<AiPlatformOptions> _optionsMonitor;

    public HybridRagRetrieverService(
        VectorRetrieverService vectorRetrieverService,
        Bm25RetrieverService bm25RetrieverService,
        HybridRetrievalService hybridRetrievalService,
        IReranker reranker,
        FreshnessBoostService freshnessBoostService,
        ContextCompressionService contextCompressionService,
        IOptionsMonitor<AiPlatformOptions> optionsMonitor)
    {
        _vectorRetrieverService = vectorRetrieverService;
        _bm25RetrieverService = bm25RetrieverService;
        _hybridRetrievalService = hybridRetrievalService;
        _reranker = reranker;
        _freshnessBoostService = freshnessBoostService;
        _contextCompressionService = contextCompressionService;
        _optionsMonitor = optionsMonitor;
    }

    public async Task<IReadOnlyList<RagSearchResult>> RetrieveAsync(
        TenantId tenantId,
        IReadOnlyList<long> knowledgeBaseIds,
        string query,
        int topK = 8,
        CancellationToken cancellationToken = default)
    {
        if (knowledgeBaseIds.Count == 0 || string.IsNullOrWhiteSpace(query) || topK <= 0)
        {
            return [];
        }

        var options = _optionsMonitor.CurrentValue;
        var vectorTopK = Math.Max(topK, options.Retrieval.VectorTopK);
        var bm25TopK = Math.Max(topK, options.Retrieval.Bm25TopK);

        var vectorResults = await _vectorRetrieverService.RetrieveAsync(
            tenantId,
            knowledgeBaseIds,
            query,
            vectorTopK,
            cancellationToken);
        var bm25Results = await _bm25RetrieverService.RetrieveAsync(
            tenantId,
            knowledgeBaseIds,
            query,
            bm25TopK,
            cancellationToken);

        IReadOnlyList<RagSearchResult> merged = options.Retrieval.EnableHybrid
            ? _hybridRetrievalService.MergeAndRerank(
                query,
                vectorResults,
                bm25Results,
                Math.Max(topK, options.Retrieval.CrossEncoderTopK))
            : vectorResults
                .OrderByDescending(item => item.Score)
                .Take(Math.Max(topK, options.Retrieval.CrossEncoderTopK))
                .ToArray();

        if (merged.Count == 0)
        {
            return [];
        }

        if (options.Retrieval.EnableCrossEncoderRerank)
        {
            merged = await _reranker.RerankAsync(
                query,
                merged,
                Math.Max(topK, options.Retrieval.CrossEncoderTopK),
                cancellationToken);
        }

        if (options.Retrieval.EnableFreshnessBoost)
        {
            merged = _freshnessBoostService.Apply(merged, options.Retrieval.FreshnessHalfLifeDays);
        }

        if (options.Retrieval.EnableContextCompression)
        {
            merged = _contextCompressionService.Compress(query, merged, options.Retrieval.ContextMaxChars);
        }

        return merged.Take(topK).ToArray();
    }
}
