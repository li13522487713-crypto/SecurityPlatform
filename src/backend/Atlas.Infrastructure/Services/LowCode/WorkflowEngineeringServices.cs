using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Hangfire;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.LowCode;

/// <summary>
/// AI 生成工作流（M19 S19-1）。
///
/// M19 阶段：在不引入 LLM 真实调用的前提下，提供"模板生成器"：
///  - auto：基于 prompt 关键字 + base canvas 模板生成最简 Entry → Llm → Exit 三节点 canvas；
///  - assisted：把 prompt 切词为节点骨架候选，由前端 Studio 进一步装配。
///
/// 真实 LLM 接入由现有 ModelRegistry 在后续模型对接里替换；接口与 DTO 已稳定。
/// </summary>
public sealed class WorkflowGenerationService : IWorkflowGenerationService
{
    private static readonly HashSet<string> AllowedModes = new(StringComparer.OrdinalIgnoreCase) { "auto", "assisted" };

    private readonly IAuditWriter _auditWriter;
    private readonly IChatClientFactory _chatClientFactory;
    private readonly ILogger<WorkflowGenerationService> _logger;

    public WorkflowGenerationService(IAuditWriter auditWriter, IChatClientFactory chatClientFactory, ILogger<WorkflowGenerationService> logger)
    {
        _auditWriter = auditWriter;
        _chatClientFactory = chatClientFactory;
        _logger = logger;
    }

