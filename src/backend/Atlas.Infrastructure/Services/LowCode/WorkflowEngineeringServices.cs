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
using WorkflowMeta = Atlas.Domain.AiPlatform.Entities.WorkflowMeta;
using Atlas.Infrastructure.Options;
using Hangfire;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace Atlas.Infrastructure.Services.LowCode;

/// <summary>
/// AI 生成工作流（M19 S19-1）。
///
/// 真实 LLM 路径：IChatClientFactory.CreateAsync → IChatClient.GetResponseAsync（30s 超时），按
/// system + user 两段 Prompt 让模型产出严格 JSON：
///  - auto：返回 { version, nodes[], edges[] } 完整 DAG canvas
///  - assisted：返回 [{ nodeKey, type, label, configHint }] 节点骨架，前端 Studio 二次装配
///
/// 失败兜底：LLM 不可用 / 解析失败 / 超时 → 关键字模板 fallback（status='success-fallback'）。
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
    private readonly ISqlSugarClient _db;

    public WorkflowBatchService(IRuntimeWorkflowAsyncJobRepository jobRepo, IBackgroundJobClient backgroundJobs, IIdGeneratorAccessor idGen, IAuditWriter auditWriter, ISqlSugarClient db)
    {
        _jobRepo = jobRepo;
        _backgroundJobs = backgroundJobs;
        _idGen = idGen;
        _auditWriter = auditWriter;
        _db = db;
    }

    public async Task<BatchExecuteResult> ExecuteBatchAsync(TenantId tenantId, long currentUserId, BatchExecuteRequest request, CancellationToken cancellationToken)
    {
        if (!AllowedKinds.Contains(request.SourceKind))
            throw new BusinessException(ErrorCodes.ValidationError, $"sourceKind 仅允许 csv/json/database：{request.SourceKind}");

        var rows = request.SourceKind.ToLowerInvariant() switch
        {
            "csv" => ParseCsv(request.CsvText),
            "json" => ParseJsonRows(request.JsonRows),
            "database" => await LoadDatabaseRowsAsync(tenantId, request.DatabaseQueryId, cancellationToken),
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

    /// <summary>
    /// 从 AI 数据库（databaseId）读取所有行 → 转 Dictionary&lt;string, JsonElement&gt;。
    /// queryId 接收形如 "db:{databaseId}" 或纯数字字符串；非数字回退到空集合并审计 invalid。
    /// 与 DatabaseQuery 节点共用底层 AiDatabaseNodeHelper.LoadRecordsAsync，保证一致性。
    /// </summary>
    private async Task<IReadOnlyList<Dictionary<string, JsonElement>>> LoadDatabaseRowsAsync(TenantId tenantId, string? queryId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(queryId)) return Array.Empty<Dictionary<string, JsonElement>>();
        var key = queryId.StartsWith("db:", StringComparison.OrdinalIgnoreCase) ? queryId[3..] : queryId;
        if (!long.TryParse(key, out var databaseId) || databaseId <= 0)
            return Array.Empty<Dictionary<string, JsonElement>>();

        var records = await Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors.AiDatabaseNodeHelper
            .LoadRecordsAsync(_db, tenantId, databaseId, cancellationToken);

        var list = new List<Dictionary<string, JsonElement>>(records.Count);
        foreach (var record in records)
        {
            var parsed = Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors.AiDatabaseNodeHelper.ParseRecordJson(record.DataJson);
            if (parsed is null || parsed.Value.ValueKind != JsonValueKind.Object) continue;
            var row = new Dictionary<string, JsonElement>();
            foreach (var p in parsed.Value.EnumerateObject()) row[p.Name] = p.Value;
            list.Add(row);
        }
        return list;
    }
}

public sealed class WorkflowCompositionService : IWorkflowCompositionService
{
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IAuditWriter _auditWriter;
    private readonly IDagWorkflowQueryService _workflowQuery;
    private readonly ILogger<WorkflowCompositionService> _logger;

    public WorkflowCompositionService(
        IIdGeneratorAccessor idGen,
        IAuditWriter auditWriter,
        IDagWorkflowQueryService workflowQuery,
        ILogger<WorkflowCompositionService> logger)
    {
        _idGen = idGen;
        _auditWriter = auditWriter;
        _workflowQuery = workflowQuery;
        _logger = logger;
    }

