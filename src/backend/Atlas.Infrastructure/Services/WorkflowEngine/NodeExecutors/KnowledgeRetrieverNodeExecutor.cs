using System.Globalization;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 知识检索节点（v5 §38 / 计划 G6）：始终走 SearchWithProfileAsync 升级版协议。
/// 支持 retrievalProfile / filters / callerContextOverride / debug 全字段；输出 key 全部 camelCase。
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
            outputs["candidates"] = JsonSerializer.SerializeToElement(Array.Empty<object>());
            outputs["finalContext"] = VariableResolver.CreateStringElement(string.Empty);
            outputs["traceId"] = VariableResolver.CreateStringElement(string.Empty);
            outputs["rewrittenQuery"] = VariableResolver.CreateStringElement(string.Empty);
            outputs["latencyMs"] = JsonSerializer.SerializeToElement(0);
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
        var debug = context.GetConfigBoolean("debug", false);
        var maskSensitive = context.GetConfigBoolean("maskSensitive", true);

        // v5 §38 / 计划 G6：解析 Filters 与 CallerContextOverride，并与默认 CallerContext 合并
        var retrievalProfile = TryParseRetrievalProfile(context.Node.Config);
        var filters = TryParseFilters(context.Node.Config);
        var defaultCaller = BuildCallerContext(context);
        var callerOverride = TryParseCallerContextOverride(context.Node.Config);
        var callerContext = MergeCallerContext(defaultCaller, callerOverride);

        var request = new RetrievalRequest(
            Query: query,
            KnowledgeBaseIds: knowledgeIds,
            TopK: topK,
            CallerContext: callerContext,
            Debug: debug,
            MinScore: minScore > 0 ? minScore : null,
            Filters: filters,
            RetrievalProfile: retrievalProfile);

        var response = await _ragRetrievalService.SearchWithProfileAsync(
            context.TenantId,
            request,
            cancellationToken);

        // 输出 key 统一 camelCase（计划 G6）
        var rerankedJson = JsonSerializer.SerializeToElement(response.Log.Reranked);
        var candidatesJson = JsonSerializer.SerializeToElement(response.Log.Candidates);
        outputs["documents"] = maskSensitive ? AiNodeObservability.Mask(rerankedJson) : rerankedJson;
        outputs["candidates"] = maskSensitive ? AiNodeObservability.Mask(candidatesJson) : candidatesJson;
        outputs["retrievedCount"] = JsonSerializer.SerializeToElement(response.Log.Reranked.Count);
        outputs["query"] = VariableResolver.CreateStringElement(query);
        outputs["rewrittenQuery"] = VariableResolver.CreateStringElement(response.Log.RewrittenQuery ?? query);
        outputs["finalContext"] = VariableResolver.CreateStringElement(response.Log.FinalContext);
        outputs["traceId"] = VariableResolver.CreateStringElement(response.Log.TraceId);
        outputs["latencyMs"] = JsonSerializer.SerializeToElement(response.Log.LatencyMs);
        outputs["embeddingModel"] = VariableResolver.CreateStringElement(response.Log.EmbeddingModel);
        outputs["vectorStore"] = VariableResolver.CreateStringElement(response.Log.VectorStore);

        // 兼容旧画布：保留 snake_case 别名（一个版本，下个版本删除）
        outputs["retrieved_count"] = outputs["retrievedCount"];
        outputs["rewritten_query"] = outputs["rewrittenQuery"];
        outputs["final_context"] = outputs["finalContext"];
        outputs["trace_id"] = outputs["traceId"];

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

    /// <summary>
    /// v5 §38 / 计划 G6：解析 filters 配置 → Dictionary&lt;string,string&gt;。
    /// 支持两种 JSON 形态：
    /// - 对象 {"tag":"security","namespace":"prod"} → 直接转字典
    /// - 数组 [{"key":"tag","value":"security"}, ...] → 折叠为字典
    /// </summary>
    private static IReadOnlyDictionary<string, string>? TryParseFilters(IReadOnlyDictionary<string, JsonElement> config)
    {
        if (!VariableResolver.TryGetConfigValue(config, "filters", out var raw))
        {
            return null;
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (raw.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in raw.EnumerateObject())
            {
                var value = VariableResolver.ToDisplayText(prop.Value);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    result[prop.Name] = value;
                }
            }
        }
        else if (raw.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in raw.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                if (!item.TryGetProperty("key", out var keyEl)) continue;
                var key = VariableResolver.ToDisplayText(keyEl);
                if (string.IsNullOrWhiteSpace(key)) continue;
                var value = item.TryGetProperty("value", out var valueEl)
                    ? VariableResolver.ToDisplayText(valueEl)
                    : string.Empty;
                result[key] = value;
            }
        }
        return result.Count == 0 ? null : result;
    }

    /// <summary>
    /// v5 §38 / 计划 G6：解析 callerContextOverride 配置；与默认 CallerContext 合并（用户字段优先）。
    /// </summary>
    private static RetrievalCallerContext? TryParseCallerContextOverride(IReadOnlyDictionary<string, JsonElement> config)
    {
        if (!VariableResolver.TryGetConfigValue(config, "callerContextOverride", out var raw))
        {
            return null;
        }
        if (raw.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize<RetrievalCallerContext>(raw.GetRawText(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static RetrievalCallerContext MergeCallerContext(
        RetrievalCallerContext defaultContext,
        RetrievalCallerContext? overrideContext)
    {
        if (overrideContext is null) return defaultContext;
        return defaultContext with
        {
            CallerType = overrideContext.CallerType, // CallerType 必填，直接覆盖
            CallerId = string.IsNullOrWhiteSpace(overrideContext.CallerId) ? defaultContext.CallerId : overrideContext.CallerId,
            CallerName = string.IsNullOrWhiteSpace(overrideContext.CallerName) ? defaultContext.CallerName : overrideContext.CallerName,
            ConversationId = overrideContext.ConversationId ?? defaultContext.ConversationId,
            WorkflowTraceId = overrideContext.WorkflowTraceId ?? defaultContext.WorkflowTraceId,
            PageId = overrideContext.PageId ?? defaultContext.PageId,
            ComponentId = overrideContext.ComponentId ?? defaultContext.ComponentId,
            TenantId = overrideContext.TenantId ?? defaultContext.TenantId,
            UserId = overrideContext.UserId ?? defaultContext.UserId,
            Preset = overrideContext.Preset ?? defaultContext.Preset,
        };
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
            UserId: context.UserId.ToString(),
            Preset: RetrievalCallerPreset.WorkflowDebug);
    }
}
