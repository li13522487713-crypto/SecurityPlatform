namespace Atlas.Application.AiPlatform.Models;

public sealed record ModelConfigDto(
    long Id,
    string Name,
    string ProviderType,
    string BaseUrl,
    string DefaultModel,
    string ModelId,
    string? SystemPrompt,
    bool IsEnabled,
    bool SupportsEmbedding,
    bool EnableStreaming,
    bool EnableReasoning,
    bool EnableTools,
    bool EnableVision,
    bool EnableJsonMode,
    float? Temperature,
    int? MaxTokens,
    float? TopP,
    float? FrequencyPenalty,
    float? PresencePenalty,
    string? ApiKeyMasked,
    DateTime CreatedAt,
    string? WorkspaceId = null);

public sealed record ModelConfigCreateRequest(
    string Name,
    string ProviderType,
    string ApiKey,
    string BaseUrl,
    string DefaultModel,
    bool SupportsEmbedding,
    string? ModelId = null,
    string? SystemPrompt = null,
    bool EnableStreaming = true,
    bool EnableReasoning = false,
    bool EnableTools = false,
    bool EnableVision = false,
    bool EnableJsonMode = false,
    float? Temperature = null,
    int? MaxTokens = null,
    float? TopP = null,
    float? FrequencyPenalty = null,
    float? PresencePenalty = null,
    string? WorkspaceId = null);

public sealed record ModelConfigUpdateRequest(
    string Name,
    string ApiKey,
    string BaseUrl,
    string DefaultModel,
    bool IsEnabled,
    bool SupportsEmbedding,
    string? ModelId = null,
    string? SystemPrompt = null,
    bool? EnableStreaming = null,
    bool? EnableReasoning = null,
    bool? EnableTools = null,
    bool? EnableVision = null,
    bool? EnableJsonMode = null,
    float? Temperature = null,
    int? MaxTokens = null,
    float? TopP = null,
    float? FrequencyPenalty = null,
    float? PresencePenalty = null,
    string? WorkspaceId = null);

public sealed record ModelConfigTestRequest(
    long? ModelConfigId,
    string ProviderType,
    string ApiKey,
    string BaseUrl,
    string Model);

public sealed record ModelConfigPromptTestRequest(
    long? ModelConfigId,
    string ProviderType,
    string ApiKey,
    string BaseUrl,
    string Model,
    string Prompt,
    bool EnableReasoning,
    bool EnableTools,
    bool EnableStreaming = true);

public sealed record ModelConfigPromptTestStreamEvent(
    string EventType,
    string Data);

public sealed record ModelConfigTestResult(
    bool Success,
    string? ErrorMessage,
    int? LatencyMs);

public sealed record ModelConfigStatsDto(
    long Total,
    long Enabled,
    long Disabled,
    long EmbeddingCount);
