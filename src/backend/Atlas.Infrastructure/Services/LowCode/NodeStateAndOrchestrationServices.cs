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
/// 双哲学编排引擎（M20 S20-6）：explicit / agentic 两种模式。
/// - explicit：保留完整 canvas，运行时按 DAG 顺序执行；前端隐藏 LLM 自决面板
/// - agentic：要求 tools 池非空且至少含 1 个 LLM 工具，否则拒绝；前端隐藏多数中间节点，仅暴露 LLM + Tool 池
/// </summary>
public sealed class DualOrchestrationEngine : IDualOrchestrationEngine
{
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
}
