using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AiPlugin : TenantEntity
{
    public AiPlugin()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        Description = string.Empty;
        Icon = string.Empty;
        Category = string.Empty;
        DefinitionJson = "{}";
        CreatedAt = DateTime.UtcNow;
    }

    public AiPlugin(
        TenantId tenantId,
        string name,
        string? description,
        string? icon,
        string? category,
        AiPluginType type,
        string? definitionJson,
        long id)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        Description = description ?? string.Empty;
        Icon = icon ?? string.Empty;
        Category = category ?? string.Empty;
        Type = type;
        DefinitionJson = string.IsNullOrWhiteSpace(definitionJson) ? "{}" : definitionJson;
        Status = AiPluginStatus.Draft;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string? Icon { get; private set; }
    public string? Category { get; private set; }
    public AiPluginType Type { get; private set; }
    public AiPluginStatus Status { get; private set; }
    public string DefinitionJson { get; private set; }
    public bool IsLocked { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    public void Update(
        string name,
        string? description,
        string? icon,
        string? category,
        AiPluginType type,
        string? definitionJson)
    {
        Name = name;
        Description = description ?? string.Empty;
        Icon = icon ?? string.Empty;
        Category = category ?? string.Empty;
        Type = type;
        DefinitionJson = string.IsNullOrWhiteSpace(definitionJson) ? "{}" : definitionJson;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Publish()
    {
        Status = AiPluginStatus.Published;
        PublishedAt = DateTime.UtcNow;
        UpdatedAt = PublishedAt;
    }

    public void Lock()
    {
        IsLocked = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Unlock()
    {
        IsLocked = false;
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum AiPluginType
{
    Custom = 0,
    BuiltIn = 1
}

public enum AiPluginStatus
{
    Draft = 0,
    Published = 1
}
