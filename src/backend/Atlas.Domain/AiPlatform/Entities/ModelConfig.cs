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
        ModelId = string.Empty;
        SystemPrompt = string.Empty;
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
        ModelId = defaultModel;
        SystemPrompt = string.Empty;
        IsEnabled = true;
        SupportsEmbedding = true;
        EnableStreaming = true;
        CreatedAt = DateTime.UtcNow;
    }

    public string Name { get; private set; }
    public string ProviderType { get; private set; }
    public string ApiKey { get; private set; }
    public string BaseUrl { get; private set; }
    public string DefaultModel { get; private set; }
    public string ModelId { get; private set; }
    public string? SystemPrompt { get; private set; }
    public bool IsEnabled { get; private set; }
    public bool SupportsEmbedding { get; private set; }
    public bool EnableStreaming { get; private set; }
    public bool EnableReasoning { get; private set; }
    public bool EnableTools { get; private set; }
    public bool EnableVision { get; private set; }
    public bool EnableJsonMode { get; private set; }
    public float? Temperature { get; private set; }
    public int? MaxTokens { get; private set; }
    public float? TopP { get; private set; }
    public float? FrequencyPenalty { get; private set; }
    public float? PresencePenalty { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public void Update(
        string name,
        string apiKey,
        string baseUrl,
        string defaultModel,
        bool isEnabled,
        bool supportsEmbedding,
        string? modelId = null,
        string? systemPrompt = null,
        bool? enableStreaming = null,
        bool? enableReasoning = null,
        bool? enableTools = null,
        bool? enableVision = null,
        bool? enableJsonMode = null,
        float? temperature = null,
        int? maxTokens = null,
        float? topP = null,
        float? frequencyPenalty = null,
        float? presencePenalty = null)
    {
        Name = name;
        ApiKey = apiKey;
        BaseUrl = baseUrl;
        DefaultModel = defaultModel;
        IsEnabled = isEnabled;
        SupportsEmbedding = supportsEmbedding;

        if (modelId is not null)
            ModelId = modelId;
        SystemPrompt = systemPrompt ?? string.Empty;
        if (enableStreaming.HasValue)
            EnableStreaming = enableStreaming.Value;
        if (enableReasoning.HasValue)
            EnableReasoning = enableReasoning.Value;
        if (enableTools.HasValue)
            EnableTools = enableTools.Value;
        if (enableVision.HasValue)
            EnableVision = enableVision.Value;
        if (enableJsonMode.HasValue)
            EnableJsonMode = enableJsonMode.Value;
        Temperature = temperature;
        MaxTokens = maxTokens;
        TopP = topP;
        FrequencyPenalty = frequencyPenalty;
        PresencePenalty = presencePenalty;
        UpdatedAt = DateTime.UtcNow;
    }
}
