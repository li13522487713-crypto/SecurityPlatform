using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Abstractions.Knowledge;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Repositories;
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
    private readonly IQueryRewriter _queryRewriter;
    private readonly IReranker _reranker;
    private readonly DocumentChunkRepository _chunkRepository;
    private readonly IOptionsMonitor<AiPlatformOptions> _aiPlatformOptions;
    private readonly ILogger<RagRetrievalService> _logger;

    public RagRetrievalService(
        IRetriever hybridRetriever,
        VectorRetrieverService vectorRetriever,
        Bm25RetrieverService bm25Retriever,
        IRagExperimentService ragExperimentService,
        IRetrievalLogService retrievalLogService,
        IQueryRewriter queryRewriter,
        IReranker reranker,
        DocumentChunkRepository chunkRepository,
        IOptionsMonitor<AiPlatformOptions> aiPlatformOptions,
        ILogger<RagRetrievalService> logger)
    {
        _hybridRetriever = hybridRetriever;
        _vectorRetriever = vectorRetriever;
        _bm25Retriever = bm25Retriever;
        _ragExperimentService = ragExperimentService;
        _retrievalLogService = retrievalLogService;
        _queryRewriter = queryRewriter;
        _reranker = reranker;
        _chunkRepository = chunkRepository;
        _aiPlatformOptions = aiPlatformOptions;
        _logger = logger;
    }

    /// <summary>
    /// v5 §38 / 计划 G4：统一检索入口（深度版）。
    /// - profile.EnableQueryRewrite=true → 调用 <see cref="IQueryRewriter.RewriteAsync"/>（无 provider 时降级到原 query）
    /// - profile.Weights / EnableHybrid → 透传到 <see cref="HybridRagRetrieverService.RetrieveWithProfileAsync"/>
    /// - profile.EnableRerank（或顶层 Rerank）→ 真实 <see cref="IReranker"/> 重排
    /// - request.Filters → MetadataFilter 强制过滤
    /// - debug=true → 候选 metadata 用 chunk 的 RowIndex / ColumnHeadersJson 等填充
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

        // ============ 1) 真查询改写（IQueryRewriter，无 provider 降级） ============
        string? rewritten = null;
        var queryToUse = request.Query.Trim();
        if (profile?.EnableQueryRewrite == true)
        {
            try
            {
                var variants = await _queryRewriter.RewriteAsync(tenantId, queryToUse, maxQueries: 1, cancellationToken);
                rewritten = variants.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(rewritten) && !string.Equals(rewritten, queryToUse, StringComparison.Ordinal))
                {
                    queryToUse = rewritten!;
                }
                else if (string.IsNullOrWhiteSpace(rewritten))
                {
                    rewritten = queryToUse; // 降级：若 rewrite 返回空，回显原 query 标识"启用了改写但无实际改动"
                    _logger.LogDebug("Query rewrite returned empty result; falling back to original query.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Query rewrite failed; falling back to original query.");
                rewritten = queryToUse;
            }
        }

        // ============ 2) 检索：profile-aware 混合（含 weights/EnableHybrid） ============
        IReadOnlyList<RagSearchResult> results;
        if (_hybridRetriever is HybridRagRetrieverService hybridProfileAware)
        {
            results = await hybridProfileAware.RetrieveWithProfileAsync(
                tenantId,
                request.KnowledgeBaseIds,
                queryToUse,
                topK + 15,
                profile,
                cancellationToken);
        }
        else
        {
            results = await _hybridRetriever.RetrieveAsync(
                tenantId,
                request.KnowledgeBaseIds,
                queryToUse,
                topK + 15,
                cancellationToken);
        }

        // 强制 MetadataFilter 与 MinScore
        var filter = new RagRetrievalFilter(
            Tags: null,
            MinScore: minScore,
            Offset: 0,
            OwnerFilter: null,
            MetadataFilter: metadataFilter);
        results = ApplyRetrievalFilter(results, filter);
        results = results.Take(topK).ToArray();

        // ============ 3) 真重排：IReranker（顶层 Rerank 优先于 profile.EnableRerank） ============
        var enableRerank = request.Rerank ?? profile?.EnableRerank ?? false;
        IReadOnlyList<RagSearchResult> rerankedResults = results;
        if (enableRerank && results.Count > 0)
        {
            try
            {
                rerankedResults = await _reranker.RerankAsync(queryToUse, results, topK, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Reranker failed; using original ranking.");
                rerankedResults = results;
            }
        }

        // ============ 4) 候选 → DTO（debug=true 时填 metadata） ============
        IReadOnlyDictionary<long, IReadOnlyDictionary<string, string>>? metadataByChunkId = null;
        if (request.Debug)
        {
            metadataByChunkId = await BuildChunkMetadataAsync(tenantId, results.Concat(rerankedResults).Select(r => r.ChunkId).Distinct().ToList(), cancellationToken);
        }

        var candidates = results.Select(r => MapCandidate(r, metadataByChunkId)).ToArray();
        var reranked = rerankedResults.Select(r => MapCandidate(r, metadataByChunkId)).ToArray();

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
                Candidates = log.Candidates.Select(c => c with { Content = string.Empty, Metadata = null }).ToArray(),
                Reranked = log.Reranked.Select(c => c with { Content = string.Empty, Metadata = null }).ToArray(),
                FinalContext = string.Empty
            };
        return RetrievalResponseDto.FromLog(responseLog);
    }

    private static RetrievalCandidate MapCandidate(
        RagSearchResult r,
        IReadOnlyDictionary<long, IReadOnlyDictionary<string, string>>? metadataByChunkId)
    {
        IReadOnlyDictionary<string, string>? metadata = null;
        if (metadataByChunkId is not null && metadataByChunkId.TryGetValue(r.ChunkId, out var m))
        {
            metadata = m;
        }
        return new RetrievalCandidate(
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
            Metadata: metadata);
    }

    private async Task<IReadOnlyDictionary<long, IReadOnlyDictionary<string, string>>> BuildChunkMetadataAsync(
        TenantId tenantId,
        IReadOnlyList<long> chunkIds,
        CancellationToken cancellationToken)
    {
        if (chunkIds.Count == 0) return new Dictionary<long, IReadOnlyDictionary<string, string>>();
        var entities = await _chunkRepository.QueryByIdsAsync(tenantId, chunkIds, cancellationToken);
        var map = new Dictionary<long, IReadOnlyDictionary<string, string>>();
        foreach (var entity in entities)
        {
            var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["chunkIndex"] = entity.ChunkIndex.ToString(),
                ["startOffset"] = entity.StartOffset.ToString(),
                ["endOffset"] = entity.EndOffset.ToString(),
                ["hasEmbedding"] = entity.HasEmbedding.ToString(),
                ["createdAt"] = entity.CreatedAt.ToString("O"),
            };
            if (entity.RowIndex > 0)
            {
                meta["rowIndex"] = entity.RowIndex.ToString();
            }
            if (!string.IsNullOrWhiteSpace(entity.ColumnHeadersJson))
            {
                meta["columnHeaders"] = entity.ColumnHeadersJson!;
            }
            map[entity.Id] = meta;
        }
        return map;
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

        // v5 §38 / 计划 G4：MetadataFilter 严格匹配。
        // 当前候选 metadata 主要承载在 TagsJson（K3 之前），未来真实 metadata column 上线时这里改为读 chunk metadata。
        if (filter.MetadataFilter is { Count: > 0 } metaFilter)
        {
            q = q.Where(item => MatchesMetadataFilter(item, metaFilter));
        }

        if (!string.IsNullOrWhiteSpace(filter.OwnerFilter))
        {
            // Owner 字段由 KnowledgeDocumentMetaEntity.OwnerUserId 承载，但 RagSearchResult 当前未包含。
            // 留作未来扩展点：与 K3+ owner 列同时落地。
            _ = filter.OwnerFilter;
        }

        return q.ToArray();
    }

    private static bool MatchesMetadataFilter(RagSearchResult item, IReadOnlyDictionary<string, string> filter)
    {
        // 把 TagsJson 当作 tags 数组；约定 filter.key=tag, filter.value=tag-name 来过滤 tag。
        // 其它 key（如 sourceType / namespace）按字符串相等比较 RagSearchResult 的字段。
        foreach (var kv in filter)
        {
            var key = kv.Key.ToLowerInvariant();
            var expected = kv.Value;
            switch (key)
            {
                case "tag":
                case "tags":
                    if (!DocumentMatchesTags(item.TagsJson, new[] { expected }))
                    {
                        return false;
                    }
                    break;
                case "documentnamespace":
                case "namespace":
                    if (!string.Equals(item.DocumentNamespace, expected, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                    break;
                case "documentname":
                    if (!string.Equals(item.DocumentName, expected, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                    break;
                default:
                    // 兜底：把 filter 当作 tag 严格匹配（key 当作 tag 名）
                    if (!DocumentMatchesTags(item.TagsJson, new[] { kv.Key }))
                    {
                        return false;
                    }
                    break;
            }
        }
        return true;
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
