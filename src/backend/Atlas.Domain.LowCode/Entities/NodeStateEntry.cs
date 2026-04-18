using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// 节点级状态持久化（M20 S20-7）。按作用域隔离：session / conversation / trigger / app。
/// 与 docs/lowcode-orchestration-spec.md §3 对齐。
/// </summary>
public sealed class NodeStateEntry : TenantEntity
{
#pragma warning disable CS8618
    public NodeStateEntry() : base(TenantId.Empty)
    {
        Scope = "session";
        ScopeKey = string.Empty;
        NodeKey = string.Empty;
        StateJson = "{}";
    }
#pragma warning restore CS8618

    public NodeStateEntry(TenantId tenantId, long id, string scope, string scopeKey, string nodeKey, string stateJson)
        : base(tenantId)
    {
        Id = id;
        Scope = scope;
        ScopeKey = scopeKey;
        NodeKey = nodeKey;
        StateJson = stateJson;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>session / conversation / trigger / app。</summary>
    [SugarColumn(Length = 32, IsNullable = false)]
    public string Scope { get; private set; }

    [SugarColumn(Length = 128, IsNullable = false)]
    public string ScopeKey { get; private set; }

    [SugarColumn(Length = 128, IsNullable = false)]
    public string NodeKey { get; private set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = false)]
    public string StateJson { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Replace(string stateJson)
    {
        StateJson = stateJson;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
