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

    public Task<IReadOnlyList<RagSearchResult>> RetrieveAsync(
        TenantId tenantId,
        IReadOnlyList<long> knowledgeBaseIds,
        string query,
        int topK = 8,
        CancellationToken cancellationToken = default)
        => RetrieveWithProfileAsync(tenantId, knowledgeBaseIds, query, topK, retrievalProfile: null, cancellationToken);

    /// <summary>
    /// v5 §38 / 计划 G4：profile 级别的混合检索。
    /// - <see cref="RetrievalProfile.EnableHybrid"/> 决定是否合并 BM25 和 Vector；
    /// - <see cref="RetrievalProfile.Weights"/> 注入到加权 RRF；
    /// - 上层 <see cref="RagRetrievalService"/> 根据 profile.EnableRerank 决定是否再走 IReranker。
    /// </summary>
    public async Task<IReadOnlyList<RagSearchResult>> RetrieveWithProfileAsync(
        TenantId tenantId,
        IReadOnlyList<long> knowledgeBaseIds,
        string query,
        int topK,
        RetrievalProfile? retrievalProfile,
        CancellationToken cancellationToken = default)
    {
        if (knowledgeBaseIds.Count == 0 || string.IsNullOrWhiteSpace(query) || topK <= 0)
        {
            return [];
        }

        var options = _optionsMonitor.CurrentValue;
        var vectorTopK = Math.Max(topK, options.Retrieval.VectorTopK);
        var bm25TopK = Math.Max(topK, options.Retrieval.Bm25TopK);

        // profile.EnableHybrid 优先级 > options.Retrieval.EnableHybrid（请求级覆盖配置级）
        var enableHybrid = retrievalProfile?.EnableHybrid ?? options.Retrieval.EnableHybrid;
        var weights = retrievalProfile?.Weights;

        var vectorWeight = (double)(weights?.Vector ?? 1f);
        var bm25Weight = (double)(weights?.Bm25 ?? 1f);

        // 当 hybrid 关闭时只跑 vector；否则双链
        var vectorResults = vectorWeight > 0d
            ? await _vectorRetrieverService.RetrieveAsync(tenantId, knowledgeBaseIds, query, vectorTopK, cancellationToken)
            : (IReadOnlyList<RagSearchResult>)Array.Empty<RagSearchResult>();
        var bm25Results = enableHybrid && bm25Weight > 0d
            ? await _bm25RetrieverService.RetrieveAsync(tenantId, knowledgeBaseIds, query, bm25TopK, cancellationToken)
            : (IReadOnlyList<RagSearchResult>)Array.Empty<RagSearchResult>();

        IReadOnlyList<RagSearchResult> merged = enableHybrid
            ? _hybridRetrievalService.MergeAndRerankWithWeights(
                query,
                vectorResults,
                bm25Results,
                Math.Max(topK, options.Retrieval.CrossEncoderTopK),
                vectorWeight,
                bm25Weight)
            : vectorResults
                .OrderByDescending(item => item.Score)
                .Take(Math.Max(topK, options.Retrieval.CrossEncoderTopK))
                .ToArray();

        if (merged.Count == 0)
        {
            return [];
        }

        // cross-encoder rerank：当 profile.EnableRerank=true 或 options 默认开启时
        var enableCrossRerank = retrievalProfile?.EnableRerank ?? options.Retrieval.EnableCrossEncoderRerank;
        if (enableCrossRerank)
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
