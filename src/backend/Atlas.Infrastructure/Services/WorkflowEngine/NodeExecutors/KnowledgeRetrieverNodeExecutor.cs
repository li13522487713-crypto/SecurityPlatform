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

        // v5 §38：如果 config 提供了 retrievalProfile / callerContext / debug，则走升级版协议；
        // 否则保留旧 SearchAsync 行为，确保既有画布零侵入。
        var retrievalProfile = TryParseRetrievalProfile(context.Node.Config);
        var debug = context.GetConfigBoolean("debug", false);
        var maskSensitive = context.GetConfigBoolean("maskSensitive", true);

        if (retrievalProfile is not null || debug)
        {
            var callerContext = BuildCallerContext(context);
            var request = new RetrievalRequest(
                Query: query,
                KnowledgeBaseIds: knowledgeIds,
                TopK: topK,
                CallerContext: callerContext,
                Debug: debug,
                MinScore: filter.MinScore,
                Filters: null,
                RetrievalProfile: retrievalProfile);
            var response = await _ragRetrievalService.SearchWithProfileAsync(
                context.TenantId,
                request,
                cancellationToken);

            var rerankedJson = JsonSerializer.SerializeToElement(response.Log.Reranked);
            outputs["documents"] = maskSensitive ? AiNodeObservability.Mask(rerankedJson) : rerankedJson;
            outputs["candidates"] = JsonSerializer.SerializeToElement(response.Log.Candidates);
            outputs["retrieved_count"] = JsonSerializer.SerializeToElement(response.Log.Reranked.Count);
            outputs["query"] = VariableResolver.CreateStringElement(query);
            outputs["rewritten_query"] = VariableResolver.CreateStringElement(response.Log.RewrittenQuery ?? query);
            outputs["final_context"] = VariableResolver.CreateStringElement(response.Log.FinalContext);
            outputs["trace_id"] = VariableResolver.CreateStringElement(response.Log.TraceId);
            activity?.SetTag("kb.retrieved_count", response.Log.Reranked.Count);
            activity?.SetTag("kb.trace_id", response.Log.TraceId);
            await AiNodeObservability.WriteAuditAsync(
                context.ServiceProvider,
                context.TenantId,
                context.UserId,
                "knowledge_node.retrieve",
                "success",
                $"kb:[{string.Join(',', knowledgeIds)}]/hits:{response.Log.Reranked.Count}/trace:{response.Log.TraceId}/node:{context.Node.Key}",
                cancellationToken);
            return new NodeExecutionResult(true, outputs);
        }

        var results = await _ragRetrievalService.SearchAsync(
            context.TenantId,
            knowledgeIds,
            query,
            topK,
            filter,
            cancellationToken);

        var filtered = results.OrderByDescending(x => x.Score).ToList();
        var documentsJson = JsonSerializer.SerializeToElement(filtered);
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

    private static RetrievalProfile? TryParseRetrievalProfile(IReadOnlyDictionary<string, JsonElement> config)
    {
        if (!VariableResolver.TryGetConfigValue(config, "retrievalProfile", out var raw))
        {
            return null;
        }
        if (raw.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RetrievalProfile>(raw.GetRawText(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static RetrievalCallerContext BuildCallerContext(NodeExecutionContext context)
    {
        return new RetrievalCallerContext(
            CallerType: KnowledgeRetrievalCallerType.Workflow,
            CallerId: context.Node.Key,
            CallerName: context.Node.Key,
            ConversationId: context.ChannelId,
            WorkflowTraceId: context.Node.Key,
            TenantId: context.TenantId.Value.ToString(),
            UserId: context.UserId.ToString());
    }
}
