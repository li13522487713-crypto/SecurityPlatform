namespace Atlas.Application.AiPlatform.Models;

public sealed record AdminAiConfigDto(
    bool EnableAiPlatform,
    bool EnableOpenPlatform,
    bool EnableCodeSandbox,
    bool EnableMarketplace,
    bool EnableContentModeration,
    int MaxDailyTokensPerUser,
    int MaxKnowledgeRetrievalCount);

public sealed record AdminAiConfigUpdateRequest(
    bool EnableAiPlatform,
    bool EnableOpenPlatform,
    bool EnableCodeSandbox,
    bool EnableMarketplace,
    bool EnableContentModeration,
    int MaxDailyTokensPerUser,
    int MaxKnowledgeRetrievalCount);