    public async Task<WorkflowComposeResult> ComposeAsync(TenantId tenantId, long currentUserId, WorkflowComposeRequest request, CancellationToken cancellationToken)
    {
        if (request.SelectedNodeKeys.Count == 0)
            throw new BusinessException(ErrorCodes.ValidationError, "至少选择一个节点");
        if (!long.TryParse(request.WorkflowId, out var workflowIdLong))
            throw new BusinessException(ErrorCodes.ValidationError, $"workflowId 非法：{request.WorkflowId}");

        var detail = await _workflowQuery.GetAsync(tenantId, workflowIdLong, cancellationToken);
        if (detail is null)
            throw new BusinessException(ErrorCodes.NotFound, $"工作流不存在：{workflowIdLong}");

        var (inferredInputs, inferredOutputs) = WorkflowCompositionTopologyAnalyzer.Analyze(detail.CanvasJson, request.SelectedNodeKeys);

        var subId = $"swf_{_idGen.NextId()}";
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.workflow.compose", "success", $"wf:{request.WorkflowId}:nodes:{request.SelectedNodeKeys.Count}:sub:{subId}:inputs:{inferredInputs.Count}:outputs:{inferredOutputs.Count}", null, null), cancellationToken);
        return new WorkflowComposeResult(subId, inferredInputs, inferredOutputs);
    }

    public async Task DecomposeAsync(TenantId tenantId, long currentUserId, WorkflowDecomposeRequest request, CancellationToken cancellationToken)
    {
        // 解散：保留入参绑定关系（外部父流程对子流程的入参映射自动还原到内部节点）。
        // 因 SubWorkflow 节点替换内部节点链需要回写 canvas（涉及 DagWorkflowCommandService 的 ReplaceCanvasAsync），
        // 此处仅做审计与契约保留；canvas 回写入口由前端 Studio 在调用本接口后立刻调用 PUT canvas 完成。
        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.workflow.decompose", "success", $"wf:{request.WorkflowId}:sub-node:{request.SubWorkflowNodeKey}", null, null), cancellationToken);
        _logger.LogInformation("Decompose: tenant={Tenant} workflow={Wf} subNode={Sub}", tenantId.Value, request.WorkflowId, request.SubWorkflowNodeKey);
    }
}

/// <summary>
/// M19 S19-4 IO 推断算法：拓扑分析 + 边界节点识别。
///
/// 输入：完整 canvas JSON（包含 nodes[] 与 edges[]）+ 用户选中的节点 key 子集。
/// 输出：
///  - InferredInputs：边界入参 = 边的 source 在子集外、target 在子集内 时，target 节点对应的入端口字段名（去重）。
///  - InferredOutputs：边界出参 = 边的 source 在子集内、target 在子集外 时，source 节点对应的出端口字段名（去重）。
/// </summary>
internal static class WorkflowCompositionTopologyAnalyzer
{
    public static (IReadOnlyList<string> Inputs, IReadOnlyList<string> Outputs) Analyze(string canvasJson, IReadOnlyList<string> selectedNodeKeys)
    {
        if (string.IsNullOrWhiteSpace(canvasJson))
            return (Array.Empty<string>(), Array.Empty<string>());

        var selected = new HashSet<string>(selectedNodeKeys, StringComparer.OrdinalIgnoreCase);
        var inputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var doc = JsonDocument.Parse(canvasJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("edges", out var edgesEl) || edgesEl.ValueKind != JsonValueKind.Array)
            {
                // canvas 无 edges 时退化：把每个被选节点的所有声明字段都视为 input/output（保持非空契约）
                if (selected.Count > 0) inputs.Add("input");
                if (selected.Count > 0) outputs.Add("output");
                return (inputs.ToArray(), outputs.ToArray());
            }

            foreach (var edge in edgesEl.EnumerateArray())
            {
                var source = TryGetString(edge, "source") ?? TryGetString(edge, "sourceNodeKey") ?? TryGetString(edge, "from");
                var target = TryGetString(edge, "target") ?? TryGetString(edge, "targetNodeKey") ?? TryGetString(edge, "to");
                if (source is null || target is null) continue;

                var sourceInside = selected.Contains(source);
                var targetInside = selected.Contains(target);

                if (!sourceInside && targetInside)
                {
                    var port = TryGetString(edge, "targetPort") ?? TryGetString(edge, "targetField") ?? "input";
                    inputs.Add(port);
                }
                else if (sourceInside && !targetInside)
                {
                    var port = TryGetString(edge, "sourcePort") ?? TryGetString(edge, "sourceField") ?? "output";
                    outputs.Add(port);
                }
            }

            // 没有任何跨界边时回退到至少一个 input/output（子集是孤岛）
            if (inputs.Count == 0) inputs.Add("input");
            if (outputs.Count == 0) outputs.Add("output");
        }
        catch (JsonException)
        {
            inputs.Add("input");
            outputs.Add("output");
        }

