namespace Atlas.Application.AiPlatform.Models;

public sealed record ModelConfigDto(
    long Id,
    string Name,
    string ProviderType,
    string BaseUrl,
    string DefaultModel,
    bool IsEnabled,
    bool SupportsEmbedding,
    string? ApiKeyMasked,
    DateTime CreatedAt);

public sealed record ModelConfigCreateRequest(
    string Name,
    string ProviderType,
    string ApiKey,
    string BaseUrl,
    string DefaultModel,
    bool SupportsEmbedding);

public sealed record ModelConfigUpdateRequest(
    string Name,
    string ApiKey,
    string BaseUrl,
    string DefaultModel,
    bool IsEnabled,
    bool SupportsEmbedding);

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
    bool EnableTools);

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
