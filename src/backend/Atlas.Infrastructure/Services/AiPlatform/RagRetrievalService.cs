using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class RagRetrievalService : IRagRetrievalService
{
    private readonly IRetriever _hybridRetriever;
    private readonly VectorRetrieverService _vectorRetriever;
    private readonly Bm25RetrieverService _bm25Retriever;
    private readonly IRagExperimentService _ragExperimentService;
    private readonly ILogger<RagRetrievalService> _logger;

    public RagRetrievalService(
        IRetriever hybridRetriever,
        VectorRetrieverService vectorRetriever,
        Bm25RetrieverService bm25Retriever,
        IRagExperimentService ragExperimentService,
        ILogger<RagRetrievalService> logger)
    {
        _hybridRetriever = hybridRetriever;
        _vectorRetriever = vectorRetriever;
        _bm25Retriever = bm25Retriever;
        _ragExperimentService = ragExperimentService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RagSearchResult>> SearchAsync(
        TenantId tenantId,
        IReadOnlyList<long> knowledgeBaseIds,
        string query,
        int topK = 5,
        CancellationToken ct = default)
    {
        if (knowledgeBaseIds.Count == 0 || string.IsNullOrWhiteSpace(query) || topK <= 0)
        {
            return [];
        }

        var normalizedQuery = query.Trim();
        var decision = await _ragExperimentService.ResolveDecisionAsync(tenantId, normalizedQuery, ct);
        var mainRetriever = ResolveRetriever(decision.PrimaryStrategy);
        var queryHash = BuildQueryHash(tenantId, normalizedQuery);

        var mainWatch = Stopwatch.StartNew();
        var mainResults = await mainRetriever.RetrieveAsync(
            tenantId,
            knowledgeBaseIds,
            normalizedQuery,
            topK,
            ct);
        mainWatch.Stop();

        var mainRunId = await _ragExperimentService.RecordRunAsync(
            tenantId,
            new RagExperimentRunCreateRequest(
                decision.ExperimentName,
                decision.Variant,
                decision.PrimaryStrategy,
                queryHash,
                topK,
                mainResults.Select(item => item.ChunkId).ToArray(),
                (int)mainWatch.ElapsedMilliseconds,
                IsShadow: false),
            ct);

        if (decision.ShadowEnabled
            && decision.ShadowStrategy.HasValue
            && decision.ShadowStrategy.Value != decision.PrimaryStrategy)
        {
            try
            {
                var shadowWatch = Stopwatch.StartNew();
                var shadowResults = await ResolveRetriever(decision.ShadowStrategy.Value).RetrieveAsync(
                    tenantId,
                    knowledgeBaseIds,
                    normalizedQuery,
                    topK,
                    ct);
                shadowWatch.Stop();

                var shadowRunId = await _ragExperimentService.RecordRunAsync(
                    tenantId,
                    new RagExperimentRunCreateRequest(
                        decision.ExperimentName,
                        "shadow",
                        decision.ShadowStrategy.Value,
                        queryHash,
                        topK,
                        shadowResults.Select(item => item.ChunkId).ToArray(),
                        (int)shadowWatch.ElapsedMilliseconds,
                        IsShadow: true),
                    ct);
                await _ragExperimentService.RecordShadowComparisonAsync(
                    tenantId,
                    mainRunId,
                    shadowRunId,
                    mainResults,
                    shadowResults,
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RAG shadow execution failed.");
            }
        }

        return mainResults;
    }

    private IRetriever ResolveRetriever(RagRetrieverStrategy strategy)
        => strategy switch
        {
            RagRetrieverStrategy.Vector => _vectorRetriever,
            RagRetrieverStrategy.Bm25 => _bm25Retriever,
            _ => _hybridRetriever
        };

    private static string BuildQueryHash(TenantId tenantId, string query)
    {
        var raw = $"{tenantId.Value}:{query}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }
}
