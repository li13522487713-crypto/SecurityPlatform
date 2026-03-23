namespace Atlas.Application.AiPlatform.Models;

public sealed record ChatHistoryMessage(
    long MessageId,
    string Role,
    string Content,
    bool IsContextCleared,
    DateTime CreatedAt);

public sealed record LongTermMemoryRecallItem(
    long Id,
    string MemoryKey,
    string Content,
    double Score);
