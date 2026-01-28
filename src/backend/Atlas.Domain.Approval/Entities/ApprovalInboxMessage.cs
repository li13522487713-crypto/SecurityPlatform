using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Domain.Approval.Entities;

/// <summary>
/// 审批流程站内信（消息落库）
/// </summary>
public sealed class ApprovalInboxMessage : TenantEntity
{
    public ApprovalInboxMessage()
        : base(TenantId.Empty)
    {
        RecipientUserId = 0;
        Title = string.Empty;
        Content = string.Empty;
    }

    public ApprovalInboxMessage(
        TenantId tenantId,
        long recipientUserId,
        long? instanceId,
        long? taskId,
        ApprovalNotificationEventType eventType,
        string title,
        string content,
        long id)
        : base(tenantId)
    {
        Id = id;
        RecipientUserId = recipientUserId;
        InstanceId = instanceId;
        TaskId = taskId;
        EventType = eventType;
        Title = title;
        Content = content;
        IsRead = false;
        CreatedAt = DateTimeOffset.UtcNow;
        ReadAt = null;
    }

    /// <summary>收件人用户 ID</summary>
    public long RecipientUserId { get; private set; }

    /// <summary>流程实例 ID（可选）</summary>
    public long? InstanceId { get; private set; }

    /// <summary>任务 ID（可选）</summary>
    public long? TaskId { get; private set; }

    /// <summary>事件类型</summary>
    public ApprovalNotificationEventType EventType { get; private set; }

    /// <summary>消息标题</summary>
    public string Title { get; private set; }

    /// <summary>消息内容</summary>
    public string Content { get; private set; }

    /// <summary>是否已读</summary>
    public bool IsRead { get; private set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>阅读时间</summary>
    public DateTimeOffset? ReadAt { get; private set; }

    public void MarkAsRead(DateTimeOffset now)
    {
        IsRead = true;
        ReadAt = now;
    }
}
