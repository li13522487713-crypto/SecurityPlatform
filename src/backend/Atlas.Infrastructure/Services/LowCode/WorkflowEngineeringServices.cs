using System.Globalization;
using System.Text;
using System.Text.Json;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;

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

    public WorkflowGenerationService(IAuditWriter auditWriter)
    {
        _auditWriter = auditWriter;
    }

    public async Task<WorkflowGenerationResult> GenerateAsync(TenantId tenantId, long currentUserId, WorkflowGenerationRequest request, CancellationToken cancellationToken)
    {
        if (!AllowedModes.Contains(request.Mode))
            throw new BusinessException(ErrorCodes.ValidationError, $"mode 仅允许 auto / assisted：{request.Mode}");
        if (string.IsNullOrWhiteSpace(request.Prompt))
            throw new BusinessException(ErrorCodes.ValidationError, "prompt 不可为空");

        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), $"lowcode.workflow.generate.{request.Mode}", "success", $"prompt-bytes:{request.Prompt.Length}", null, null), cancellationToken);

        if (string.Equals(request.Mode, "auto", StringComparison.OrdinalIgnoreCase))
        {
            var canvas = JsonSerializer.Serialize(new
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
            return new WorkflowGenerationResult("auto", "success", canvas, null, null);
        }
        // assisted
        var nodes = ExtractAssistedSkeleton(request.Prompt);
        return new WorkflowGenerationResult("assisted", "success", null, nodes, null);
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

    private readonly IRuntimeWorkflowExecutor _executor;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IAuditWriter _auditWriter;

    public WorkflowBatchService(IRuntimeWorkflowExecutor executor, IIdGeneratorAccessor idGen, IAuditWriter auditWriter)
    {
        _executor = executor;
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

        var batchReq = new RuntimeWorkflowBatchInvokeRequest(request.WorkflowId, rows, request.OnFailure ?? "continue", null, null);
        var r = await _executor.InvokeBatchAsync(tenantId, currentUserId, batchReq, cancellationToken);

        await _auditWriter.WriteAsync(new AuditRecord(tenantId, currentUserId.ToString(), "lowcode.workflow.batch", "success", $"wf:{request.WorkflowId}:source:{request.SourceKind}:total:{r.Total}", null, null), cancellationToken);
        return new BatchExecuteResult(r.JobId, r.Total, r.Succeeded, r.Failed);
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
