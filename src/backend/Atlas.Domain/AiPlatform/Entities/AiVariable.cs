using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AiVariable : TenantEntity
{
    public AiVariable()
        : base(TenantId.Empty)
    {
        Key = string.Empty;
        Value = string.Empty;
        CreatedAt = DateTime.UtcNow;
    }

    public AiVariable(
        TenantId tenantId,
        string key,
        string? value,
        AiVariableScope scope,
        long? scopeId,
        long id)
        : base(tenantId)
    {
        Id = id;
        Key = key;
        Value = value;
        Scope = scope;
        ScopeId = scopeId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string Key { get; private set; }
    public string? Value { get; private set; }
    public AiVariableScope Scope { get; private set; }
    public long? ScopeId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public void Update(string key, string? value, AiVariableScope scope, long? scopeId)
    {
        Key = key;
        Value = value;
        Scope = scope;
        ScopeId = scopeId;
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum AiVariableScope
{
    System = 0,
    Project = 1,
    Bot = 2
}
