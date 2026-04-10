using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class ChatMessage : TenantEntity
{
    public ChatMessage()
        : base(TenantId.Empty)
    {
        Role = string.Empty;
        Content = string.Empty;
        Metadata = string.Empty;
    }

    public ChatMessage(
        TenantId tenantId,
        long conversationId,
        string role,
        string content,
        string? metadata,
        bool isContextCleared,
        long id)
        : base(tenantId)
    {
        Id = id;
        ConversationId = conversationId;
        Role = role;
        Content = content;
        Metadata = metadata ?? string.Empty;
        IsContextCleared = isContextCleared;
        CreatedAt = DateTime.UtcNow;
    }

    public long ConversationId { get; private set; }
    public string Role { get; private set; }
    public string Content { get; private set; }
    public string? Metadata { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public bool IsContextCleared { get; private set; }

    public void UpdateContent(string content, string? metadata = null)
    {
        Content = content;
        if (metadata is not null)
        {
            Metadata = metadata;
        }
    }
}
