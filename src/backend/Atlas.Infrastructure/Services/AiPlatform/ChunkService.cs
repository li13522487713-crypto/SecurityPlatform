using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class ChunkService : IChunkService
{
    private readonly KnowledgeBaseRepository _knowledgeBaseRepository;
    private readonly KnowledgeDocumentRepository _documentRepository;
    private readonly DocumentChunkRepository _chunkRepository;
    private readonly IVectorStore _vectorStore;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IUnitOfWork _unitOfWork;

    public ChunkService(
        KnowledgeBaseRepository knowledgeBaseRepository,
        KnowledgeDocumentRepository documentRepository,
        DocumentChunkRepository chunkRepository,
        IVectorStore vectorStore,
        IIdGeneratorAccessor idGeneratorAccessor,
        IUnitOfWork unitOfWork)
    {
        _knowledgeBaseRepository = knowledgeBaseRepository;
        _documentRepository = documentRepository;
        _chunkRepository = chunkRepository;
        _vectorStore = vectorStore;
        _idGeneratorAccessor = idGeneratorAccessor;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<DocumentChunkDto>> GetByDocumentAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await EnsureDocumentInKnowledgeBaseAsync(tenantId, knowledgeBaseId, documentId, cancellationToken);
        var (items, total) = await _chunkRepository.GetByDocumentAsync(tenantId, documentId, pageIndex, pageSize, cancellationToken);
        return new PagedResult<DocumentChunkDto>(items.Select(Map).ToList(), total, pageIndex, pageSize);
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        ChunkCreateRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureDocumentInKnowledgeBaseAsync(tenantId, knowledgeBaseId, request.DocumentId, cancellationToken);
        var entity = new DocumentChunk(
            tenantId,
            knowledgeBaseId,
            request.DocumentId,
            request.ChunkIndex,
            request.Content.Trim(),
            request.StartOffset,
            request.EndOffset,
            hasEmbedding: false,
            _idGeneratorAccessor.NextId());
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _chunkRepository.AddAsync(entity, cancellationToken);
            await RefreshKnowledgeBaseChunkCountAsync(tenantId, knowledgeBaseId, cancellationToken);
            await RefreshDocumentChunkCountAsync(tenantId, request.DocumentId, cancellationToken);
        }, cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long chunkId,
        ChunkUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var chunk = await _chunkRepository.FindByKnowledgeBaseAndIdAsync(tenantId, knowledgeBaseId, chunkId, cancellationToken)
            ?? throw new BusinessException("分片不存在。", ErrorCodes.NotFound);

        chunk.UpdateContent(request.Content.Trim(), request.StartOffset, request.EndOffset);
        chunk.MarkEmbedding(false);
        await _chunkRepository.UpdateAsync(chunk, cancellationToken);
        try
        {
            await _vectorStore.DeleteAsync($"kb_{knowledgeBaseId}", [chunk.Id.ToString()], cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // Ignore missing vector collection.
        }
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long chunkId,
        CancellationToken cancellationToken)
    {
        var chunk = await _chunkRepository.FindByKnowledgeBaseAndIdAsync(tenantId, knowledgeBaseId, chunkId, cancellationToken)
            ?? throw new BusinessException("分片不存在。", ErrorCodes.NotFound);
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _chunkRepository.DeleteAsync(tenantId, chunkId, cancellationToken);
            await RefreshKnowledgeBaseChunkCountAsync(tenantId, knowledgeBaseId, cancellationToken);
            await RefreshDocumentChunkCountAsync(tenantId, chunk.DocumentId, cancellationToken);
        }, cancellationToken);

        try
        {
            await _vectorStore.DeleteAsync($"kb_{knowledgeBaseId}", [chunk.Id.ToString()], cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // Ignore missing vector collection.
        }
    }

    private async Task EnsureDocumentInKnowledgeBaseAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        CancellationToken cancellationToken)
    {
        _ = await _knowledgeBaseRepository.FindByIdAsync(tenantId, knowledgeBaseId, cancellationToken)
            ?? throw new BusinessException("知识库不存在。", ErrorCodes.NotFound);
        var document = await _documentRepository.FindByIdAsync(tenantId, documentId, cancellationToken)
            ?? throw new BusinessException("文档不存在。", ErrorCodes.NotFound);
        if (document.KnowledgeBaseId != knowledgeBaseId)
        {
            throw new BusinessException("文档不属于该知识库。", ErrorCodes.ValidationError);
        }
    }

    private async Task RefreshKnowledgeBaseChunkCountAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        CancellationToken cancellationToken)
    {
        var kb = await _knowledgeBaseRepository.FindByIdAsync(tenantId, knowledgeBaseId, cancellationToken);
        if (kb is null)
        {
            return;
        }

        kb.SetChunkCount(await _chunkRepository.CountByKnowledgeBaseAsync(tenantId, knowledgeBaseId, cancellationToken));
        await _knowledgeBaseRepository.UpdateAsync(kb, cancellationToken);
    }

    private async Task RefreshDocumentChunkCountAsync(
        TenantId tenantId,
        long documentId,
        CancellationToken cancellationToken)
    {
        var document = await _documentRepository.FindByIdAsync(tenantId, documentId, cancellationToken);
        if (document is null)
        {
            return;
        }

        var chunks = await _chunkRepository.GetAllByDocumentAsync(tenantId, documentId, cancellationToken);
        document.MarkCompleted(chunks.Count);
        await _documentRepository.UpdateAsync(document, cancellationToken);
    }

    private static DocumentChunkDto Map(DocumentChunk entity)
        => new(
            entity.Id,
            entity.KnowledgeBaseId,
            entity.DocumentId,
            entity.ChunkIndex,
            entity.Content,
            entity.StartOffset,
            entity.EndOffset,
            entity.HasEmbedding,
            entity.CreatedAt,
            entity.RowIndex,
            entity.ColumnHeadersJson);
}
