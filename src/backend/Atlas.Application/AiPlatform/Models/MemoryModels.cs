namespace Atlas.Application.AiPlatform.Models;

public readonly record struct MemoryNamespace(string Scope)
{
    public string NormalizedScope => string.IsNullOrWhiteSpace(Scope) ? "default" : Scope.Trim().ToLowerInvariant();
}

public sealed record MemoryRecordInput(
    string Key,
    string Content,
    string Source,
    long? ConversationId = null);

public sealed record MemoryRecord(
    long Id,
    string Key,
    string Content,
    string Source,
    int HitCount,
    DateTime LastReferencedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

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
