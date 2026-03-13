using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class RagRetrievalService : IRagRetrievalService
{
    private const string DefaultEmbeddingModel = "text-embedding-3-small";

    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IVectorStore _vectorStore;
    private readonly DocumentChunkRepository _chunkRepository;
    private readonly KnowledgeDocumentRepository _knowledgeDocumentRepository;
    private readonly ILogger<RagRetrievalService> _logger;

    public RagRetrievalService(
        IEmbeddingProvider embeddingProvider,
        IVectorStore vectorStore,
        DocumentChunkRepository chunkRepository,
        KnowledgeDocumentRepository knowledgeDocumentRepository,
        ILogger<RagRetrievalService> logger)
    {
        _embeddingProvider = embeddingProvider;
        _vectorStore = vectorStore;
        _chunkRepository = chunkRepository;
        _knowledgeDocumentRepository = knowledgeDocumentRepository;
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

        var embedding = await _embeddingProvider.EmbedAsync(
            new EmbeddingRequest(
                Model: DefaultEmbeddingModel,
                Inputs: [query.Trim()],
                Provider: _embeddingProvider.ProviderName),
            ct);
        var queryVector = embedding.Vectors.FirstOrDefault();
        if (queryVector is null || queryVector.Length == 0)
        {
            return [];
        }

        var merged = new List<RagSearchResult>();
        foreach (var knowledgeBaseId in knowledgeBaseIds.Distinct().Where(x => x > 0))
        {
            IReadOnlyList<VectorSearchResult> vectorResults;
            try
            {
                vectorResults = await _vectorStore.SearchAsync($"kb_{knowledgeBaseId}", queryVector, topK, ct);
            }
            catch (InvalidOperationException)
            {
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RAG vector search failed for kb={KnowledgeBaseId}.", knowledgeBaseId);
                continue;
            }

            if (vectorResults.Count == 0)
            {
                continue;
            }

            var chunkIdCandidates = vectorResults
                .Select(r => long.TryParse(r.Id, out var id) ? id : 0L)
                .Where(id => id > 0)
                .ToArray();
            if (chunkIdCandidates.Length == 0)
            {
                continue;
            }

            var chunks = await _chunkRepository.QueryByIdsAsync(tenantId, chunkIdCandidates, ct);
            if (chunks.Count == 0)
            {
                continue;
            }

            var chunkMap = chunks
                .Where(x => x.KnowledgeBaseId == knowledgeBaseId)
                .ToDictionary(x => x.Id, x => x);
            if (chunkMap.Count == 0)
            {
                continue;
            }

            var documentIds = chunkMap.Values.Select(x => x.DocumentId).Distinct().ToArray();
            var documentMap = (await _knowledgeDocumentRepository.QueryByIdsAsync(tenantId, documentIds, ct))
                .ToDictionary(x => x.Id, x => x.FileName);

            foreach (var result in vectorResults)
            {
                if (!long.TryParse(result.Id, out var chunkId) || !chunkMap.TryGetValue(chunkId, out var chunk))
                {
                    continue;
                }

                documentMap.TryGetValue(chunk.DocumentId, out var documentName);
                merged.Add(new RagSearchResult(
                    knowledgeBaseId,
                    chunk.DocumentId,
                    chunk.Id,
                    string.IsNullOrWhiteSpace(result.Content) ? chunk.Content : result.Content,
                    result.Score,
                    documentName));
            }
        }

        return merged
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .ToArray();
    }
}