    public async Task<WorkflowGenerationResult> GenerateAsync(TenantId tenantId, long currentUserId, WorkflowGenerationRequest request, CancellationToken cancellationToken)
    {
        if (!AllowedModes.Contains(request.Mode))
            throw new BusinessException(ErrorCodes.ValidationError, $"mode 仅允许 auto / assisted：{request.Mode}");
        if (string.IsNullOrWhiteSpace(request.Prompt))
            throw new BusinessException(ErrorCodes.ValidationError, "prompt 不可为空");

        // 优先尝试 LLM 真实生成；失败时回退到模板/关键字推断（保证无 LLM 配置时可用）。
        var (canvas, nodes, usedLlm) = await TryGenerateWithLlmAsync(tenantId, request, cancellationToken);
        var status = usedLlm ? "success" : "success-fallback";

        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), $"lowcode.workflow.generate.{request.Mode}", "success", $"prompt-bytes:{request.Prompt.Length}:llm:{usedLlm}", null, null), cancellationToken);

        if (string.Equals(request.Mode, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return new WorkflowGenerationResult("auto", status, canvas, null, null);
        }
        // assisted：优先用 LLM 推断；否则关键字 fallback
        var skeleton = nodes ?? ExtractAssistedSkeleton(request.Prompt);
        return new WorkflowGenerationResult("assisted", status, null, skeleton, null);
    }

    /// <summary>
    /// 调用 IChatClientFactory.CreateAsync 获取租户默认 LLM 模型；用 system + user 两段式 Prompt 让模型产出 canvas / nodes JSON。
    /// 任何阶段出错或解析失败 → 返回 (templateCanvas, null, usedLlm=false)，调用方走关键字 fallback。
    /// </summary>
    private async Task<(string Canvas, IReadOnlyList<GeneratedNodeSkeleton>? Nodes, bool UsedLlm)> TryGenerateWithLlmAsync(TenantId tenantId, WorkflowGenerationRequest request, CancellationToken cancellationToken)
    {
        // 默认 fallback canvas（与原 M19 模板一致），便于回退场景。
        var fallbackCanvas = JsonSerializer.Serialize(new
        {
            version = "1.0",
            nodes = new object[]
            {
                new { key = "entry", type = "Entry" },
                new { key = "llm-1", type = "Llm", config = new { systemPrompt = request.Prompt } },
                new { key = "exit", type = "Exit" }
            },
            edges = new object[]
            {
                new { from = "entry", to = "llm-1" },
                new { from = "llm-1", to = "exit" }
            }
        });

        IChatClient? client;
        try
        {
            client = await _chatClientFactory.CreateAsync(tenantId, modelConfigId: null, modelName: null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "WorkflowGenerationService 无可用 LLM，回退模板/关键字");
            return (fallbackCanvas, null, false);
        }
        if (client is null) return (fallbackCanvas, null, false);

        var systemPrompt = string.Equals(request.Mode, "auto", StringComparison.OrdinalIgnoreCase)
            ? "你是 Atlas 低代码工作流生成助手。仅输出严格的 JSON，不要带任何 Markdown 代码块或解释文本。\n输出 schema：{\"version\":\"1.0\",\"nodes\":[{\"key\":string,\"type\":string,\"config\":object?}],\"edges\":[{\"from\":string,\"to\":string}]}\n节点 type 限定：Entry / Exit / Llm / KnowledgeRetriever / DatabaseQuery / HttpRequester / TextProcessor / Selector / SubWorkflow / Plugin。\n必须包含 Entry 与 Exit 节点；至少 3 个节点；edges 必须连接所有节点。"
            : "你是 Atlas 低代码工作流生成助手。仅输出 JSON 数组：[{\"nodeKey\":string,\"type\":string,\"label\":string,\"configHint\":object?}]\n节点 type 限定：Entry / Exit / Llm / KnowledgeRetriever / DatabaseQuery / HttpRequester / TextProcessor / Selector / SubWorkflow / Plugin。\n必须含 Entry 起点与 Exit 终点；3-8 个节点；不输出 Markdown。";
        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, request.Prompt)
            };
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            var response = await client.GetResponseAsync(messages, options: null, cancellationToken: timeout.Token);
            var raw = response?.Text ?? string.Empty;
            var json = ExtractJson(raw);
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("LLM 返回空 / 不含 JSON，回退模板");
                return (fallbackCanvas, null, false);
            }
            if (string.Equals(request.Mode, "auto", StringComparison.OrdinalIgnoreCase))
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("nodes", out _))
                {
                    return (fallbackCanvas, null, false);
                }
                return (json, null, true);
            }
            else
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return (fallbackCanvas, null, false);
                }
                var list = new List<GeneratedNodeSkeleton>();
                foreach (var n in doc.RootElement.EnumerateArray())
                {
                    var key = n.TryGetProperty("nodeKey", out var k) ? k.GetString() ?? string.Empty : string.Empty;
                    var type = n.TryGetProperty("type", out var t) ? t.GetString() ?? "TextProcessor" : "TextProcessor";
                    var label = n.TryGetProperty("label", out var l) ? l.GetString() ?? type : type;
                    Dictionary<string, JsonElement>? hint = null;
                    if (n.TryGetProperty("configHint", out var h) && h.ValueKind == JsonValueKind.Object)
                    {
                        hint = new Dictionary<string, JsonElement>();
                        foreach (var p in h.EnumerateObject()) hint[p.Name] = p.Value;
                    }
                    list.Add(new GeneratedNodeSkeleton(string.IsNullOrWhiteSpace(key) ? $"node-{list.Count + 1}" : key, type, label, hint));
                }
                return (fallbackCanvas, list, true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM 生成失败，回退模板");
            return (fallbackCanvas, null, false);
        }
    }

    /// <summary>从 LLM 输出中抽取 JSON 子串（容错 ```json ... ``` Markdown）。</summary>
    private static string ExtractJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var s = raw.Trim();
        // 去 Markdown 代码块包裹
        if (s.StartsWith("```"))
        {
            var first = s.IndexOf('\n');
            var last = s.LastIndexOf("```", StringComparison.Ordinal);
            if (first > 0 && last > first)
            {
                s = s[(first + 1)..last].Trim();
            }
        }
        // 找首个 { 或 [ 起始的 JSON 块
        var startObj = s.IndexOf('{');
        var startArr = s.IndexOf('[');
        var start = startObj < 0 ? startArr : (startArr < 0 ? startObj : Math.Min(startObj, startArr));
        if (start < 0) return string.Empty;
        var endObj = s.LastIndexOf('}');
        var endArr = s.LastIndexOf(']');
        var end = Math.Max(endObj, endArr);
        if (end <= start) return string.Empty;
        return s.Substring(start, end - start + 1);
    }

    private static IReadOnlyList<GeneratedNodeSkeleton> ExtractAssistedSkeleton(string prompt)
    {
        var keywords = prompt
            .Split(new[] { '。', '；', '\n', '\r', ',', '、' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Take(8)
            .ToList();
        var list = new List<GeneratedNodeSkeleton> { new("entry", "Entry", "开始", null) };
        for (var i = 0; i < keywords.Count; i++)
        {
            var k = keywords[i];
            var type = k.Contains("查询") || k.Contains("搜索") ? "KnowledgeRetriever"
                : k.Contains("数据库") ? "DatabaseQuery"
                : k.Contains("LLM") || k.Contains("大模型") || k.Contains("生成") ? "Llm"
                : k.Contains("HTTP") || k.Contains("调用") ? "HttpRequester"
                : "TextProcessor";
            list.Add(new GeneratedNodeSkeleton($"node-{i + 1}", type, k, null));
        }
        list.Add(new GeneratedNodeSkeleton("exit", "Exit", "结束", null));
        return list;
    }
}

public sealed class WorkflowBatchService : IWorkflowBatchService
{
    private static readonly HashSet<string> AllowedKinds = new(StringComparer.OrdinalIgnoreCase) { "csv", "json", "database" };

    private readonly IRuntimeWorkflowAsyncJobRepository _jobRepo;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IAuditWriter _auditWriter;

    public WorkflowBatchService(IRuntimeWorkflowAsyncJobRepository jobRepo, IBackgroundJobClient backgroundJobs, IIdGeneratorAccessor idGen, IAuditWriter auditWriter)
    {
        _jobRepo = jobRepo;
        _backgroundJobs = backgroundJobs;
        _idGen = idGen;
        _auditWriter = auditWriter;
    }

    public async Task<BatchExecuteResult> ExecuteBatchAsync(TenantId tenantId, long currentUserId, BatchExecuteRequest request, CancellationToken cancellationToken)
    {
        if (!AllowedKinds.Contains(request.SourceKind))
            throw new BusinessException(ErrorCodes.ValidationError, $"sourceKind 仅允许 csv/json/database：{request.SourceKind}");

        var rows = request.SourceKind.ToLowerInvariant() switch
        {
            "csv" => ParseCsv(request.CsvText),
            "json" => ParseJsonRows(request.JsonRows),
            "database" => ParseDatabaseStub(request.DatabaseQueryId),
            _ => Array.Empty<Dictionary<string, JsonElement>>()
        };

        // M19 收尾：批量执行 Hangfire 持久化任务（替代同步循环）；客户端通过 GET /async-jobs/{jobId} 轮询进度。
        var jobId = $"bwj_{_idGen.NextId()}";
        var batchReq = new RuntimeWorkflowBatchInvokeRequest(request.WorkflowId, rows, request.OnFailure ?? "continue", null, null);
        var batchRequestJson = JsonSerializer.Serialize(batchReq);
        var entity = new Atlas.Domain.LowCode.Entities.RuntimeWorkflowAsyncJob(tenantId, _idGen.NextId(), jobId, request.WorkflowId, batchRequestJson, currentUserId);
        await _jobRepo.InsertAsync(entity, cancellationToken);
        _backgroundJobs.Enqueue<RuntimeWorkflowBackgroundJob>(job => job.RunBatchJobAsync(tenantId.Value, currentUserId, jobId, batchRequestJson));
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.workflow.batch.submit", "success", $"wf:{request.WorkflowId}:source:{request.SourceKind}:total:{rows.Count}:job:{jobId}", null, null), cancellationToken);
        return new BatchExecuteResult(jobId, rows.Count, 0, 0);
    }

    private static IReadOnlyList<Dictionary<string, JsonElement>> ParseCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return Array.Empty<Dictionary<string, JsonElement>>();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToArray();
        if (lines.Length < 2) return Array.Empty<Dictionary<string, JsonElement>>();
        var header = lines[0].Split(',');
        var list = new List<Dictionary<string, JsonElement>>(lines.Length - 1);
        for (var i = 1; i < lines.Length; i++)
        {
            var cols = lines[i].Split(',');
            var row = new Dictionary<string, JsonElement>();
            for (var j = 0; j < header.Length && j < cols.Length; j++)
            {
                row[header[j].Trim()] = JsonSerializer.SerializeToElement(cols[j].Trim());
            }
            list.Add(row);
        }
        return list;
    }

    private static IReadOnlyList<Dictionary<string, JsonElement>> ParseJsonRows(JsonElement? rows)
    {
        if (rows is null || rows.Value.ValueKind != JsonValueKind.Array) return Array.Empty<Dictionary<string, JsonElement>>();
        var list = new List<Dictionary<string, JsonElement>>();
        foreach (var item in rows.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var dict = new Dictionary<string, JsonElement>();
            foreach (var p in item.EnumerateObject()) dict[p.Name] = p.Value;
            list.Add(dict);
        }
        return list;
    }

    private static IReadOnlyList<Dictionary<string, JsonElement>> ParseDatabaseStub(string? queryId)
    {
        // M19 简化：数据库查询走 mock；M19 后续接入 IRuntimeDataSourceConnector 后真实查询。
        if (string.IsNullOrWhiteSpace(queryId)) return Array.Empty<Dictionary<string, JsonElement>>();
        return new[]
        {
            new Dictionary<string, JsonElement> { ["row"] = JsonSerializer.SerializeToElement(1) },
            new Dictionary<string, JsonElement> { ["row"] = JsonSerializer.SerializeToElement(2) }
        };
    }
}

