using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class LongTermMemory : TenantEntity
{
    public LongTermMemory()
        : base(TenantId.Empty)
    {
        MemoryKey = string.Empty;
        Content = string.Empty;
        Source = string.Empty;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        LastReferencedAt = CreatedAt;
        HitCount = 1;
    }

    public LongTermMemory(
        TenantId tenantId,
        long userId,
        long agentId,
        long conversationId,
        string memoryKey,
        string content,
        string source,
        long id)
        : base(tenantId)
    {
        Id = id;
        UserId = userId;
        AgentId = agentId;
        ConversationId = conversationId;
        MemoryKey = memoryKey;
        Content = content;
        Source = source;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        LastReferencedAt = CreatedAt;
        HitCount = 1;
    }

    public long UserId { get; private set; }
    public long AgentId { get; private set; }
    public long ConversationId { get; private set; }
    public string MemoryKey { get; private set; }
    public string Content { get; private set; }
    public string Source { get; private set; }
    public int HitCount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime LastReferencedAt { get; private set; }

    public void Reinforce(string content, string source, long conversationId)
    {
        Content = content;
        Source = source;
        ConversationId = conversationId;
        HitCount += 1;
        LastReferencedAt = DateTime.UtcNow;
        UpdatedAt = LastReferencedAt;
    }

    public void Touch()
    {
        HitCount += 1;
        LastReferencedAt = DateTime.UtcNow;
        UpdatedAt = LastReferencedAt;
    }
}
