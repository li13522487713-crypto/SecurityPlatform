using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.System.Entities;

/// <summary>
/// 通知公告实体
/// </summary>
public sealed class Notification : TenantEntity
{
    public Notification()
        : base(TenantId.Empty)
    {
        Title = string.Empty;
        Content = string.Empty;
        NoticeType = "Announcement";
        PublisherName = string.Empty;
        IsActive = true;
    }

    public Notification(
        TenantId tenantId,
        string title,
        string content,
        string noticeType,
        int priority,
        long publisherId,
        string publisherName,
        DateTimeOffset publishedAt,
        DateTimeOffset? expiresAt,
        long id)
        : base(tenantId)
    {
        Id = id;
        Title = title;
        Content = content;
        NoticeType = noticeType;
        Priority = priority;
        PublisherId = publisherId;
        PublisherName = publisherName;
        PublishedAt = publishedAt;
        ExpiresAt = expiresAt;
        IsActive = true;
    }

    public string Title { get; private set; }
    public string Content { get; private set; }

    /// <summary>类型：Announcement / System / Reminder</summary>
    public string NoticeType { get; private set; }

    /// <summary>优先级：0=普通 1=重要 2=紧急</summary>
    public int Priority { get; private set; }

    public long PublisherId { get; private set; }
    public string PublisherName { get; private set; }
    public DateTimeOffset PublishedAt { get; private set; }

    /// <summary>过期时间，null 表示永不过期</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public void Update(
        string title,
        string content,
        string noticeType,
        int priority,
        DateTimeOffset? expiresAt)
    {
        Title = title;
        Content = content;
        NoticeType = noticeType;
        Priority = priority;
        ExpiresAt = expiresAt;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
