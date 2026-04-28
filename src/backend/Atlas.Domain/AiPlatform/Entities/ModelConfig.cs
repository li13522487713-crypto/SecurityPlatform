using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

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
        long id,
        string? workspaceId = null)
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
        WorkspaceId = NormalizeWorkspaceId(workspaceId);
        CreatedAt = DateTime.UtcNow;
    }

    [SugarColumn(Length = 128)]
    public string Name { get; private set; }

    [SugarColumn(Length = 64)]
    public string ProviderType { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string ApiKey { get; private set; }

    [SugarColumn(Length = 512)]
    public string BaseUrl { get; private set; }

    [SugarColumn(Length = 256)]
    public string DefaultModel { get; private set; }

    [SugarColumn(Length = 256)]
    public string ModelId { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true)]
    public string? SystemPrompt { get; private set; }
    public bool IsEnabled { get; private set; }
    public bool SupportsEmbedding { get; private set; }
    public bool EnableStreaming { get; private set; }
    public bool EnableReasoning { get; private set; }
    public bool EnableTools { get; private set; }
    public bool EnableVision { get; private set; }
    public bool EnableJsonMode { get; private set; }
    [SugarColumn(IsNullable = true)]
    public float? Temperature { get; private set; }

    [SugarColumn(IsNullable = true)]
    public int? MaxTokens { get; private set; }

    [SugarColumn(IsNullable = true)]
    public float? TopP { get; private set; }

    [SugarColumn(IsNullable = true)]
    public float? FrequencyPenalty { get; private set; }

    [SugarColumn(IsNullable = true)]
    public float? PresencePenalty { get; private set; }

    public DateTime CreatedAt { get; private set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? UpdatedAt { get; private set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? WorkspaceId { get; private set; }

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
        float? presencePenalty = null,
        string? workspaceId = null)
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
        if (workspaceId is not null)
            WorkspaceId = NormalizeWorkspaceId(workspaceId);
        UpdatedAt = DateTime.UtcNow;
    }

    private static string? NormalizeWorkspaceId(string? workspaceId)
    {
        var trimmed = workspaceId?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
