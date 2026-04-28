using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions.Knowledge;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 知识写入节点（v5 §35 / 计划 G6）：创建知识文档并通过 IKnowledgeIndexJobService → Hangfire 调度索引。
/// 完整支持 ParsingStrategy / ChunkingProfile / mode (append|overwrite) 全字段。
/// </summary>
public sealed class KnowledgeIndexerNodeExecutor : INodeExecutor
{
    private readonly KnowledgeDocumentRepository _knowledgeDocumentRepository;
    private readonly IKnowledgeIndexJobService _indexJobService;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public KnowledgeIndexerNodeExecutor(
        KnowledgeDocumentRepository knowledgeDocumentRepository,
        IKnowledgeIndexJobService indexJobService,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _knowledgeDocumentRepository = knowledgeDocumentRepository;
        _indexJobService = indexJobService;
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

        using var activity = AiNodeObservability.StartNodeActivity(
            "Knowledge.Index",
            context.TenantId,
            context.UserId,
            context.ChannelId,
            context.Node.Key,
            new Dictionary<string, object?>
            {
                ["kb.id"] = knowledgeId,
                ["kb.file_id"] = fileId
            });

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

        // v5 §35 / 计划 G6：完整解析 ParsingStrategy + ChunkingProfile + mode
        var parsingStrategy = TryParseParsingStrategy(context.Node.Config);
        var chunkingProfile = TryParseChunkingProfile(context.Node.Config);
        var mode = ResolveMode(context.Node.Config);

        // 通过 IKnowledgeIndexJobService 入队 Hangfire 任务（含 overwrite 模式 GC 旧 chunks）
        var jobId = await _indexJobService.EnqueueIndexAsync(
            context.TenantId,
            knowledgeId,
            document.Id,
            chunkingProfile,
            mode,
            cancellationToken);

        outputs["documentId"] = JsonSerializer.SerializeToElement(document.Id);
        outputs["knowledgeId"] = JsonSerializer.SerializeToElement(knowledgeId);
        outputs["jobId"] = JsonSerializer.SerializeToElement(jobId);
        outputs["mode"] = VariableResolver.CreateStringElement(mode.ToString().ToLowerInvariant());
        outputs["status"] = VariableResolver.CreateStringElement("queued");
        if (parsingStrategy is not null)
        {
            outputs["parsingStrategy"] = JsonSerializer.SerializeToElement(parsingStrategy);
        }
        if (chunkingProfile is not null)
        {
            outputs["chunkingProfile"] = JsonSerializer.SerializeToElement(chunkingProfile);
        }

        // 兼容旧画布：保留 snake_case 别名一个版本
        outputs["document_id"] = outputs["documentId"];
        outputs["knowledge_id"] = outputs["knowledgeId"];
        if (parsingStrategy is not null)
        {
            outputs["parsing_strategy"] = outputs["parsingStrategy"];
        }

        await AiNodeObservability.WriteAuditAsync(
            context.ServiceProvider,
            context.TenantId,
            context.UserId,
            "knowledge_node.index",
            "success",
            $"kb:{knowledgeId}/doc:{document.Id}/file:{fileId}/job:{jobId}/mode:{mode}/node:{context.Node.Key}",
            cancellationToken);
        return new NodeExecutionResult(true, outputs);
    }

    private static ParsingStrategy? TryParseParsingStrategy(IReadOnlyDictionary<string, JsonElement> config)
    {
        if (!VariableResolver.TryGetConfigValue(config, "parsingStrategy", out var raw))
        {
            return null;
        }
        if (raw.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ParsingStrategy>(raw.GetRawText(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// v5 §35 / 计划 G6：解析 chunkingProfile 对象。
    /// 缺省时返回 null，KnowledgeIndexJobRunner 内部按 ChunkingOptions 默认值处理。
    /// </summary>
    private static ChunkingProfile? TryParseChunkingProfile(IReadOnlyDictionary<string, JsonElement> config)
    {
        if (!VariableResolver.TryGetConfigValue(config, "chunkingProfile", out var raw))
        {
            return null;
        }
        if (raw.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize<ChunkingProfile>(raw.GetRawText(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// v5 §35 / 计划 G6：解析 mode 配置。支持 "append" / "overwrite"（默认 append）。
    /// </summary>
    private static KnowledgeIndexMode ResolveMode(IReadOnlyDictionary<string, JsonElement> config)
    {
        var modeStr = "append";
        if (VariableResolver.TryGetConfigValue(config, "mode", out var raw))
        {
            modeStr = VariableResolver.ToDisplayText(raw)?.Trim().ToLowerInvariant() ?? "append";
        }
        return modeStr switch
        {
            "overwrite" => KnowledgeIndexMode.Overwrite,
            _ => KnowledgeIndexMode.Append
        };
    }
}
