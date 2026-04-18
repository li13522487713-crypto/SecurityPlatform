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

public sealed class DualOrchestrationEngine : IDualOrchestrationEngine
{
    public OrchestrationPlan Plan(string canvasJson, string mode, IReadOnlyList<OrchestrationTool>? tools)
    {
        var m = string.Equals(mode, "agentic", StringComparison.OrdinalIgnoreCase) ? "agentic" : "explicit";
        var meta = JsonSerializer.Serialize(new
        {
            mode = m,
            toolsCount = tools?.Count ?? 0,
            generatedAt = DateTimeOffset.UtcNow
        });
        return new OrchestrationPlan(m, canvasJson, tools, meta);
    }
}
