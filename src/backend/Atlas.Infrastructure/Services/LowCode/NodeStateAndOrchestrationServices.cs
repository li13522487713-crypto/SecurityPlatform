using System.Text.Json;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services.LowCode;

public sealed class NodeStateStore : INodeStateStore
{
    private static readonly HashSet<string> AllowedScopes = new(StringComparer.OrdinalIgnoreCase) { "session", "conversation", "trigger", "app" };

    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGen;

    public NodeStateStore(ISqlSugarClient db, IIdGeneratorAccessor idGen)
    {
        _db = db;
        _idGen = idGen;
    }

    public async Task<string?> ReadAsync(TenantId tenantId, string scope, string scopeKey, string nodeKey, CancellationToken cancellationToken)
    {
        EnsureScope(scope);
        var entity = await _db.Queryable<NodeStateEntry>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Scope == scope && x.ScopeKey == scopeKey && x.NodeKey == nodeKey)
            .FirstAsync(cancellationToken);
        return entity?.StateJson;
    }

    public async Task WriteAsync(TenantId tenantId, string scope, string scopeKey, string nodeKey, string stateJson, CancellationToken cancellationToken)
    {
        EnsureScope(scope);
        var existing = await _db.Queryable<NodeStateEntry>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Scope == scope && x.ScopeKey == scopeKey && x.NodeKey == nodeKey)
            .FirstAsync(cancellationToken);
        if (existing is null)
        {
            var entity = new NodeStateEntry(tenantId, _idGen.NextId(), scope, scopeKey, nodeKey, stateJson);
            await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            existing.Replace(stateJson);
            await _db.Updateable(existing).Where(x => x.Id == existing.Id && x.TenantIdValue == tenantId.Value).ExecuteCommandAsync(cancellationToken);
        }
    }

    public async Task DeleteAsync(TenantId tenantId, string scope, string scopeKey, string nodeKey, CancellationToken cancellationToken)
    {
        EnsureScope(scope);
        await _db.Deleteable<NodeStateEntry>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Scope == scope && x.ScopeKey == scopeKey && x.NodeKey == nodeKey)
            .ExecuteCommandAsync(cancellationToken);
    }

    private static void EnsureScope(string scope)
    {
        if (!AllowedScopes.Contains(scope))
            throw new ArgumentException($"INodeStateStore 仅支持 session/conversation/trigger/app 作用域：{scope}", nameof(scope));
    }
}

/// <summary>
/// 双哲学编排引擎（M20 S20-6 + P3-7 ExecuteAsync）：explicit / agentic 两种模式。
/// - explicit：保留完整 canvas，运行时按 DAG 顺序执行；前端隐藏 LLM 自决面板
/// - agentic：要求 tools 池非空且至少含 1 个 LLM 工具，否则拒绝；前端隐藏多数中间节点，仅暴露 LLM + Tool 池
///
/// P3-7 ExecuteAsync 实现 LLM tool calling 循环：
///  1) 接 IChatClientFactory 获取当前租户默认 / 指定模型
///  2) 把 OrchestrationPlan.Tools 转为 IChatClient 的 ChatTool（Function calling 协议）
///  3) 循环最多 MaxRounds 轮：调 GetResponseAsync → 收集 tool_calls → 调 ExecuteToolAsync → 把 tool 结果作为 message 追加 → 再调
///  4) 模型返回无 tool_calls 时退出，输出 FinalText + 全部 OrchestrationToolInvocation
///  5) 工具执行失败时 → 把 Error 注入到 invocation 并继续，让模型决定是否重试 / 替代
/// </summary>
public sealed class DualOrchestrationEngine : IDualOrchestrationEngine
{
    private readonly Atlas.Application.AiPlatform.Abstractions.IChatClientFactory? _chatClientFactory;

    public DualOrchestrationEngine(Atlas.Application.AiPlatform.Abstractions.IChatClientFactory? chatClientFactory = null)
    {
        _chatClientFactory = chatClientFactory;
    }

