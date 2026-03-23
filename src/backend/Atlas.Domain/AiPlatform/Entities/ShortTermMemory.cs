using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class ShortTermMemory : TenantEntity
{
    public ShortTermMemory()
        : base(TenantId.Empty)
    {
        Summary = string.Empty;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public ShortTermMemory(
        TenantId tenantId,
        long conversationId,
        long agentId,
        long userId,
        string summary,
        int summarizedMessageCount,
        long id)
        : base(tenantId)
    {
        Id = id;
        ConversationId = conversationId;
        AgentId = agentId;
        UserId = userId;
        Summary = summary;
        SummarizedMessageCount = summarizedMessageCount;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public long ConversationId { get; private set; }
    public long AgentId { get; private set; }
    public long UserId { get; private set; }
    public string Summary { get; private set; }
    public int SummarizedMessageCount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void UpdateSummary(string summary, int summarizedMessageCount)
    {
        Summary = summary;
        SummarizedMessageCount = summarizedMessageCount;
        UpdatedAt = DateTime.UtcNow;
    }
}