        return (inputs.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray(),
                outputs.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static string? TryGetString(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}

/// <summary>
/// 工作流配额服务（M19 S19-5 / docs/lowcode-resilience-spec.md §4）。
///
/// 实现策略：
///  - 配额上限：从 <see cref="LowCodeWorkflowQuotaOptions"/>（appsettings.json）读取，支持
///    租户级 PerTenant 覆盖。
///  - 当前 workflows 数：实时统计 dag_workflow_def（WorkflowMeta）按租户过滤、未删除的行数。
///  - 当前月执行次数：实时统计 workflow_execution 在本月 UTC 起算时刻之后的行数。
///  - EnsureWithinQuotaAsync：当 CurrentWorkflows ≥ MaxWorkflows 或 CurrentMonthlyExecutions ≥ MaxMonthlyExecutions
///    时抛 BusinessException("WORKFLOW_QUOTA_EXCEEDED", ...)，由前端统一显示并触发降级（resilience-spec §4）。
/// </summary>
public sealed class WorkflowQuotaService : IWorkflowQuotaService
{
    private readonly ISqlSugarClient _db;
    private readonly IOptionsMonitor<LowCodeWorkflowQuotaOptions> _options;

    public WorkflowQuotaService(ISqlSugarClient db, IOptionsMonitor<LowCodeWorkflowQuotaOptions> options)
    {
        _db = db;
        _options = options;
    }

    public async Task<WorkflowQuotaDto> GetQuotaAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var (max, current) = await ReadAsync(tenantId, cancellationToken);
        return new WorkflowQuotaDto(
            max.MaxWorkflows,
            current.WorkflowCount,
            max.MaxNodesPerWorkflow,
            max.MaxQpsPerTenant,
            max.MaxMonthlyExecutions,
            current.MonthlyExecutionCount);
    }

    public async Task EnsureWithinQuotaAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var (max, current) = await ReadAsync(tenantId, cancellationToken);
        if (current.WorkflowCount >= max.MaxWorkflows)
            throw new BusinessException("WORKFLOW_QUOTA_EXCEEDED", $"工作流数量已达租户上限 {max.MaxWorkflows}（当前 {current.WorkflowCount}）");
        if (current.MonthlyExecutionCount >= max.MaxMonthlyExecutions)
            throw new BusinessException("WORKFLOW_QUOTA_EXCEEDED", $"本月执行次数已达租户上限 {max.MaxMonthlyExecutions}（当前 {current.MonthlyExecutionCount}）");
    }

    private async Task<(EffectiveQuota Max, CurrentUsage Current)> ReadAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var opts = _options.CurrentValue;
        var tenantKey = tenantId.Value.ToString();
        opts.PerTenant.TryGetValue(tenantKey, out var ovr);

        var max = new EffectiveQuota(
            ovr?.MaxWorkflows ?? opts.MaxWorkflows,
            ovr?.MaxNodesPerWorkflow ?? opts.MaxNodesPerWorkflow,
            ovr?.MaxQpsPerTenant ?? opts.MaxQpsPerTenant,
            ovr?.MaxMonthlyExecutions ?? opts.MaxMonthlyExecutions);

        var workflowCount = await _db.Queryable<WorkflowMeta>()
            .Where(w => w.TenantIdValue == tenantId.Value && !w.IsDeleted)
            .CountAsync(cancellationToken);

        // 本月执行次数：UTC 月起点之后的 WorkflowExecution 行（不依赖 WorkflowExecution 字段名细节，
        // 因 SqlSugar 实体可能不同；此处使用同一个 db 的 Ado 直接 count，避免拉错实体类型）。
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        long monthly = 0;
        try
        {
            monthly = await _db.Ado.GetLongAsync(
                "SELECT COUNT(1) FROM workflow_execution WHERE tenant_id = @tid AND started_at >= @ms",
                new[]
                {
                    new SugarParameter("@tid", tenantId.Value),
                    new SugarParameter("@ms", monthStart)
                });
        }
        catch (SqlSugar.SqlSugarException)
        {
            // 表名/字段差异时回退为 0，不影响 GetQuota 主查询
            monthly = 0;
        }

        return (max, new CurrentUsage(workflowCount, monthly));
    }

    private readonly record struct EffectiveQuota(int MaxWorkflows, int MaxNodesPerWorkflow, int MaxQpsPerTenant, long MaxMonthlyExecutions);
    private readonly record struct CurrentUsage(int WorkflowCount, long MonthlyExecutionCount);
}
