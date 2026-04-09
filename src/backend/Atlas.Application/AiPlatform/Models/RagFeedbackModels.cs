namespace Atlas.Application.AiPlatform.Models;

public sealed record RagFeedbackCreateRequest(
    string QueryId,
    int Rating,
    string? Comment,
    string? ConversationId,
    string? AgentId);

public sealed record RagFeedbackDto(
    long Id,
    string QueryId,
    int Rating,
    string Comment,
    string ConversationId,
    string AgentId,
    long UserId,
    DateTime CreatedAt);