public sealed class WorkflowCompositionService : IWorkflowCompositionService
{
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IAuditWriter _auditWriter;

    public WorkflowCompositionService(IIdGeneratorAccessor idGen, IAuditWriter auditWriter)
    {
        _idGen = idGen;
        _auditWriter = auditWriter;
    }

    public async Task<WorkflowComposeResult> ComposeAsync(TenantId tenantId, long currentUserId, WorkflowComposeRequest request, CancellationToken cancellationToken)
    {
        if (request.SelectedNodeKeys.Count == 0)
            throw new BusinessException(ErrorCodes.ValidationError, "至少选择一个节点");

        var subId = $"swf_{_idGen.NextId()}";
        // M19 阶段：IO 推断算法在 DagWorkflowQueryService 上获取 canvas 后由 WorkflowCompositionTopologyAnalyzer 完成；
        // 当前为占位实现：把 selected nodes 视为黑盒，产出固定 input/output。
        var inferredInputs = new[] { "input" };
        var inferredOutputs = new[] { "output" };

        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.workflow.compose", "success", $"wf:{request.WorkflowId}:nodes:{request.SelectedNodeKeys.Count}:sub:{subId}", null, null), cancellationToken);
        return new WorkflowComposeResult(subId, inferredInputs, inferredOutputs);
    }

