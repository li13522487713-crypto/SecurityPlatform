using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class Conversation : TenantEntity
{
    public Conversation()
        : base(TenantId.Empty)
    {
        Title = string.Empty;
    }

    public Conversation(
        TenantId tenantId,
        long agentId,
        long userId,
        string? title,
        long id)
        : base(tenantId)
    {
        Id = id;
        AgentId = agentId;
        UserId = userId;
        Title = title;
        CreatedAt = DateTime.UtcNow;
        LastMessageAt = CreatedAt;
        LastContextClearedAt = DateTime.UnixEpoch;
        MessageCount = 0;
    }

    public long AgentId { get; private set; }
    public long UserId { get; private set; }
    public string? Title { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime LastMessageAt { get; private set; }
    public int MessageCount { get; private set; }
    public DateTime LastContextClearedAt { get; private set; }

    public void AddMessage(DateTime? messageAt = null)
    {
        MessageCount++;
        LastMessageAt = messageAt ?? DateTime.UtcNow;
    }

    public void RemoveMessage(DateTime? lastMessageAt = null)
    {
        MessageCount = Math.Max(0, MessageCount - 1);
        LastMessageAt = lastMessageAt ?? DateTime.UnixEpoch;
    }

    public void ResetMessages()
    {
        MessageCount = 0;
        LastMessageAt = DateTime.UnixEpoch;
    }

    public void UpdateTitle(string title)
    {
        Title = title;
    }

    public void ClearContext(DateTime? clearedAt = null)
    {
        LastContextClearedAt = clearedAt ?? DateTime.UtcNow;
    }
}
