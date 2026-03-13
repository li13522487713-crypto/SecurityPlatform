using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.System.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class DocumentProcessingService
{
    private const string DefaultEmbeddingModel = "text-embedding-3-small";
    private const int EmbeddingBatchSize = 20;

    private readonly KnowledgeDocumentRepository _documentRepository;
    private readonly DocumentChunkRepository _chunkRepository;
    private readonly KnowledgeBaseRepository _knowledgeBaseRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IDocumentParser _documentParser;
    private readonly IChunkingService _chunkingService;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IVectorStore _vectorStore;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DocumentProcessingService> _logger;

    public DocumentProcessingService(
        KnowledgeDocumentRepository documentRepository,
        DocumentChunkRepository chunkRepository,
        KnowledgeBaseRepository knowledgeBaseRepository,
        IFileStorageService fileStorageService,
        IDocumentParser documentParser,
        IChunkingService chunkingService,
        IEmbeddingProvider embeddingProvider,
        IVectorStore vectorStore,
        IIdGeneratorAccessor idGeneratorAccessor,
        IUnitOfWork unitOfWork,
        ILogger<DocumentProcessingService> logger)
    {
        _documentRepository = documentRepository;
        _chunkRepository = chunkRepository;
        _knowledgeBaseRepository = knowledgeBaseRepository;
        _fileStorageService = fileStorageService;
        _documentParser = documentParser;
        _chunkingService = chunkingService;
        _embeddingProvider = embeddingProvider;
        _vectorStore = vectorStore;
        _idGeneratorAccessor = idGeneratorAccessor;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task ProcessAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        ChunkingOptions options,
        CancellationToken cancellationToken)
    {
        var document = await _documentRepository.FindByKnowledgeBaseAndIdAsync(tenantId, knowledgeBaseId, documentId, cancellationToken)
            ?? throw new BusinessException("文档不存在。", ErrorCodes.NotFound);

        var kb = await _knowledgeBaseRepository.FindByIdAsync(tenantId, knowledgeBaseId, cancellationToken)
            ?? throw new BusinessException("知识库不存在。", ErrorCodes.NotFound);

        try
        {
            document.MarkProcessing();
            await _documentRepository.UpdateAsync(document, cancellationToken);

            var previousChunks = await _chunkRepository.GetAllByDocumentAsync(tenantId, document.Id, cancellationToken);
            if (previousChunks.Count > 0)
            {
                await _chunkRepository.DeleteByDocumentAsync(tenantId, document.Id, cancellationToken);
                try
                {
                    await _vectorStore.DeleteAsync(
                        $"kb_{knowledgeBaseId}",
                        previousChunks.Select(x => x.Id.ToString()),
                        cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    // Collection may not exist yet.
                }
            }

            if (!document.FileId.HasValue || document.FileId.Value <= 0)
            {
                throw new BusinessException("文件记录缺失，无法解析文档。", ErrorCodes.ValidationError);
            }

            var file = await _fileStorageService.DownloadAsync(tenantId, document.FileId.Value, cancellationToken);
            await using var stream = file.Stream;
            var parsed = await _documentParser.ParseAsync(stream, file.FileName, cancellationToken);
            var chunks = _chunkingService.Chunk(parsed.Text, options);

            var chunkEntities = chunks.Select(chunk => new DocumentChunk(
                tenantId,
                knowledgeBaseId,
                document.Id,
                chunk.Index,
                chunk.Content,
                chunk.StartOffset,
                chunk.EndOffset,
                hasEmbedding: false,
                _idGeneratorAccessor.NextId())).ToList();

            await _chunkRepository.AddRangeAsync(chunkEntities, cancellationToken);

            if (chunkEntities.Count > 0)
            {
                await EnsureVectorCollectionAsync(knowledgeBaseId, chunkEntities, cancellationToken);
                foreach (var batch in chunkEntities.Chunk(EmbeddingBatchSize))
                {
                    var batchItems = batch.ToArray();
                    var embedding = await _embeddingProvider.EmbedAsync(
                        new EmbeddingRequest(
                            Model: DefaultEmbeddingModel,
                            Inputs: batchItems.Select(x => x.Content).ToArray(),
                            Provider: _embeddingProvider.ProviderName),
                        cancellationToken);

                    var vectors = new List<VectorRecord>(batchItems.Length);
                    var embeddedChunkIds = new List<long>(batchItems.Length);
                    for (var i = 0; i < batchItems.Length; i++)
                    {
                        var entity = batchItems[i];
                        var vector = i < embedding.Vectors.Count ? embedding.Vectors[i] : Array.Empty<float>();
                        if (vector.Length == 0)
                        {
                            continue;
                        }

                        vectors.Add(new VectorRecord(
                            entity.Id.ToString(),
                            vector,
                            entity.Content,
                            new Dictionary<string, string>
                            {
                                ["knowledgeBaseId"] = entity.KnowledgeBaseId.ToString(),
                                ["documentId"] = entity.DocumentId.ToString(),
                                ["chunkId"] = entity.Id.ToString()
                            }));
                        embeddedChunkIds.Add(entity.Id);
                    }

                    if (vectors.Count == 0)
                    {
                        continue;
                    }

                    await _vectorStore.UpsertAsync($"kb_{knowledgeBaseId}", vectors, cancellationToken);
                    await _chunkRepository.MarkEmbeddingByIdsAsync(tenantId, embeddedChunkIds, hasEmbedding: true, cancellationToken);
                }
            }

            document.MarkCompleted(chunkEntities.Count);
            kb.SetDocumentCount(await _documentRepository.CountByKnowledgeBaseAsync(tenantId, knowledgeBaseId, cancellationToken));
            kb.SetChunkCount(await _chunkRepository.CountByKnowledgeBaseAsync(tenantId, knowledgeBaseId, cancellationToken));
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _documentRepository.UpdateAsync(document, cancellationToken);
                await _knowledgeBaseRepository.UpdateAsync(kb, cancellationToken);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Process document failed. kb={KnowledgeBaseId}, doc={DocumentId}", knowledgeBaseId, documentId);
            document.MarkFailed(ex.Message);
            kb.SetDocumentCount(await _documentRepository.CountByKnowledgeBaseAsync(tenantId, knowledgeBaseId, cancellationToken));
            kb.SetChunkCount(await _chunkRepository.CountByKnowledgeBaseAsync(tenantId, knowledgeBaseId, cancellationToken));
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _documentRepository.UpdateAsync(document, cancellationToken);
                await _knowledgeBaseRepository.UpdateAsync(kb, cancellationToken);
            }, cancellationToken);
        }
    }

    private async Task EnsureVectorCollectionAsync(
        long knowledgeBaseId,
        IReadOnlyCollection<DocumentChunk> chunkEntities,
        CancellationToken cancellationToken)
    {
        var probe = await _embeddingProvider.EmbedAsync(
            new EmbeddingRequest(
                Model: DefaultEmbeddingModel,
                Inputs: [chunkEntities.First().Content],
                Provider: _embeddingProvider.ProviderName),
            cancellationToken);
        var dimensions = probe.Vectors.FirstOrDefault()?.Length ?? 0;
        if (dimensions <= 0)
        {
            throw new InvalidOperationException("Embedding provider returned empty vector dimensions.");
        }

        await _vectorStore.EnsureCollectionAsync($"kb_{knowledgeBaseId}", dimensions, cancellationToken);
    }
}
