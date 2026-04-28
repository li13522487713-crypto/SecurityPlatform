using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 知识删除节点：按知识库或文档删除数据。
/// Config 参数：knowledgeId、documentId（可选）
/// </summary>
public sealed class KnowledgeDeleterNodeExecutor : INodeExecutor
{
    private readonly KnowledgeDocumentRepository _knowledgeDocumentRepository;
    private readonly DocumentChunkRepository _documentChunkRepository;

    public KnowledgeDeleterNodeExecutor(
        KnowledgeDocumentRepository knowledgeDocumentRepository,
        DocumentChunkRepository documentChunkRepository)
    {
        _knowledgeDocumentRepository = knowledgeDocumentRepository;
        _documentChunkRepository = documentChunkRepository;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.KnowledgeDeleter;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var knowledgeId = context.GetConfigInt64("knowledgeId", 0L);
        if (knowledgeId <= 0)
        {
            return new NodeExecutionResult(false, outputs, "KnowledgeDeleter 缺少 knowledgeId。");
        }

        var documentId = context.GetConfigInt64("documentId", 0L);
        using var activity = AiNodeObservability.StartNodeActivity(
            "Knowledge.Delete",
            context.TenantId,
            context.UserId,
            context.ChannelId,
            context.Node.Key,
            new Dictionary<string, object?>
            {
                ["kb.id"] = knowledgeId,
                ["kb.document_id"] = documentId
            });
        if (documentId > 0)
        {
            var existing = await _knowledgeDocumentRepository.FindByKnowledgeBaseAndIdAsync(
                context.TenantId,
                knowledgeId,
                documentId,
                cancellationToken);
            if (existing is not null)
            {
                await _documentChunkRepository.DeleteByDocumentAsync(context.TenantId, documentId, cancellationToken);
                await _knowledgeDocumentRepository.DeleteAsync(existing, cancellationToken);
                outputs["deleted_document_count"] = JsonSerializer.SerializeToElement(1);
            }
            else
            {
                outputs["deleted_document_count"] = JsonSerializer.SerializeToElement(0);
            }

            outputs["deleted_scope"] = VariableResolver.CreateStringElement("document");
            outputs["knowledge_id"] = JsonSerializer.SerializeToElement(knowledgeId);
            outputs["document_id"] = JsonSerializer.SerializeToElement(documentId);
            await AiNodeObservability.WriteAuditAsync(
                context.ServiceProvider,
                context.TenantId,
                context.UserId,
                "knowledge_node.delete_document",
                "success",
                $"kb:{knowledgeId}/doc:{documentId}/node:{context.Node.Key}",
                cancellationToken);
            return new NodeExecutionResult(true, outputs);
        }

        await _documentChunkRepository.DeleteByKnowledgeBaseAsync(context.TenantId, knowledgeId, cancellationToken);
        await _knowledgeDocumentRepository.DeleteByKnowledgeBaseAsync(context.TenantId, knowledgeId, cancellationToken);
        outputs["deleted_scope"] = VariableResolver.CreateStringElement("knowledge_base");
        outputs["knowledge_id"] = JsonSerializer.SerializeToElement(knowledgeId);
        await AiNodeObservability.WriteAuditAsync(
            context.ServiceProvider,
            context.TenantId,
            context.UserId,
            "knowledge_node.delete_kb_data",
            "success",
            $"kb:{knowledgeId}/node:{context.Node.Key}",
            cancellationToken);
        return new NodeExecutionResult(true, outputs);
    }
}

