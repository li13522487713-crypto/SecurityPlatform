using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AiPluginApi : TenantEntity
{
    public AiPluginApi()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        Description = string.Empty;
        Method = "GET";
        Path = "/";
        RequestSchemaJson = "{}";
        ResponseSchemaJson = "{}";
        CreatedAt = DateTime.UtcNow;
    }

    public AiPluginApi(
        TenantId tenantId,
        long pluginId,
        string name,
        string? description,
        string method,
        string path,
        string? requestSchemaJson,
        string? responseSchemaJson,
        int timeoutSeconds,
        long id)
        : base(tenantId)
    {
        Id = id;
        PluginId = pluginId;
        Name = name;
        Description = description ?? string.Empty;
        Method = method;
        Path = path;
        RequestSchemaJson = string.IsNullOrWhiteSpace(requestSchemaJson) ? "{}" : requestSchemaJson;
        ResponseSchemaJson = string.IsNullOrWhiteSpace(responseSchemaJson) ? "{}" : responseSchemaJson;
        TimeoutSeconds = timeoutSeconds > 0 ? timeoutSeconds : 30;
        IsEnabled = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long PluginId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string Method { get; private set; }
    public string Path { get; private set; }
    public string RequestSchemaJson { get; private set; }
    public string ResponseSchemaJson { get; private set; }
    public int TimeoutSeconds { get; private set; }
    public bool IsEnabled { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public void Update(
        string name,
        string? description,
        string method,
        string path,
        string? requestSchemaJson,
        string? responseSchemaJson,
        int timeoutSeconds,
        bool isEnabled)
    {
        Name = name;
        Description = description ?? string.Empty;
        Method = method;
        Path = path;
        RequestSchemaJson = string.IsNullOrWhiteSpace(requestSchemaJson) ? "{}" : requestSchemaJson;
        ResponseSchemaJson = string.IsNullOrWhiteSpace(responseSchemaJson) ? "{}" : responseSchemaJson;
        TimeoutSeconds = timeoutSeconds > 0 ? timeoutSeconds : 30;
        IsEnabled = isEnabled;
        UpdatedAt = DateTime.UtcNow;
    }
}
