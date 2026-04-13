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
        Keyword = string.Empty;
        Name = string.Empty;
        Description = string.Empty;
        ValueType = string.Empty;
        DefaultValueJson = "{}";
        SchemaJson = "{}";
        Channel = string.Empty;
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
        Keyword = key;
        Name = key;
        Description = string.Empty;
        ValueType = "string";
        DefaultValueJson = string.IsNullOrWhiteSpace(value) ? "{}" : value;
        SchemaJson = "{}";
        Channel = scope == AiVariableScope.System ? "system" : "custom";
        Scope = scope;
        ScopeId = scopeId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string Key { get; private set; }
    public string? Value { get; private set; }
    public string Keyword { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string ValueType { get; private set; }
    public string DefaultValueJson { get; private set; }
    public string SchemaJson { get; private set; }
    public string Channel { get; private set; }
    public bool IsReadonly { get; private set; }
    public bool IsSystem { get; private set; }
    public AiVariableScope Scope { get; private set; }
    public long? ScopeId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public void Update(
        string key,
        string? value,
        AiVariableScope scope,
        long? scopeId,
        string? keyword = null,
        string? name = null,
        string? description = null,
        string? valueType = null,
        string? defaultValueJson = null,
        string? schemaJson = null,
        string? channel = null,
        bool? isReadonly = null,
        bool? isSystem = null)
    {
        Key = key;
        Value = value;
        Keyword = string.IsNullOrWhiteSpace(keyword) ? key : keyword;
        Name = string.IsNullOrWhiteSpace(name) ? Keyword : name;
        Description = description ?? string.Empty;
        ValueType = string.IsNullOrWhiteSpace(valueType) ? ValueType : valueType;
        DefaultValueJson = string.IsNullOrWhiteSpace(defaultValueJson) ? DefaultValueJson : defaultValueJson;
        SchemaJson = string.IsNullOrWhiteSpace(schemaJson) ? SchemaJson : schemaJson;
        Channel = string.IsNullOrWhiteSpace(channel) ? Channel : channel;
        IsReadonly = isReadonly ?? IsReadonly;
        IsSystem = isSystem ?? (scope == AiVariableScope.System);
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
