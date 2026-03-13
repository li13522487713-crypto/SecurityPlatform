using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class KnowledgeBaseService : IKnowledgeBaseService
{
    private readonly KnowledgeBaseRepository _knowledgeBaseRepository;
    private readonly KnowledgeDocumentRepository _knowledgeDocumentRepository;
    private readonly DocumentChunkRepository _documentChunkRepository;
    private readonly AgentKnowledgeLinkRepository _agentKnowledgeLinkRepository;
    private readonly IVectorStore _vectorStore;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IUnitOfWork _unitOfWork;

    public KnowledgeBaseService(
        KnowledgeBaseRepository knowledgeBaseRepository,
        KnowledgeDocumentRepository knowledgeDocumentRepository,
        DocumentChunkRepository documentChunkRepository,
        AgentKnowledgeLinkRepository agentKnowledgeLinkRepository,
        IVectorStore vectorStore,
        IIdGeneratorAccessor idGeneratorAccessor,
        IUnitOfWork unitOfWork)
    {
        _knowledgeBaseRepository = knowledgeBaseRepository;
        _knowledgeDocumentRepository = knowledgeDocumentRepository;
        _documentChunkRepository = documentChunkRepository;
        _agentKnowledgeLinkRepository = agentKnowledgeLinkRepository;
        _vectorStore = vectorStore;
        _idGeneratorAccessor = idGeneratorAccessor;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<KnowledgeBaseDto>> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _knowledgeBaseRepository.GetPagedAsync(
            tenantId,
            keyword,
            pageIndex,
            pageSize,
            cancellationToken);
        return new PagedResult<KnowledgeBaseDto>(items.Select(Map).ToList(), total, pageIndex, pageSize);
    }

    public async Task<KnowledgeBaseDto?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        var entity = await _knowledgeBaseRepository.FindByIdAsync(tenantId, id, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        KnowledgeBaseCreateRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedName = request.Name.Trim();
        if (await _knowledgeBaseRepository.ExistsByNameAsync(tenantId, normalizedName, excludeId: null, cancellationToken))
        {
            throw new BusinessException("知识库名称已存在。", ErrorCodes.ValidationError);
        }

        var entity = new KnowledgeBase(
            tenantId,
            normalizedName,
            request.Description?.Trim(),
            request.Type,
            _idGeneratorAccessor.NextId());
        await _knowledgeBaseRepository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long id,
        KnowledgeBaseUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _knowledgeBaseRepository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("知识库不存在。", ErrorCodes.NotFound);
        var normalizedName = request.Name.Trim();
        if (await _knowledgeBaseRepository.ExistsByNameAsync(tenantId, normalizedName, excludeId: id, cancellationToken))
        {
            throw new BusinessException("知识库名称已存在。", ErrorCodes.ValidationError);
        }

        entity.Update(normalizedName, request.Description?.Trim(), request.Type);
        await _knowledgeBaseRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await _knowledgeBaseRepository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("知识库不存在。", ErrorCodes.NotFound);

        var chunks = await _documentChunkRepository.GetByKnowledgeBaseAsync(tenantId, entity.Id, top: 0, cancellationToken);
        var chunkIds = chunks.Select(x => x.Id.ToString()).ToList();

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _agentKnowledgeLinkRepository.DeleteByKnowledgeBaseIdAsync(tenantId, entity.Id, cancellationToken);
            await _documentChunkRepository.DeleteByKnowledgeBaseAsync(tenantId, entity.Id, cancellationToken);
            await _knowledgeDocumentRepository.DeleteByKnowledgeBaseAsync(tenantId, entity.Id, cancellationToken);
            await _knowledgeBaseRepository.DeleteAsync(tenantId, entity.Id, cancellationToken);
        }, cancellationToken);

        if (chunkIds.Count > 0)
        {
            try
            {
                await _vectorStore.DeleteAsync($"kb_{entity.Id}", chunkIds, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                // Ignore missing vector collection; relational cascade already succeeded.
            }
        }
    }

    private static KnowledgeBaseDto Map(KnowledgeBase entity)
        => new(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.Type,
            entity.DocumentCount,
            entity.ChunkCount,
            entity.CreatedAt);
}
