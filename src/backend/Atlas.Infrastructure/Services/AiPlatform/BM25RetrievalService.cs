using System.Text.RegularExpressions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class BM25RetrievalService
{
    private static readonly Regex TokenRegex = new(@"[\p{L}\p{N}_]+", RegexOptions.Compiled);

    private readonly DocumentChunkRepository _chunkRepository;
    private readonly KnowledgeDocumentRepository _knowledgeDocumentRepository;
    private readonly AiPlatformOptions _options;

    public BM25RetrievalService(
        DocumentChunkRepository chunkRepository,
        KnowledgeDocumentRepository knowledgeDocumentRepository,
        IOptions<AiPlatformOptions> options)
    {
        _chunkRepository = chunkRepository;
        _knowledgeDocumentRepository = knowledgeDocumentRepository;
        _options = options.Value;
    }

    /// <summary>
    /// v5 §38 / 计划 G4：profile-aware overload。
    /// profile 中 TopK / MinScore 优先于参数 topK；其它字段（hybrid / rerank）由上层 RagRetrievalService 负责。
    /// </summary>
    public Task<IReadOnlyList<RagSearchResult>> SearchAsync(
        TenantId tenantId,
        IReadOnlyList<long> knowledgeBaseIds,
        string query,
        int topK,
        RetrievalProfile? profile,
        CancellationToken cancellationToken)
    {
        var effectiveTopK = profile?.TopK > 0 ? profile.TopK : topK;
        return SearchAsync(tenantId, knowledgeBaseIds, query, effectiveTopK, cancellationToken);
    }

    public async Task<IReadOnlyList<RagSearchResult>> SearchAsync(
        TenantId tenantId,
        IReadOnlyList<long> knowledgeBaseIds,
        string query,
        int topK,
        CancellationToken cancellationToken)
    {
        if (knowledgeBaseIds.Count == 0 || string.IsNullOrWhiteSpace(query) || topK <= 0)
        {
            return Array.Empty<RagSearchResult>();
        }

        var normalizedKnowledgeBaseIds = knowledgeBaseIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
        if (normalizedKnowledgeBaseIds.Length == 0)
        {
            return Array.Empty<RagSearchResult>();
        }

        var chunks = await _chunkRepository.GetByKnowledgeBasesAsync(
            tenantId,
            normalizedKnowledgeBaseIds,
            _options.Retrieval.Bm25CandidateCount,
            cancellationToken);
        if (chunks.Count == 0)
        {
            return Array.Empty<RagSearchResult>();
        }

        var queryTerms = Tokenize(query);
        if (queryTerms.Length == 0)
        {
            return Array.Empty<RagSearchResult>();
        }

        var termSet = queryTerms.Distinct(StringComparer.Ordinal).ToArray();
        var chunkTerms = chunks.ToDictionary(chunk => chunk.Id, chunk => Tokenize(chunk.Content));
        var avgDocLength = Math.Max(1, chunkTerms.Values.Average(tokens => tokens.Length));

        var docFreq = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var term in termSet)
        {
            docFreq[term] = chunkTerms.Values.Count(tokens => tokens.Contains(term, StringComparer.Ordinal));
        }

        var scored = new List<(long ChunkId, float Score)>(chunks.Count);
        var totalDocs = chunks.Count;
        foreach (var chunk in chunks)
        {
            var tokens = chunkTerms[chunk.Id];
            var termFreq = tokens
                .GroupBy(token => token, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

            var score = 0d;
            foreach (var term in termSet)
            {
                if (!termFreq.TryGetValue(term, out var tf) || tf == 0)
                {
                    continue;
                }

                var df = Math.Max(1, docFreq.GetValueOrDefault(term));
                var idf = Math.Log(1 + ((totalDocs - df + 0.5) / (df + 0.5)));
                const double k1 = 1.5;
                const double b = 0.75;
                var denominator = tf + k1 * (1 - b + b * (tokens.Length / avgDocLength));
                score += idf * ((tf * (k1 + 1)) / denominator);
            }

            if (score > 0)
            {
                scored.Add((chunk.Id, (float)score));
            }
        }

        if (scored.Count == 0)
        {
            return Array.Empty<RagSearchResult>();
        }

        var topChunkIds = scored
            .OrderByDescending(item => item.Score)
            .Take(topK)
            .Select(item => item.ChunkId)
            .ToArray();

        var topChunkMap = chunks
            .Where(chunk => topChunkIds.Contains(chunk.Id))
            .ToDictionary(chunk => chunk.Id, chunk => chunk);
        var scoreMap = scored.ToDictionary(item => item.ChunkId, item => item.Score);

        var documentIds = topChunkMap.Values.Select(chunk => chunk.DocumentId).Distinct().ToArray();
        var documents = await _knowledgeDocumentRepository.QueryByIdsAsync(tenantId, documentIds, cancellationToken);
        var documentMap = documents.ToDictionary(item => item.Id, item => item.FileName);
        var documentCreatedAtMap = documents.ToDictionary(item => item.Id, item => (DateTime?)item.CreatedAt);
        var documentTagsMap = documents.ToDictionary(item => item.Id, item => item.TagsJson);

        return topChunkIds
            .Where(id => topChunkMap.ContainsKey(id))
            .Select(id =>
            {
                var chunk = topChunkMap[id];
                documentMap.TryGetValue(chunk.DocumentId, out var documentName);
                documentCreatedAtMap.TryGetValue(chunk.DocumentId, out var documentCreatedAt);
                documentTagsMap.TryGetValue(chunk.DocumentId, out var tagsJson);
                return new RagSearchResult(
                    chunk.KnowledgeBaseId,
                    chunk.DocumentId,
                    chunk.Id,
                    chunk.Content,
                    scoreMap[id],
                    documentName,
                    documentCreatedAt,
                    chunk.StartOffset,
                    chunk.EndOffset,
                    tagsJson,
                    DocumentNamespace: null);
            })
            .ToArray();
    }

    private static string[] Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        return TokenRegex.Matches(text.ToLowerInvariant())
            .Select(match => match.Value)
            .Where(token => token.Length > 1)
            .ToArray();
    }
}
