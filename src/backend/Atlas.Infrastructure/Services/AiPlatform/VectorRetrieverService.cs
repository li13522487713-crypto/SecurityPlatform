using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class VectorRetrieverService : IRetriever
{
    private readonly ILlmProviderFactory _llmProviderFactory;
    private readonly IVectorStore _vectorStore;
    private readonly DocumentChunkRepository _chunkRepository;
    private readonly KnowledgeDocumentRepository _knowledgeDocumentRepository;
    private readonly ILogger<VectorRetrieverService> _logger;

    public VectorRetrieverService(
        ILlmProviderFactory llmProviderFactory,
        IVectorStore vectorStore,
        DocumentChunkRepository chunkRepository,
        KnowledgeDocumentRepository knowledgeDocumentRepository,
        ILogger<VectorRetrieverService> logger)
    {
        _llmProviderFactory = llmProviderFactory;
        _vectorStore = vectorStore;
        _chunkRepository = chunkRepository;
        _knowledgeDocumentRepository = knowledgeDocumentRepository;
        _logger = logger;
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

        var normalizedKnowledgeBaseIds = knowledgeBaseIds
            .Where(item => item > 0)
            .Distinct()
            .ToArray();
        if (normalizedKnowledgeBaseIds.Length == 0)
        {
            return [];
        }

        var embeddingProvider = _llmProviderFactory.GetEmbeddingProvider();
        EmbeddingResult embedding;
        try
        {
            embedding = await embeddingProvider.EmbedAsync(
                new EmbeddingRequest(
                    Model: "text-embedding-3-small",
                    Inputs: [query.Trim()],
                    Provider: embeddingProvider.ProviderName),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RAG vector embedding generation failed.");
            return [];
        }

        var queryVector = embedding.Vectors.FirstOrDefault();
        if (queryVector is null || queryVector.Length == 0)
        {
            return [];
        }

        var vectorHits = new List<(long KnowledgeBaseId, VectorSearchResult Hit)>();
        foreach (var knowledgeBaseId in normalizedKnowledgeBaseIds)
        {
            IReadOnlyList<VectorSearchResult> vectorResults;
            try
            {
                vectorResults = await _vectorStore.SearchAsync(
                    $"kb_{knowledgeBaseId}",
                    queryVector,
                    topK,
                    cancellationToken);
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

            vectorHits.AddRange(vectorResults.Select(item => (knowledgeBaseId, item)));
        }

        if (vectorHits.Count == 0)
        {
            return [];
        }

        var chunkIds = vectorHits
            .Select(item => long.TryParse(item.Hit.Id, out var chunkId) ? chunkId : 0L)
            .Where(chunkId => chunkId > 0)
            .Distinct()
            .ToArray();
        if (chunkIds.Length == 0)
        {
            return [];
        }

        var chunks = await _chunkRepository.QueryByIdsAsync(tenantId, chunkIds, cancellationToken);
        if (chunks.Count == 0)
        {
            return [];
        }

        var chunkMap = chunks.ToDictionary(item => item.Id, item => item);
        var documentIds = chunks
            .Select(item => item.DocumentId)
            .Distinct()
            .ToArray();
        var documents = await _knowledgeDocumentRepository.QueryByIdsAsync(tenantId, documentIds, cancellationToken);
        var documentNameMap = documents.ToDictionary(item => item.Id, item => item.FileName);
        var documentCreatedAtMap = documents.ToDictionary(item => item.Id, item => (DateTime?)item.CreatedAt);

        var merged = new List<RagSearchResult>(vectorHits.Count);
        foreach (var (knowledgeBaseId, hit) in vectorHits)
        {
            if (!long.TryParse(hit.Id, out var chunkId) || !chunkMap.TryGetValue(chunkId, out var chunk))
            {
                continue;
            }

            if (chunk.KnowledgeBaseId != knowledgeBaseId)
            {
                continue;
            }

            documentNameMap.TryGetValue(chunk.DocumentId, out var documentName);
            documentCreatedAtMap.TryGetValue(chunk.DocumentId, out var documentCreatedAt);
            merged.Add(
                new RagSearchResult(
                    knowledgeBaseId,
                    chunk.DocumentId,
                    chunk.Id,
                    string.IsNullOrWhiteSpace(hit.Content) ? chunk.Content : hit.Content,
                    hit.Score,
                    documentName,
                    documentCreatedAt));
        }

        return merged;
    }
}
