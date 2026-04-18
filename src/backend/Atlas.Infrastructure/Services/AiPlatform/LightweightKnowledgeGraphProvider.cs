using System.Text.RegularExpressions;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class LightweightKnowledgeGraphProvider : IKnowledgeGraphProvider
{
    private static readonly Regex TokenRegex = new(@"[\p{L}\p{N}_]+", RegexOptions.Compiled);

    private readonly DocumentChunkRepository _chunkRepository;
    private readonly KnowledgeDocumentRepository _knowledgeDocumentRepository;

    public LightweightKnowledgeGraphProvider(
        DocumentChunkRepository chunkRepository,
        KnowledgeDocumentRepository knowledgeDocumentRepository)
    {
        _chunkRepository = chunkRepository;
        _knowledgeDocumentRepository = knowledgeDocumentRepository;
    }

    public async Task<IReadOnlyList<RagSearchResult>> SearchAsync(
        TenantId tenantId,
        IReadOnlyList<long> knowledgeBaseIds,
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        if (knowledgeBaseIds.Count == 0 || string.IsNullOrWhiteSpace(query) || topK <= 0)
        {
            return [];
        }

        var normalizedKnowledgeBaseIds = knowledgeBaseIds
            .Where(item => item > 0)
            .Distinct()
            .ToArray();
        if (normalizedKnowledgeBaseIds.Length == 0)
        {
            return [];
        }

        var queryTokens = Tokenize(query);
        if (queryTokens.Length == 0)
        {
            return [];
        }

        var candidates = await _chunkRepository.GetByKnowledgeBasesAsync(
            tenantId,
            normalizedKnowledgeBaseIds,
            topPerKnowledgeBase: Math.Max(topK * 40, 200),
            cancellationToken);
        if (candidates.Count == 0)
        {
            return [];
        }

        var candidateTokenMap = candidates.ToDictionary(item => item.Id, item => Tokenize(item.Content));
        var seedCandidates = candidates
            .Select(item => new
            {
                Chunk = item,
                Overlap = ComputeOverlap(queryTokens, candidateTokenMap[item.Id])
            })
            .Where(item => item.Overlap > 0f)
            .OrderByDescending(item => item.Overlap)
            .Take(Math.Max(topK, 4))
            .ToArray();
        if (seedCandidates.Length == 0)
        {
            return [];
        }

        var scoreMap = new Dictionary<long, float>();
        foreach (var seed in seedCandidates)
        {
            scoreMap[seed.Chunk.Id] = Math.Max(scoreMap.GetValueOrDefault(seed.Chunk.Id), seed.Overlap);
        }

        var groupedByDocument = candidates
            .GroupBy(item => item.DocumentId)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.ChunkIndex).ToArray());

        foreach (var seed in seedCandidates)
        {
            if (!groupedByDocument.TryGetValue(seed.Chunk.DocumentId, out var docChunks))
            {
                continue;
            }

            foreach (var neighbor in docChunks)
            {
                if (Math.Abs(neighbor.ChunkIndex - seed.Chunk.ChunkIndex) > 2)
                {
                    continue;
                }

                var neighborToken = candidateTokenMap[neighbor.Id];
                var lexical = ComputeOverlap(queryTokens, neighborToken);
                var boostedScore = Math.Max(seed.Overlap * 0.75f, lexical * 0.65f);
                scoreMap[neighbor.Id] = Math.Max(scoreMap.GetValueOrDefault(neighbor.Id), boostedScore);
            }
        }

        var selectedChunkIds = scoreMap
            .OrderByDescending(item => item.Value)
            .Take(topK)
            .Select(item => item.Key)
            .ToArray();
        if (selectedChunkIds.Length == 0)
        {
            return [];
        }

        var selectedChunkMap = candidates
            .Where(item => selectedChunkIds.Contains(item.Id))
            .ToDictionary(item => item.Id, item => item);
        var documentIds = selectedChunkMap.Values
            .Select(item => item.DocumentId)
            .Distinct()
            .ToArray();
        var documents = await _knowledgeDocumentRepository.QueryByIdsAsync(tenantId, documentIds, cancellationToken);
        var documentNameMap = documents.ToDictionary(item => item.Id, item => item.FileName);
        var documentCreatedAtMap = documents.ToDictionary(item => item.Id, item => (DateTime?)item.CreatedAt);
        var documentTagsMap = documents.ToDictionary(item => item.Id, item => item.TagsJson);

        return selectedChunkIds
            .Where(selectedChunkMap.ContainsKey)
            .Select(chunkId => BuildResult(
                selectedChunkMap[chunkId],
                scoreMap[chunkId],
                documentNameMap,
                documentCreatedAtMap,
                documentTagsMap))
            .ToArray();
    }

    private static RagSearchResult BuildResult(
        DocumentChunk chunk,
        float score,
        IReadOnlyDictionary<long, string> documentNameMap,
        IReadOnlyDictionary<long, DateTime?> documentCreatedAtMap,
        IReadOnlyDictionary<long, string> documentTagsMap)
    {
        documentNameMap.TryGetValue(chunk.DocumentId, out var documentName);
        documentCreatedAtMap.TryGetValue(chunk.DocumentId, out var documentCreatedAt);
        documentTagsMap.TryGetValue(chunk.DocumentId, out var tagsJson);
        return new RagSearchResult(
            chunk.KnowledgeBaseId,
            chunk.DocumentId,
            chunk.Id,
            chunk.Content,
            score,
            documentName,
            documentCreatedAt,
            chunk.StartOffset,
            chunk.EndOffset,
            tagsJson,
            DocumentNamespace: null);
    }

    private static float ComputeOverlap(
        IReadOnlyCollection<string> queryTokens,
        IReadOnlyCollection<string> contentTokens)
    {
        if (queryTokens.Count == 0 || contentTokens.Count == 0)
        {
            return 0f;
        }

        var overlap = contentTokens.Count(queryTokens.Contains);
        if (overlap <= 0)
        {
            return 0f;
        }

        return overlap / (float)queryTokens.Count;
    }

    private static string[] Tokenize(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        return TokenRegex.Matches(content.ToLowerInvariant())
            .Select(item => item.Value)
            .Where(item => item.Length > 1)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
