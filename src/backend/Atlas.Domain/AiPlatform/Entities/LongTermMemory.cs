using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Enums;
using SqlSugar;

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
        ResourceLibrarySource = LibrarySource.Custom;
    }

    public LongTermMemory(
        TenantId tenantId,
        long userId,
        long agentId,
        long conversationId,
        string memoryKey,
        string content,
        string source,
        long id,
        LibrarySource resourceLibrarySource = LibrarySource.Custom)
        : base(tenantId)
    {
        Id = id;
        UserId = userId;
        AgentId = agentId;
        ConversationId = conversationId;
        MemoryKey = memoryKey;
        Content = content;
        Source = source;
        ResourceLibrarySource = resourceLibrarySource;
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

    /// <summary>资源库「官方/自定义」来源（区别于业务字段 <see cref="Source"/> 的记忆出处）。</summary>
    [SugarColumn(IsNullable = false)]
    public LibrarySource ResourceLibrarySource { get; private set; }

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