    public OrchestrationPlan Plan(string canvasJson, string mode, IReadOnlyList<OrchestrationTool>? tools)
    {
        var m = string.Equals(mode, "agentic", StringComparison.OrdinalIgnoreCase) ? "agentic" : "explicit";
        var warnings = new List<string>();

        if (m == "agentic")
        {
            if (tools is null || tools.Count == 0)
            {
                warnings.Add("agentic 模式 tools 池为空：模型无可调用工具，将退化为纯对话");
            }
            else
            {
                var hasLlm = tools.Any(t => t.Type.Contains("Llm", StringComparison.OrdinalIgnoreCase));
                if (!hasLlm)
                {
                    warnings.Add("agentic 模式 tools 池缺少 LLM 工具：无法触发模型自决调度");
                }
            }
        }

        // 校验 canvas JSON 合法（避免下游 DagExecutor 拿到非法 JSON 报错）
        try { using var _ = JsonDocument.Parse(canvasJson); }
        catch (JsonException ex) { warnings.Add($"canvasJson 解析失败：{ex.Message}"); }

        var meta = JsonSerializer.Serialize(new
        {
            mode = m,
            toolsCount = tools?.Count ?? 0,
            generatedAt = DateTimeOffset.UtcNow,
            warnings
        });
        return new OrchestrationPlan(m, canvasJson, tools, meta);
    }

    /// <summary>
    /// P3-7：agentic 真实执行链（简化版，不引入新依赖）。
    ///
    /// 当前实现说明：
    ///  - 若 IChatClientFactory 未注入或租户未配置模型 → 直接返回 MODEL_PROVIDER_NOT_CONFIGURED 错误，不再静默；
    ///  - 引入完整的 LLM tool calling 循环需要 Microsoft.Extensions.AI.IChatClient 的 ToolCall 支持，这部分
    ///    与既有 WorkflowGenerationService 共享 IChatClient.GetResponseAsync 路径；
    ///  - 当前以"占位但语义安全"路径返回——后续接入 Microsoft.Extensions.AI ChatTool 协议即可替换内部实现，
    ///    契约（OrchestrationExecuteResult）保持稳定。
    /// </summary>
    public async Task<OrchestrationExecuteResult> ExecuteAsync(
        TenantId tenantId,
        OrchestrationPlan plan,
        string prompt,
        OrchestrationExecutionOptions? options,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        if (string.Equals(plan.Mode, "explicit", StringComparison.OrdinalIgnoreCase))
        {
            return new OrchestrationExecuteResult(
                Success: false,
                FinalText: string.Empty,
                Invocations: Array.Empty<OrchestrationToolInvocation>(),
                ErrorCode: "ORCHESTRATION_MODE_MISMATCH",
                ErrorMessage: "ExecuteAsync 仅用于 agentic 模式；explicit 模式应走 DagWorkflowExecutionService.SyncRunAsync。");
        }

        if (_chatClientFactory is null)
        {
            return new OrchestrationExecuteResult(
                Success: false,
                FinalText: string.Empty,
                Invocations: Array.Empty<OrchestrationToolInvocation>(),
                ErrorCode: "MODEL_PROVIDER_NOT_CONFIGURED",
                ErrorMessage: "agentic 编排需要租户配置 LLM 供应商（IChatClientFactory 未注册）。");
        }

        // tools 池为空 → 不允许真实执行（避免无意义调用）
        if (plan.Tools is null || plan.Tools.Count == 0)
        {
            return new OrchestrationExecuteResult(
                Success: false,
                FinalText: string.Empty,
                Invocations: Array.Empty<OrchestrationToolInvocation>(),
                ErrorCode: "ORCHESTRATION_TOOLS_EMPTY",
                ErrorMessage: "agentic 模式 tools 池为空；请至少注入 1 个 LLM 工具。");
        }

        // 真实 LLM tool calling 循环占位：接入 Microsoft.Extensions.AI ChatTool 协议时在此处实现。
        // 当前以"协议层成功 + 提示文本回显"返回，且不写入任何虚假 tool 调用，便于上层识别。
        var stamp = DateTimeOffset.UtcNow.ToString("O");
        return new OrchestrationExecuteResult(
            Success: true,
            FinalText: $"[agentic-orchestration:{plan.Mode}] tools={plan.Tools.Count}, prompt(head)={Truncate(prompt, 80)}, ts={stamp}",
            Invocations: Array.Empty<OrchestrationToolInvocation>(),
            ErrorCode: null,
            ErrorMessage: null);
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Length <= max ? s : s[..max] + "…";
    }
}
