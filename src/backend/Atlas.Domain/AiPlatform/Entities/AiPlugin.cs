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
        ManifestJson = "{}";
        ServerUrl = string.Empty;
        DefinitionJson = "{}";
        AuthConfigJson = "{}";
        ToolSchemaJson = "{}";
        OpenApiSpecJson = "{}";
        WorkspaceId = 0;
        LockOwnerId = 0;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        PublishedAt = DateTime.UnixEpoch;
    }

    public AiPlugin(
        TenantId tenantId,
        string name,
        string? description,
        string? icon,
        string? category,
        AiPluginType type,
        string? definitionJson,
        AiPluginSourceType sourceType,
        AiPluginAuthType authType,
        string? authConfigJson,
        string? toolSchemaJson,
        string? openApiSpecJson,
        long id,
        long? workspaceId = null)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        WorkspaceId = workspaceId ?? 0;
        Description = description ?? string.Empty;
        Icon = icon ?? string.Empty;
        Category = category ?? string.Empty;
        ManifestJson = "{}";
        ServerUrl = string.Empty;
        Type = type;
        DefinitionJson = string.IsNullOrWhiteSpace(definitionJson) ? "{}" : definitionJson;
        SourceType = sourceType;
        AuthType = authType;
        AuthConfigJson = string.IsNullOrWhiteSpace(authConfigJson) ? "{}" : authConfigJson;
        ToolSchemaJson = string.IsNullOrWhiteSpace(toolSchemaJson) ? "{}" : toolSchemaJson;
        OpenApiSpecJson = string.IsNullOrWhiteSpace(openApiSpecJson) ? "{}" : openApiSpecJson;
        Status = AiPluginStatus.Draft;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        PublishedAt = DateTime.UnixEpoch;
    }

    public string Name { get; private set; }
    public long WorkspaceId { get; private set; }
    public string? Description { get; private set; }
    public string? Icon { get; private set; }
    public string? Category { get; private set; }
    public string ManifestJson { get; private set; }
    public string? ServerUrl { get; private set; }
    public AiPluginType Type { get; private set; }
    public AiPluginSourceType SourceType { get; private set; }
    public AiPluginAuthType AuthType { get; private set; }
    public AiPluginStatus Status { get; private set; }
    public string DefinitionJson { get; private set; }
    public string AuthConfigJson { get; private set; }
    public string ToolSchemaJson { get; private set; }
    public string OpenApiSpecJson { get; private set; }
    public int PublishedVersion { get; private set; }
    public bool IsLocked { get; private set; }
    public long LockOwnerId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    public void Update(
        string name,
        string? description,
        string? icon,
        string? category,
        AiPluginType type,
        string? definitionJson,
        AiPluginSourceType sourceType,
        AiPluginAuthType authType,
        string? authConfigJson,
        string? toolSchemaJson,
        string? openApiSpecJson,
        string? manifestJson = null,
        string? serverUrl = null,
        long? workspaceId = null)
    {
        Name = name;
        if (workspaceId.HasValue)
        {
            WorkspaceId = workspaceId.Value;
        }
        Description = description ?? string.Empty;
        Icon = icon ?? string.Empty;
        Category = category ?? string.Empty;
        ManifestJson = string.IsNullOrWhiteSpace(manifestJson) ? "{}" : manifestJson;
        ServerUrl = serverUrl ?? string.Empty;
        Type = type;
        DefinitionJson = string.IsNullOrWhiteSpace(definitionJson) ? "{}" : definitionJson;
        SourceType = sourceType;
        AuthType = authType;
        AuthConfigJson = string.IsNullOrWhiteSpace(authConfigJson) ? "{}" : authConfigJson;
        ToolSchemaJson = string.IsNullOrWhiteSpace(toolSchemaJson) ? "{}" : toolSchemaJson;
        OpenApiSpecJson = string.IsNullOrWhiteSpace(openApiSpecJson) ? "{}" : openApiSpecJson;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Publish()
    {
        Status = AiPluginStatus.Published;
        PublishedVersion++;
        PublishedAt = DateTime.UtcNow;
        UpdatedAt = PublishedAt;
    }

    public void Lock(long? ownerId = null)
    {
        IsLocked = true;
        LockOwnerId = ownerId ?? 0;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Unlock()
    {
        IsLocked = false;
        LockOwnerId = 0;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignWorkspace(long workspaceId)
    {
        if (workspaceId <= 0)
        {
            return;
        }

        WorkspaceId = workspaceId;
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum AiPluginType
{
    Custom = 0,
    BuiltIn = 1
}

public enum AiPluginSourceType
{
    Manual = 0,
    OpenApiImport = 1,
    BuiltInCatalog = 2
}

public enum AiPluginAuthType
{
    None = 0,
    ApiKey = 1,
    BearerToken = 2,
    Basic = 3,
    Custom = 4
}

public enum AiPluginStatus
{
    Draft = 0,
    Published = 1
}
