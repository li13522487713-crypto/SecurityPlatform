using System.Globalization;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 知识检索节点：调用 RAG 检索服务并输出结果列表。
/// </summary>
public sealed class KnowledgeRetrieverNodeExecutor : INodeExecutor
{
    private readonly IRagRetrievalService _ragRetrievalService;

    public KnowledgeRetrieverNodeExecutor(IRagRetrievalService ragRetrievalService)
    {
        _ragRetrievalService = ragRetrievalService;
    }

    public WorkflowNodeType NodeType => WorkflowNodeType.KnowledgeRetriever;

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var knowledgeIds = ResolveKnowledgeIds(context.Node.Config);
        if (knowledgeIds.Count == 0)
        {
            outputs["documents"] = JsonSerializer.SerializeToElement(Array.Empty<object>());
            return new NodeExecutionResult(true, outputs);
        }

        using var activity = AiNodeObservability.StartNodeActivity(
            "Knowledge.Retrieve",
            context.TenantId,
            context.UserId,
            context.ChannelId,
            context.Node.Key,
            new Dictionary<string, object?>
            {
                ["kb.ids"] = string.Join(',', knowledgeIds),
                ["kb.count"] = knowledgeIds.Count
            });

        var queryTemplate = context.GetConfigString("query", "{{query}}");
        var query = context.ReplaceVariables(queryTemplate).Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return new NodeExecutionResult(false, outputs, "KnowledgeRetriever 查询语句为空。");
        }

        var topK = Math.Clamp(context.GetConfigInt32("topK", 5), 1, 50);
        var minScore = ResolveMinScore(context.Node.Config);
        var filter = new RagRetrievalFilter(
            Tags: ResolveTags(context.Node.Config),
            MinScore: minScore > 0 ? minScore : null,
            Offset: 0,
            OwnerFilter: ResolveOwnerFilter(context.Node.Config),
            MetadataFilter: null);
        var results = await _ragRetrievalService.SearchAsync(
            context.TenantId,
            knowledgeIds,
            query,
            topK,
            filter,
            cancellationToken);

        var filtered = results.OrderByDescending(x => x.Score).ToList();
        var documentsJson = JsonSerializer.SerializeToElement(filtered);
        // X2：默认对检索文档应用脱敏；可通过 config.maskSensitive=false 关闭。
        var maskSensitive = context.GetConfigBoolean("maskSensitive", true);
        outputs["documents"] = maskSensitive ? AiNodeObservability.Mask(documentsJson) : documentsJson;
        outputs["retrieved_count"] = JsonSerializer.SerializeToElement(filtered.Count);
        outputs["query"] = VariableResolver.CreateStringElement(query);
        activity?.SetTag("kb.retrieved_count", filtered.Count);
        await AiNodeObservability.WriteAuditAsync(
            context.ServiceProvider,
            context.TenantId,
            context.UserId,
            "knowledge_node.retrieve",
            "success",
            $"kb:[{string.Join(',', knowledgeIds)}]/hits:{filtered.Count}/node:{context.Node.Key}",
            cancellationToken);
        return new NodeExecutionResult(true, outputs);
    }

    private static List<long> ResolveKnowledgeIds(IReadOnlyDictionary<string, JsonElement> config)
    {
        if (!VariableResolver.TryGetConfigValue(config, "knowledgeIds", out var raw))
        {
            return [];
        }

        if (raw.ValueKind == JsonValueKind.Array)
        {
            return raw.EnumerateArray()
                .Select(VariableResolver.ToDisplayText)
                .Select(x => long.TryParse(x, out var id) ? id : 0)
                .Where(x => x > 0)
                .Distinct()
                .ToList();
        }

        var text = VariableResolver.ToDisplayText(raw);
        return text
            .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => long.TryParse(x, out var id) ? id : 0)
            .Where(x => x > 0)
            .Distinct()
            .ToList();
    }

    private static float ResolveMinScore(IReadOnlyDictionary<string, JsonElement> config)
    {
        if (!VariableResolver.TryGetConfigValue(config, "minScore", out var raw))
        {
            return 0f;
        }

        return float.TryParse(VariableResolver.ToDisplayText(raw), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? Math.Clamp(value, 0f, 1f)
            : 0f;
    }

    private static IReadOnlyList<string>? ResolveTags(IReadOnlyDictionary<string, JsonElement> config)
    {
        if (!VariableResolver.TryGetConfigValue(config, "tags", out var raw) || raw.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var list = raw.EnumerateArray()
            .Select(VariableResolver.ToDisplayText)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToArray();
        return list.Length == 0 ? null : list;
    }

    private static string? ResolveOwnerFilter(IReadOnlyDictionary<string, JsonElement> config)
    {
        if (!VariableResolver.TryGetConfigValue(config, "ownerFilter", out var raw))
        {
            return null;
        }

        var text = VariableResolver.ToDisplayText(raw).Trim();
        return text.Length == 0 ? null : text;
    }
}
