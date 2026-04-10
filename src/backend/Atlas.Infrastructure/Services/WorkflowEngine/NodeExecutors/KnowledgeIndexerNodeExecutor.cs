using System.Text.Json;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services.AiPlatform;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 知识写入节点：创建知识文档并触发文档处理。
/// </summary>
public sealed class KnowledgeIndexerNodeExecutor : INodeExecutor
{
    private readonly KnowledgeDocumentRepository _knowledgeDocumentRepository;
    private readonly DocumentProcessingService _documentProcessingService;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public KnowledgeIndexerNodeExecutor(
        KnowledgeDocumentRepository knowledgeDocumentRepository,
        DocumentProcessingService documentProcessingService,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _knowledgeDocumentRepository = knowledgeDocumentRepository;
        _documentProcessingService = documentProcessingService;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.KnowledgeIndexer;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var knowledgeId = context.GetConfigInt64("knowledgeId", 0L);
        if (knowledgeId <= 0)
        {
            return new NodeExecutionResult(false, outputs, "KnowledgeIndexer 缺少 knowledgeId。");
        }

        var fileId = context.GetConfigInt64("fileId", 0L);
        if (fileId <= 0)
        {
            return new NodeExecutionResult(false, outputs, "KnowledgeIndexer 当前仅支持 fileId 导入。");
        }

        var fileName = context.GetConfigString("fileName", $"doc-{fileId}.txt");
        var contentType = context.GetConfigString("contentType", "text/plain");
        var fileSizeBytes = context.GetConfigInt64("fileSizeBytes", 0L);
        var document = new KnowledgeDocument(
            context.TenantId,
            knowledgeId,
            fileId,
            fileName,
            contentType,
            Math.Max(0L, fileSizeBytes),
            _idGeneratorAccessor.NextId());
        await _knowledgeDocumentRepository.AddAsync(document, cancellationToken);

        var chunkSize = Math.Clamp(context.GetConfigInt32("chunkSize", 500), 100, 5000);
        var overlap = Math.Clamp(context.GetConfigInt32("overlap", 50), 0, 1000);
        await _documentProcessingService.ProcessAsync(
            context.TenantId,
            knowledgeId,
            document.Id,
            new ChunkingOptions(chunkSize, overlap, ChunkingStrategy.Fixed),
            cancellationToken);

        outputs["document_id"] = JsonSerializer.SerializeToElement(document.Id);
        outputs["knowledge_id"] = JsonSerializer.SerializeToElement(knowledgeId);
        outputs["status"] = VariableResolver.CreateStringElement("indexed");
        return new NodeExecutionResult(true, outputs);
    }
}
