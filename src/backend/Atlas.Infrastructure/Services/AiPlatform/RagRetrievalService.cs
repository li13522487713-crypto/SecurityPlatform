using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class RagRetrievalService : IRagRetrievalService
{
    private const string DefaultEmbeddingModel = "text-embedding-3-small";

    private readonly ILlmProviderFactory _llmProviderFactory;
    private readonly IVectorStore _vectorStore;
    private readonly DocumentChunkRepository _chunkRepository;
    private readonly KnowledgeDocumentRepository _knowledgeDocumentRepository;
    private readonly BM25RetrievalService _bm25RetrievalService;
    private readonly HybridRetrievalService _hybridRetrievalService;
    private readonly IOptionsMonitor<AiPlatformOptions> _optionsMonitor;
    private readonly ILogger<RagRetrievalService> _logger;

    public RagRetrievalService(
        ILlmProviderFactory llmProviderFactory,
        IVectorStore vectorStore,
        DocumentChunkRepository chunkRepository,
        KnowledgeDocumentRepository knowledgeDocumentRepository,
        BM25RetrievalService bm25RetrievalService,
        HybridRetrievalService hybridRetrievalService,
        IOptionsMonitor<AiPlatformOptions> options,
        ILogger<RagRetrievalService> logger)
    {
        _llmProviderFactory = llmProviderFactory;
        _vectorStore = vectorStore;
        _chunkRepository = chunkRepository;
        _knowledgeDocumentRepository = knowledgeDocumentRepository;
        _bm25RetrievalService = bm25RetrievalService;
        _hybridRetrievalService = hybridRetrievalService;
        _optionsMonitor = options;
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

        var normalizedKnowledgeBaseIds = knowledgeBaseIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();
        if (normalizedKnowledgeBaseIds.Length == 0)
        {
            return [];
        }

        var options = _optionsMonitor.CurrentValue;
        var vectorTopK = Math.Max(topK, options.Retrieval.VectorTopK);
        var bm25TopK = Math.Max(topK, options.Retrieval.Bm25TopK);

        var vectorResults = await SearchVectorAsync(
            tenantId,
            normalizedKnowledgeBaseIds,
            query,
            vectorTopK,
            ct);
        var bm25Results = await _bm25RetrievalService.SearchAsync(
            tenantId,
            normalizedKnowledgeBaseIds,
            query,
            bm25TopK,
            ct);

        if (options.Retrieval.EnableHybrid)
        {
            return _hybridRetrievalService.MergeAndRerank(query, vectorResults, bm25Results, topK);
        }

        return vectorResults
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .ToArray();
    }

    private async Task<IReadOnlyList<RagSearchResult>> SearchVectorAsync(
        TenantId tenantId,
        IReadOnlyList<long> knowledgeBaseIds,
        string query,
        int topK,
        CancellationToken ct)
    {
        var embeddingProvider = _llmProviderFactory.GetEmbeddingProvider();
        EmbeddingResult embedding;
        try
        {
            embedding = await embeddingProvider.EmbedAsync(
                new EmbeddingRequest(
                    Model: DefaultEmbeddingModel,
                    Inputs: [query.Trim()],
                    Provider: embeddingProvider.ProviderName),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RAG embedding generation failed. Fallback to lexical retrieval only.");
            return Array.Empty<RagSearchResult>();
        }

        var queryVector = embedding.Vectors.FirstOrDefault();
        if (queryVector is null || queryVector.Length == 0)
        {
            return Array.Empty<RagSearchResult>();
        }

        var vectorHits = new List<(long KnowledgeBaseId, VectorSearchResult Hit)>();
        foreach (var knowledgeBaseId in knowledgeBaseIds)
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

            vectorHits.AddRange(vectorResults.Select(hit => (knowledgeBaseId, hit)));
        }

        if (vectorHits.Count == 0)
        {
            return Array.Empty<RagSearchResult>();
        }

        var chunkIds = vectorHits
            .Select(hit => long.TryParse(hit.Hit.Id, out var id) ? id : 0L)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
        if (chunkIds.Length == 0)
        {
            return Array.Empty<RagSearchResult>();
        }

        var chunks = await _chunkRepository.QueryByIdsAsync(tenantId, chunkIds, ct);
        if (chunks.Count == 0)
        {
            return Array.Empty<RagSearchResult>();
        }

        var chunkMap = chunks.ToDictionary(chunk => chunk.Id, chunk => chunk);
        var documentIds = chunks.Select(chunk => chunk.DocumentId).Distinct().ToArray();
        var documentMap = (await _knowledgeDocumentRepository.QueryByIdsAsync(tenantId, documentIds, ct))
            .ToDictionary(document => document.Id, document => document.FileName);

        var merged = new List<RagSearchResult>(vectorHits.Count);
        foreach (var (knowledgeBaseId, result) in vectorHits)
        {
            if (!long.TryParse(result.Id, out var chunkId) || !chunkMap.TryGetValue(chunkId, out var chunk))
            {
                continue;
            }

            if (chunk.KnowledgeBaseId != knowledgeBaseId)
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

        return merged;
    }
}
