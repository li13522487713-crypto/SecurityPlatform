namespace Atlas.Application.AiPlatform.Models;

public sealed record AgentListItem(
    long Id,
    string Name,
    string? Description,
    string? AvatarUrl,
    string Status,
    string? ModelName,
    DateTime CreatedAt,
    int PublishVersion);

public sealed record AgentDetail(
    long Id,
    string Name,
    string? Description,
    string? AvatarUrl,
    string? SystemPrompt,
    long? ModelConfigId,
    string? ModelName,
    float? Temperature,
    int? MaxTokens,
    string Status,
    long CreatorId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? PublishedAt,
    int PublishVersion,
    IReadOnlyList<long>? KnowledgeBaseIds);

public sealed record AgentCreateRequest(
    string Name,
    string? Description,
    string? SystemPrompt,
    long? ModelConfigId,
    string? ModelName,
    float? Temperature,
    int? MaxTokens);

public sealed record AgentUpdateRequest(
    string Name,
    string? Description,
    string? AvatarUrl,
    string? SystemPrompt,
    long? ModelConfigId,
    string? ModelName,
    float? Temperature,
    int? MaxTokens,
    IReadOnlyList<long>? KnowledgeBaseIds);
