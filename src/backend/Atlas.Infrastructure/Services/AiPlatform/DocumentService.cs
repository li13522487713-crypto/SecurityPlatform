using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Abstractions.Knowledge;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.System.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class DocumentService : IDocumentService
{
    private readonly KnowledgeBaseRepository _knowledgeBaseRepository;
    private readonly KnowledgeDocumentRepository _documentRepository;
    private readonly DocumentChunkRepository _chunkRepository;
    private readonly IVectorStore _vectorStore;
    private readonly IFileStorageService _fileStorageService;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly KnowledgeQuotaPolicy _knowledgeQuotaPolicy;
    // v5 §35 / 计划 G3：由专用 ParseJobService 触发 Hangfire 链路
    private readonly IKnowledgeParseJobService _parseJobService;

    public DocumentService(
        KnowledgeBaseRepository knowledgeBaseRepository,
        KnowledgeDocumentRepository documentRepository,
        DocumentChunkRepository chunkRepository,
        IVectorStore vectorStore,
        IFileStorageService fileStorageService,
        IIdGeneratorAccessor idGeneratorAccessor,
        IUnitOfWork unitOfWork,
        KnowledgeQuotaPolicy knowledgeQuotaPolicy,
        IKnowledgeParseJobService parseJobService)
    {
        _knowledgeBaseRepository = knowledgeBaseRepository;
        _documentRepository = documentRepository;
        _chunkRepository = chunkRepository;
        _vectorStore = vectorStore;
        _fileStorageService = fileStorageService;
        _idGeneratorAccessor = idGeneratorAccessor;
        _unitOfWork = unitOfWork;
        _knowledgeQuotaPolicy = knowledgeQuotaPolicy;
        _parseJobService = parseJobService;
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        DocumentCreateRequest request,
        CancellationToken cancellationToken)
    {
        var kb = await _knowledgeBaseRepository.FindByIdAsync(tenantId, knowledgeBaseId, cancellationToken)
            ?? throw new BusinessException("知识库不存在。", ErrorCodes.NotFound);
        var file = await _fileStorageService.GetInfoAsync(tenantId, request.FileId, cancellationToken)
            ?? throw new BusinessException("文件不存在。", ErrorCodes.NotFound);

        await _knowledgeQuotaPolicy.EnsureCanAddDocumentAsync(tenantId, knowledgeBaseId, cancellationToken);
        _knowledgeQuotaPolicy.EnsureFileSize(file.SizeBytes);

        ValidateFileMatchesKnowledgeBaseType(kb.Type, file.ContentType);
        ValidateOptionalTagsAndImageMetadata(kb.Type, request.TagsJson, request.ImageMetadataJson);

        var entity = new KnowledgeDocument(
            tenantId,
            knowledgeBaseId,
            request.FileId,
            file.OriginalName,
            file.ContentType,
            file.SizeBytes,
            _idGeneratorAccessor.NextId(),
            request.TagsJson,
            request.ImageMetadataJson);
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _documentRepository.AddAsync(entity, cancellationToken);
            var documentCount = await _documentRepository.CountByKnowledgeBaseAsync(tenantId, knowledgeBaseId, cancellationToken);
            kb.SetDocumentCount(documentCount);
            await _knowledgeBaseRepository.UpdateAsync(kb, cancellationToken);
        }, cancellationToken);

        // v5 §35 / 计划 G3：把"上传 → 解析 → 切片 → 索引"封装为 KnowledgeParseJob，
        // 通过 IKnowledgeParseJobService 持久化记录并由 Hangfire BackgroundJob 调度执行。
        await _parseJobService.EnqueueParseAsync(
            tenantId,
            knowledgeBaseId,
            entity.Id,
            request.ParsingStrategy,
            cancellationToken);

        return entity.Id;
    }

    public async Task<PagedResult<KnowledgeDocumentDto>> ListByKnowledgeBaseAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        _ = await _knowledgeBaseRepository.FindByIdAsync(tenantId, knowledgeBaseId, cancellationToken)
            ?? throw new BusinessException("知识库不存在。", ErrorCodes.NotFound);
        var (items, total) = await _documentRepository.GetByKnowledgeBaseAsync(
            tenantId,
            knowledgeBaseId,
            pageIndex,
            pageSize,
            cancellationToken);
        return new PagedResult<KnowledgeDocumentDto>(items.Select(Map).ToList(), total, pageIndex, pageSize);
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        CancellationToken cancellationToken)
    {
        var kb = await _knowledgeBaseRepository.FindByIdAsync(tenantId, knowledgeBaseId, cancellationToken)
            ?? throw new BusinessException("知识库不存在。", ErrorCodes.NotFound);
        var doc = await _documentRepository.FindByKnowledgeBaseAndIdAsync(tenantId, knowledgeBaseId, documentId, cancellationToken)
            ?? throw new BusinessException("文档不存在。", ErrorCodes.NotFound);

        var chunks = await _chunkRepository.GetAllByDocumentAsync(tenantId, documentId, cancellationToken);
        var chunkIds = chunks.Select(x => x.Id.ToString()).ToList();
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _chunkRepository.DeleteByDocumentAsync(tenantId, documentId, cancellationToken);
            await _documentRepository.DeleteAsync(tenantId, documentId, cancellationToken);

            var documentCount = await _documentRepository.CountByKnowledgeBaseAsync(tenantId, knowledgeBaseId, cancellationToken);
            kb.SetDocumentCount(documentCount);
            kb.SetChunkCount(await _chunkRepository.CountByKnowledgeBaseAsync(tenantId, knowledgeBaseId, cancellationToken));
            await _knowledgeBaseRepository.UpdateAsync(kb, cancellationToken);
        }, cancellationToken);

        if (chunkIds.Count > 0)
        {
            try
            {
                await _vectorStore.DeleteAsync($"kb_{knowledgeBaseId}", chunkIds, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                // Ignore missing vector collection.
            }
        }
    }

    public async Task<DocumentProgressDto> GetProgressAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        CancellationToken cancellationToken)
    {
        _ = await _knowledgeBaseRepository.FindByIdAsync(tenantId, knowledgeBaseId, cancellationToken)
            ?? throw new BusinessException("知识库不存在。", ErrorCodes.NotFound);
        var doc = await _documentRepository.FindByKnowledgeBaseAndIdAsync(tenantId, knowledgeBaseId, documentId, cancellationToken)
            ?? throw new BusinessException("文档不存在。", ErrorCodes.NotFound);

        // v5 §35：从 DocumentProcessingStatus 映射到完整生命周期状态机。
        var lifecycle = doc.Status switch
        {
            DocumentProcessingStatus.Pending => KnowledgeDocumentLifecycleStatus.Uploaded,
            DocumentProcessingStatus.Processing => KnowledgeDocumentLifecycleStatus.Parsing,
            DocumentProcessingStatus.Completed => KnowledgeDocumentLifecycleStatus.Ready,
            DocumentProcessingStatus.Failed => KnowledgeDocumentLifecycleStatus.Failed,
            _ => KnowledgeDocumentLifecycleStatus.Uploaded
        };

        return new DocumentProgressDto(
            doc.Id,
            doc.Status,
            doc.ChunkCount,
            doc.ErrorMessage,
            doc.ProcessedAt,
            LifecycleStatus: lifecycle);
    }

    public async Task ResegmentAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        DocumentResegmentRequest request,
        CancellationToken cancellationToken)
    {
        _ = await _knowledgeBaseRepository.FindByIdAsync(tenantId, knowledgeBaseId, cancellationToken)
            ?? throw new BusinessException("知识库不存在。", ErrorCodes.NotFound);
        var doc = await _documentRepository.FindByKnowledgeBaseAndIdAsync(tenantId, knowledgeBaseId, documentId, cancellationToken)
            ?? throw new BusinessException("文档不存在。", ErrorCodes.NotFound);
        doc.MarkProcessing();
        await _documentRepository.UpdateAsync(doc, cancellationToken);

        // v5 §35 / 计划 G3：重跑解析也走 IKnowledgeParseJobService → Hangfire 链路。
        // 兼容旧请求：若调用方未提供 ParsingStrategy，则按 ParseStrategy 标量字段构造一个最小策略。
        var parsing = request.ParsingStrategy ?? new ParsingStrategy(
            ParsingType: request.ParseStrategy == DocumentParseStrategy.Precise
                ? ParsingType.Precise
                : ParsingType.Quick);
        await _parseJobService.EnqueueParseAsync(
            tenantId,
            knowledgeBaseId,
            documentId,
            parsing,
            cancellationToken);
    }

    private static KnowledgeDocumentDto Map(KnowledgeDocument entity)
        => new(
            entity.Id,
            entity.KnowledgeBaseId,
            entity.FileId,
            entity.FileName,
            entity.ContentType,
            entity.FileSizeBytes,
            entity.Status,
            entity.ErrorMessage,
            entity.ChunkCount,
            entity.CreatedAt,
            entity.ProcessedAt,
            entity.TagsJson,
            entity.ImageMetadataJson);

    private static void ValidateFileMatchesKnowledgeBaseType(KnowledgeBaseType type, string? contentType)
    {
        switch (type)
        {
            case KnowledgeBaseType.Image:
                if (string.IsNullOrWhiteSpace(contentType) ||
                    !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    throw new BusinessException("图片知识库仅支持上传 image/* 类型文件。", ErrorCodes.ValidationError);
                }

                break;
            case KnowledgeBaseType.Table:
                if (!string.IsNullOrWhiteSpace(contentType))
                {
                    var ct = contentType;
                    var textLike = ct.Contains("text/", StringComparison.OrdinalIgnoreCase) ||
                                   ct.Contains("csv", StringComparison.OrdinalIgnoreCase) ||
                                   ct.Contains("tsv", StringComparison.OrdinalIgnoreCase) ||
                                   ct.Contains("json", StringComparison.OrdinalIgnoreCase) ||
                                   ct.Contains("spreadsheet", StringComparison.OrdinalIgnoreCase);
                    if (!textLike)
                    {
                        throw new BusinessException("表格知识库请上传文本或 CSV/TSV 等可解析为行的文件。", ErrorCodes.ValidationError);
                    }
                }

                break;
            case KnowledgeBaseType.Text:
            default:
                break;
        }
    }

    private static void ValidateOptionalTagsAndImageMetadata(
        KnowledgeBaseType knowledgeBaseType,
        string? tagsJson,
        string? imageMetadataJson)
    {
        if (!string.IsNullOrWhiteSpace(tagsJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(tagsJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    throw new BusinessException("TagsJson 必须是 JSON 数组（字符串列表）。", ErrorCodes.ValidationError);
                }
            }
            catch (JsonException)
            {
                throw new BusinessException("TagsJson 不是合法的 JSON。", ErrorCodes.ValidationError);
            }
        }

        var metaTrimmed = imageMetadataJson?.Trim();
        var hasMeta = !string.IsNullOrEmpty(metaTrimmed) && metaTrimmed != "{}";
        if (knowledgeBaseType != KnowledgeBaseType.Image && hasMeta)
        {
            throw new BusinessException("ImageMetadataJson 仅适用于图片类型知识库。", ErrorCodes.ValidationError);
        }

        if (!hasMeta)
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(imageMetadataJson!);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new BusinessException("ImageMetadataJson 必须是 JSON 对象。", ErrorCodes.ValidationError);
            }
        }
        catch (JsonException)
        {
            throw new BusinessException("ImageMetadataJson 不是合法的 JSON。", ErrorCodes.ValidationError);
        }
    }
}