    public async Task DecomposeAsync(TenantId tenantId, long currentUserId, WorkflowDecomposeRequest request, CancellationToken cancellationToken)
    {
        // M19 阶段：解散为占位（将 SubWorkflow 节点替换回内部子节点链由 Studio 渲染）。
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.workflow.decompose", "success", $"wf:{request.WorkflowId}:sub-node:{request.SubWorkflowNodeKey}", null, null), cancellationToken);
    }
}

public sealed class WorkflowQuotaService : IWorkflowQuotaService
{
    // M19 简化：所有租户共享默认配额；M19 后续接入 IConfiguration / 租户级配额表。
    private const int DefaultMaxWorkflows = 200;
    private const int DefaultMaxNodesPerWorkflow = 100;
    private const int DefaultMaxQps = 10;
    private const long DefaultMaxMonthlyExecutions = 100_000;

    public Task<WorkflowQuotaDto> GetQuotaAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        _ = tenantId;
        return Task.FromResult(new WorkflowQuotaDto(DefaultMaxWorkflows, 0, DefaultMaxNodesPerWorkflow, DefaultMaxQps, DefaultMaxMonthlyExecutions, 0));
    }

    public Task EnsureWithinQuotaAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        _ = tenantId;
        // M19 阶段不做实时配额校验；接口已稳定，配额耗尽降级策略由 docs/lowcode-resilience-spec.md §4 落实。
        return Task.CompletedTask;
    }
}
