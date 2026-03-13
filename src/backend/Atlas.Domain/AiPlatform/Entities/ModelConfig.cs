using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class ModelConfig : TenantEntity
{
    public ModelConfig()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        ProviderType = string.Empty;
        ApiKey = string.Empty;
        BaseUrl = string.Empty;
        DefaultModel = string.Empty;
    }

    public ModelConfig(
        TenantId tenantId,
        string name,
        string providerType,
        string apiKey,
        string baseUrl,
        string defaultModel,
        long id)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        ProviderType = providerType;
        ApiKey = apiKey;
        BaseUrl = baseUrl;
        DefaultModel = defaultModel;
        IsEnabled = true;
        SupportsEmbedding = true;
        CreatedAt = DateTime.UtcNow;
    }

    public string Name { get; private set; }
    public string ProviderType { get; private set; }
    public string ApiKey { get; private set; }
    public string BaseUrl { get; private set; }
    public string DefaultModel { get; private set; }
    public bool IsEnabled { get; private set; }
    public bool SupportsEmbedding { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public void Update(
        string name,
        string apiKey,
        string baseUrl,
        string defaultModel,
        bool isEnabled,
        bool supportsEmbedding)
    {
        Name = name;
        ApiKey = apiKey;
        BaseUrl = baseUrl;
        DefaultModel = defaultModel;
        IsEnabled = isEnabled;
        SupportsEmbedding = supportsEmbedding;
        UpdatedAt = DateTime.UtcNow;
    }
}
