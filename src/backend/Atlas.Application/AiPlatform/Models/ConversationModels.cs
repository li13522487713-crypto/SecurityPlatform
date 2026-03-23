namespace Atlas.Application.AiPlatform.Models;

public sealed record ConversationDto(
    long Id,
    long AgentId,
    long UserId,
    string? Title,
    DateTime CreatedAt,
    DateTime? LastMessageAt,
    int MessageCount);

public sealed record ConversationCreateRequest(long AgentId, string? Title = null);

public sealed record ConversationUpdateRequest(string Title);

public sealed record ChatMessageDto(
    long Id,
    string Role,
    string Content,
    string? Metadata,
    DateTime CreatedAt,
    bool IsContextCleared);

public sealed record AgentChatRequest(
    long? ConversationId,
    string Message,
    bool? EnableRag);

public sealed record AgentChatResponse(
    long ConversationId,
    long MessageId,
    string Content,
    string? Sources);

public sealed record AgentChatCancelRequest(long ConversationId);

public sealed record AgentChatStreamEvent(
    string EventType,
    string Data);

public sealed record AgentToolCallStep(
    string EventType,
    string Data);

public sealed record AgentToolCallResult(
    bool Executed,
    string? FinalAnswer,
    IReadOnlyList<AgentToolCallStep> Steps,
    string? MetadataJson);
