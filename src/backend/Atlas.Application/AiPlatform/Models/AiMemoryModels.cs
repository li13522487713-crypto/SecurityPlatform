namespace Atlas.Application.AiPlatform.Models;

public sealed record LongTermMemoryListItem(
    long Id,
    long AgentId,
    long ConversationId,
    string MemoryKey,
    string Content,
    string Source,
    int HitCount,
    DateTime LastReferencedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
