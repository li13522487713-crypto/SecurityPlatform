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

public sealed record ConversationAppendMessageRequest(
    string Role,
    string Content,
    string? Metadata = null);

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
    bool? EnableRag,
    IReadOnlyList<AgentChatAttachment>? Attachments = null);

public sealed record AgentChatAttachment(
    string Type,
    string? Url,
    string? FileId,
    string? MimeType,
    string? Name,
    string? Text);

public sealed record AgentChatResponse(
    long ConversationId,
    long MessageId,
    string Content,
    string? Sources);

public sealed record AgentChatCancelRequest(long ConversationId);

public sealed record AgentChatStreamEvent(
    string EventType,
    string Data);
