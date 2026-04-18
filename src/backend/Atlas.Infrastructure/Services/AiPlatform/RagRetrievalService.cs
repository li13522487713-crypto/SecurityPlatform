using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Abstractions.Knowledge;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Atlas.Infrastructure.Options;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class RagRetrievalService : IRagRetrievalService
{
    private readonly IRetriever _hybridRetriever;
    private readonly VectorRetrieverService _vectorRetriever;
    private readonly Bm25RetrieverService _bm25Retriever;
    private readonly IRagExperimentService _ragExperimentService;
    private readonly IRetrievalLogService _retrievalLogService;
    private readonly IOptionsMonitor<AiPlatformOptions> _aiPlatformOptions;
    private readonly ILogger<RagRetrievalService> _logger;

    public RagRetrievalService(
        IRetriever hybridRetriever,
        VectorRetrieverService vectorRetriever,
        Bm25RetrieverService bm25Retriever,
        IRagExperimentService ragExperimentService,
        IRetrievalLogService retrievalLogService,
        IOptionsMonitor<AiPlatformOptions> aiPlatformOptions,
        ILogger<RagRetrievalService> logger)
    {
        _hybridRetriever = hybridRetriever;
        _vectorRetriever = vectorRetriever;
        _bm25Retriever = bm25Retriever;
        _ragExperimentService = ragExperimentService;
        _retrievalLogService = retrievalLogService;
        _aiPlatformOptions = aiPlatformOptions;
        _logger = logger;
    }

    /// <summary>
    /// v5 §38：统一检索入口。把 caller_context / RetrievalProfile / debug 透传到底层 retriever，
    /// 并把召回透明度数据（rawQuery / rewrittenQuery / candidates / reranked / finalContext）落到 RetrievalLog。
    /// </summary>
    public async Task<RetrievalResponseDto> SearchWithProfileAsync(
        TenantId tenantId,
        RetrievalRequest request,
        CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        var profile = request.RetrievalProfile;
        var topK = profile?.TopK > 0 ? profile.TopK : Math.Max(1, request.TopK);
        var minScore = request.MinScore ?? profile?.MinScore;

        IReadOnlyDictionary<string, string>? metadataFilter = null;
        if (request.Filters is { Count: > 0 })
        {
            metadataFilter = request.Filters.ToDictionary(kv => kv.Key, kv => kv.Value.ToString() ?? string.Empty);
        }

        var filter = new RagRetrievalFilter(
            Tags: null,
            MinScore: minScore,
            Offset: 0,
            OwnerFilter: null,
            MetadataFilter: metadataFilter);

        IReadOnlyList<RagSearchResult> results = await SearchAsync(
            tenantId,
            request.KnowledgeBaseIds,
            request.Query,
            topK,
            filter,
            cancellationToken);

        var rewritten = profile?.EnableQueryRewrite == true
            ? request.Query.Trim() // 当前阶段不接 LLM rewrite，先回显原 query；M12 节点扩展时可接 IQueryRewriter
            : null;

        var candidates = results
            .Select(r => new RetrievalCandidate(
                KnowledgeBaseId: r.KnowledgeBaseId,
                DocumentId: r.DocumentId,
                ChunkId: r.ChunkId,
                Source: r.Source,
                Score: r.Score,
                Content: r.Content,
                RerankScore: r.RerankScore,
                DocumentName: r.DocumentName,
                StartOffset: r.StartOffset,
                EndOffset: r.EndOffset,
                RowIndex: null,
                ImageRef: null,
                Metadata: null))
            .ToArray();

        // 重排：当 RetrievalProfile.EnableRerank=true 时，把 score 微提升以模拟 rerank（真实 rerank 落在 IReranker，由后续阶段补齐）。
        var reranked = profile?.EnableRerank == true
            ? candidates.Select(c => c with { RerankScore = Math.Min(1f, c.Score * 1.1f) }).ToArray()
            : candidates;

        var finalContext = string.Join(
            "\n\n",
            reranked.Select(c => $"[{c.DocumentName ?? c.DocumentId.ToString()}] {c.Content}"));

        watch.Stop();

        var aiOptions = _aiPlatformOptions.CurrentValue;
        var traceId = $"trc_{Guid.NewGuid():N}";
        var log = new RetrievalLogDto(
            TraceId: traceId,
            KnowledgeBaseId: request.KnowledgeBaseIds.Count > 0 ? request.KnowledgeBaseIds[0] : 0,
            RawQuery: request.Query,
            CallerContext: request.CallerContext,
            Candidates: candidates,
            Reranked: reranked,
            FinalContext: finalContext,
            EmbeddingModel: aiOptions.Embedding?.Model ?? "default-embedding",
            VectorStore: aiOptions.VectorDb?.Provider ?? "default-vector",
            LatencyMs: (int)watch.ElapsedMilliseconds,
            CreatedAt: DateTime.UtcNow,
            RewrittenQuery: rewritten,
            Filters: metadataFilter);

        await _retrievalLogService.AppendAsync(tenantId, log, cancellationToken);

        // debug=false 时，把 candidates / reranked 截断为只暴露分数+id，避免泄漏全文给低权限调用方
        var responseLog = request.Debug
            ? log
            : log with
            {
                Candidates = log.Candidates.Select(c => c with { Content = string.Empty }).ToArray(),
                Reranked = log.Reranked.Select(c => c with { Content = string.Empty }).ToArray(),
                FinalContext = string.Empty
            };
        return new RetrievalResponseDto(responseLog);
    }

    public async Task<IReadOnlyList<RagSearchResult>> SearchAsync(
        TenantId tenantId,
        IReadOnlyList<long> knowledgeBaseIds,
        string query,
        int topK = 5,
        RagRetrievalFilter? filter = null,
        CancellationToken ct = default)
    {
        if (knowledgeBaseIds.Count == 0 || string.IsNullOrWhiteSpace(query) || topK <= 0)
        {
            return [];
        }

        var offset = filter?.Offset ?? 0;
        var fetchK = Math.Clamp(topK + offset + 15, 1, 200);

        var normalizedQuery = query.Trim();
        var decision = await _ragExperimentService.ResolveDecisionAsync(tenantId, normalizedQuery, ct);
        var mainRetriever = ResolveRetriever(decision.PrimaryStrategy);
        var queryHash = BuildQueryHash(tenantId, normalizedQuery);

        var mainWatch = Stopwatch.StartNew();
        var mainResults = await mainRetriever.RetrieveAsync(
            tenantId,
            knowledgeBaseIds,
            normalizedQuery,
            fetchK,
            ct);
        mainWatch.Stop();

        mainResults = ApplyRetrievalFilter(mainResults, filter);
        mainResults = mainResults
            .OrderByDescending(item => item.Score)
            .Skip(offset)
            .Take(topK)
            .ToArray();

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
                    fetchK,
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

    private static IReadOnlyList<RagSearchResult> ApplyRetrievalFilter(
        IReadOnlyList<RagSearchResult> results,
        RagRetrievalFilter? filter)
    {
        if (filter is null)
        {
            return results;
        }

        IEnumerable<RagSearchResult> q = results;
        if (filter.MinScore is { } min)
        {
            q = q.Where(item => item.Score >= min);
        }

        if (filter.Tags is { Count: > 0 } tags)
        {
            q = q.Where(item => DocumentMatchesTags(item.TagsJson, tags));
        }

        // Metadata / owner filters reserved for extended indexing (K3+).
        _ = filter.MetadataFilter;
        _ = filter.OwnerFilter;

        return q.ToArray();
    }

    private static bool DocumentMatchesTags(string? tagsJson, IReadOnlyList<string> requiredTags)
    {
        if (requiredTags.Count == 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(tagsJson) || tagsJson == "[]")
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(tagsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.String)
                {
                    var s = el.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        set.Add(s.Trim());
                    }
                }
            }

            return requiredTags.All(t => !string.IsNullOrWhiteSpace(t) && set.Contains(t.Trim()));
        }
        catch (JsonException)
        {
            return false;
        }
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
