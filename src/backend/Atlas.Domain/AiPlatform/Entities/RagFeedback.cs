using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

[SugarTable("RagFeedback")]
[SugarIndex("UX_RagFeedback_Tenant_Query_User", nameof(TenantIdValue), OrderByType.Asc, nameof(QueryId), OrderByType.Asc, nameof(UserId), OrderByType.Asc, true)]
public sealed class RagFeedback : TenantEntity
{
    public RagFeedback()
        : base(TenantId.Empty)
    {
        QueryId = string.Empty;
        Comment = string.Empty;
        ConversationId = string.Empty;
        AgentId = string.Empty;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public RagFeedback(
        TenantId tenantId,
        long id,
        string queryId,
        int rating,
        string comment,
        string conversationId,
        string agentId,
        long userId,
        DateTimeOffset createdAt)
        : base(tenantId)
    {
        Id = id;
        QueryId = queryId;
        Rating = rating;
        Comment = comment;
        ConversationId = conversationId;
        AgentId = agentId;
        UserId = userId;
        CreatedAt = createdAt;
    }

    public string QueryId { get; private set; }
    public int Rating { get; private set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string Comment { get; private set; }
    public string ConversationId { get; private set; }
    public string AgentId { get; private set; }
    public long UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
