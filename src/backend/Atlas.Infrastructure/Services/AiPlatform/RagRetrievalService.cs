using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
